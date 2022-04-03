using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//This moves about and can be controlled with platformer movement.
//Collisions are handled by a series of raycasts. instead of a single boxcast. Helps with edge detection.
//IE: This lets us handle lips (consider just the bottom ray hitting)

//We also can easily have a unique rectangular shape (here) and have like enemy/bullet/whatever collisions independent. 
namespace BloopsPlatform
{
    public class PlatformPhysics : MonoBehaviour
    {
        #region Fields
        private Vector2 boundsOffset;
        [Header("Collision Settings")]
        [SerializeField] private Bounds movementShape;
        [SerializeField] private LayerMask layerMask;
        [SerializeField] private float skinWidth;
        [SerializeField] private float lipHeight;
        //Horizontal lip, basically.
        private float verticalCastPadding => lipHeight;
        [Header("Gravity Settings")]
        [SerializeField] private float baseGravity = -9.81f;
        [SerializeField] private float upwardsGravityModifier=1;
        [SerializeField] private float downwardsGravityModifier=1;
        [Header("Jump Settings")]
        [SerializeField] private int airJumps;
        [SerializeField] private float jumpForce;
        private bool _pressingJump = false;
        [SerializeField] private float holdJumpForTime = 0.25f;
        //gravity holding timer...
        [SerializeField] private float jumpHeldGravityModifier = 1;
        private int jumps = 0;
        private bool tryJump;
        [Header("Wall Cling")] [SerializeField] private bool canWallJump;
        [SerializeField] private float wallJumpForceModifier = 1;
        [SerializeField][Range(0,1)]
        float wallClingingGravityModifier = 1;
        private bool wallClingingLeft;
        private bool wallClingingRight;
        private float timeWallClinging;

        [Header("Coyote Time")] [SerializeField]
        private float coyoteTime;

        [SerializeField] private float jumpBuffer;
        [Header("Running Movement Settings")]
        [SerializeField] private float maxHorizontalSpeed;
        [SerializeField] private float maxHorizontalAcceleration = 35;
        [SerializeField] private float maxHorizontalDeccelToZero = 100;
        [SerializeField] private float maxInAirHorizontalAcceleration = 20;
        private Caster downCaster;
        private Caster rightCaster;
        private Caster leftCaster;
        private Caster upCaster;
        //Timers
        private float timeSinceGrounded;
        private float timeSinceLeftGround;
        private float timeSinceLastJumped;
        private float timeSincePressedJump;
        //physics Settings
        public bool Grounded { get; private set; }
        public Vector2 Velocity { get; private set; }
        private Vector2 _desiredVelocity;
        private bool wallClinging => wallClingingLeft || wallClingingRight;
        private float CurrentGroundFriction => Grounded ? downCaster.Friction : 0;

        private IMovingPlatform _movingPlatform;
        private Vector2 adoptedVel = Vector2.zero;
        private Vector2 RealVelocity => adoptedVel + Velocity;
        private bool _cantGoFurtherRightThisFrame;//horizontal hit tracking so we dont pickup movingPlatform velocities and override collisions.
        private bool _cantGoFurtherLeftThisFrame;
        #endregion

        private void Awake()
        {
            if (lipHeight == 0)
            {
                //Cant actually be 0!
                lipHeight = Mathf.Epsilon;
            }

            adoptedVel = Vector2.zero;
            _movingPlatform = null;
            timeSinceGrounded = 0;
            timeSinceLastJumped = 0;
            timeSinceLeftGround = 0;
            _pressingJump = false;
            tryJump = false;
            boundsOffset = movementShape.center;
            downCaster = new Caster(layerMask, 10);
            upCaster = new Caster(layerMask, 5);
            leftCaster = new Caster(layerMask, 10);
            rightCaster = new Caster(layerMask, 10);
            
            ///Debug
            if (Physics2D.queriesStartInColliders)
            {
                Debug.LogWarning("Please disable 'queries start in colliders' in Physics2D settings for this controller to work properly. Sorry");
            }
        }

        private void Update()
        {
            TickTimers();
            CheckForStoppedWallCling();//do this before horizontal, which can reset it.
            ApplyDesired();
            ApplyGravity();
            ApplyJump();
            _movingPlatform = null; //set throughout collisions
            adoptedVel = Vector2.zero; //reset and add throughout collisions
            CastCeilingFTick();
            CastHorizontalFTick();
            CastGroundFTick();
            //move character
            
            var delta = (Vector3)(RealVelocity * Time.deltaTime);
            transform.position = transform.position + delta;
        }

        #region VelocityHelpers

        private void SetVelocity(Vector2 vel)
        {
            Velocity = vel;
        }

        private void SetVelocityRelative(Vector2 velChange)
        {
            Velocity = Velocity + velChange;
        }

        private void SetVerticalVelocity(float vy)
        {
            Velocity = new Vector2(Velocity.x, vy);
        }

        private void SetHorizontalVelocity(float vx)
        {
            Velocity = new Vector2(vx,Velocity.y);
        }

        private Vector2 AdoptedVelocity()
        {
            if(Grounded && wallClinging)
            {
                if (downCaster.MovingPlatform != null && (leftCaster.MovingPlatform != null || rightCaster.MovingPlatform != null))
                {
                    if (downCaster.MovingPlatform == leftCaster.MovingPlatform || downCaster.MovingPlatform == rightCaster.MovingPlatform)
                    {
                        return downCaster.MovingPlatform.GetVelocity();
                    }
                    else
                    {
                        Debug.LogWarning("Grounded and wall clinging on two moving platforms? This case is not explicitly handled.");
                    }
                }
            }
            
            if (Grounded)
            {
                return downCaster.MovingPlatform != null ? downCaster.MovingPlatform.GetVelocity() : Vector2.zero;
            }else if (!Grounded && wallClingingLeft)
            {
                return leftCaster.MovingPlatform?.GetVelocity() ?? Vector2.zero;
            }
            else if (!Grounded && wallClingingRight)
            {
                return leftCaster.MovingPlatform?.GetVelocity() ?? Vector2.zero;
            }

            
            //else
            return Vector2.zero;
        }
        
        #endregion

        public void Move(float horizontal)
        {
            
            _desiredVelocity = new Vector2(horizontal*Mathf.Max(maxHorizontalSpeed-CurrentGroundFriction),_desiredVelocity.y);
        }


        public void AddForce(Vector2 force)
        {
            SetVelocityRelative(force);
        }

        private void TickTimers()
        {
            if (Grounded)
            {
                timeSinceGrounded += Time.deltaTime;
                timeSinceLeftGround = 0;
            }
            else
            {
                timeSinceLeftGround += Time.deltaTime;
                timeSinceGrounded = 0;
            }

            if (wallClinging)
            {
                timeWallClinging += Time.deltaTime;
            }
            else
            {
                timeWallClinging = 0;
            }

               timeSinceLastJumped += Time.deltaTime;
                timeSincePressedJump += Time.deltaTime;
        }

        private void ApplyJump()
        {
            //press jump button, or pressed within jumpBuffer window
            //|| (Grounded && _pressingJump && timeSincePressedJump < jumpBuffer)
            if (tryJump)
            {
                if(canWallJump && !Grounded){
                    if (wallClingingLeft)
                    {
                        jumps = 1;//reset to 0, as if grounded, then +1
                        tryJump = false;
                        timeSinceLastJumped = 0;
                        Grounded = false;
                        //todo: Give the user control over this. One vector for fixed angle, one for modifier on jumpForce.
                        var jumpVector = Vector2.one.normalized*jumpForce*wallJumpForceModifier;//do the hard math for me. This can be calculated ahead of time. its like .707 or such, some trig.
                       SetVelocity(new Vector2(jumpVector.x, jumpVector.y));
                    }else if (wallClingingRight)
                    {
                        jumps = 1;//reset to 0, then +1
                        tryJump = false;
                        timeSinceLastJumped = 0;
                        Grounded = false;
                        var jumpVector = new Vector2(-1,1).normalized * jumpForce * wallJumpForceModifier;
                        SetVelocity(new Vector2(jumpVector.x, jumpVector.y));
                    }//else...
                }
                //on the ground, or in the air with jumps, or in the air within coyote time.
                //|| (!tryJump && !Grounded && _pressingJump && timeSinceLeftGround < coyoteTime)
                if (Grounded || jumps < airJumps + 1 )
                {
                    //jump tracking variables
                    jumps++;
                    tryJump = false;
                    timeSinceLastJumped = 0;
                    Grounded = false; //this will get reset in collisions, but lets be optimistic for jump resetting.

                    //jump
                    SetVerticalVelocity(jumpForce);
                }

            }
            

            if (Grounded)
            {
                jumps = 0;
                tryJump = false;
            }
        }

        void CheckForStoppedWallCling()
        {
            //Update WallClings
            if (_desiredVelocity.x <= 0)
            {
                wallClingingRight = false; //if moving left or not pushing right, we arent wall clinging.
            }
            else if (_desiredVelocity.x >= 0)
            {
                wallClingingLeft = false;
            }else if (Velocity.y >= 0)//on the ground or moving upwards, not clinging (not changing gravity).
            {
                wallClingingLeft = false;
                wallClingingRight = false;
            }
        }
        //Desired Horizontal
        private void ApplyDesired()
        {
            
            //Horizontal
            float acceleration;
            if (Grounded)
            {
                acceleration = _desiredVelocity.x == 0 ? maxHorizontalDeccelToZero : maxHorizontalAcceleration; 
            }
            else
            {
                acceleration = maxInAirHorizontalAcceleration;
            }
            
            float delta = acceleration * Time.deltaTime;
            
            //Todo: Factor in friction
            //we can get friction from downCaster.Friction, which will be recently set if grounded is true
            
            
            //Set horizontal component
            SetHorizontalVelocity(Mathf.MoveTowards(Velocity.x, _desiredVelocity.x, delta));

        }

        public void JumpPress()
        {
            timeSincePressedJump = 0;
            tryJump = true;
            _pressingJump = true;
        }

        public void JumpRelease()
        {
            _pressingJump = false;
            tryJump = false;//prevents coyote time if your press is faster than coyote time.
        }
        private void LateUpdate()
        {
            //After we calculate our new position, update our physics shape..
            UpdateBounds();
        }

        private void ApplyGravity()
        {
            SetVelocityRelative(GetGravity() * Time.deltaTime);
        }

        private Vector2 GetGravity()
        {
            float g =baseGravity;//default gravity scale
            if (Velocity.y > 0)
            {
                g *= upwardsGravityModifier;
                //
                if (_pressingJump && timeSinceLastJumped < holdJumpForTime)
                {
                    g *= jumpHeldGravityModifier;
                }
            }
            else if(Velocity.y < 0)
            {

                g *= downwardsGravityModifier;

                if (wallClinging)
                {
                    //Get wall friction.
                    float friction = wallClingingLeft ? leftCaster.Friction : rightCaster.Friction;
                    //when no friction, we don't change g. When 1 friction, we use wall cling modifier.
                    g *= Mathf.Lerp(1,wallClingingGravityModifier,friction);
                }
                else
                {
                    //I don't know what will be easier to edit, if the downwards is or isn't part of wallCling....
                    //IE: it's its own "gravity", or a percentage of downwards...
                    // g *= downwardsGravityModifier;
                }
            }
            return Vector2.up * g;//we write gravity as "-" because that makes sense. so "up" is positive 1 times the negative number, g
        }

        private void UpdateBounds()
        {
            movementShape.center = transform.position + (Vector3)boundsOffset;
        }

        #region Collisions

        

        void CastHorizontalFTick()
        {
            _cantGoFurtherRightThisFrame = false;
            _cantGoFurtherLeftThisFrame = false;
            var dir = Vector2.right;
            //todo: cache these
            var lipDown = Vector2.down * lipHeight;
            var lipUp = Vector2.up * lipHeight;
            
            //From top to bottom, so the last raycast is the Normal that we can grab for going up a slope.
            bool right = rightCaster.ArrayRaycast(dir, movementShape.TopRight() + -dir * skinWidth+lipDown, movementShape.BottomRight() + -dir * skinWidth + lipUp, Mathf.Max(RealVelocity.x,0) * Time.deltaTime + skinWidth);
            if (!right)
            {
                wallClingingRight = false;
            }
            else
            {
                _cantGoFurtherRightThisFrame = true;
            }
            
            if (right && (Velocity.x > 0 || RealVelocity.x > 0))
            {
                //Todo: check for lips with rightCaster.Results
                //Snap out from overlap to rest at hit point
                float leftPoint = rightCaster.ResultsPointMinX;

                //Shift left out of wall.
                transform.position = new Vector3(leftPoint - movementShape.extents.x, transform.position.y, transform.position.z);
                UpdateBounds();
                WallCling();
                SetHorizontalVelocity(0);
                var hitWallVel = rightCaster.MovingPlatform?.GetVelocity() ?? Vector2.zero;
                //the wall is moving towards us. It should push us, which it can do by moving us by its velocity.
                if (hitWallVel.x < 0){
                    adoptedVel = new Vector2(hitWallVel.x, adoptedVel.y); //we cant move horizontally, because of horizontal collision, but we can still adopt a moving platforms velocity.
                }
                else//the wall is moving away from us.
                {
                    if (wallClinging)
                    {
                        adoptedVel = new Vector2(hitWallVel.x, adoptedVel.y); //we cant move horizontally, because of horizontal collision, but we can still adopt a moving platforms velocity.

                    }
                    else
                    {
                        adoptedVel = new Vector2(0, adoptedVel.x);
                    }
                }

            //todo: set movingPlatform correctly
            }

            //We copy and paste the above and below, because we want to be able to jump up and have a moving platform get us from behind, so the code will need all directions.
            dir = Vector2.left;
            bool left = leftCaster.ArrayRaycast(dir, movementShape.TopLeft() + -dir * skinWidth + lipDown, movementShape.BottomLeft() + -dir * skinWidth + lipUp, Mathf.Min(RealVelocity.x,0)*Time.deltaTime + skinWidth);

            if (!left)
            {
                wallClingingLeft = false;
            }
            else
            {
                _cantGoFurtherLeftThisFrame = true;
            }
            
            if (left && (Velocity.x < 0 || RealVelocity.x < 0))
            {
                float rightPoint = leftCaster.ResultsPointMaxX;
                transform.position = new Vector3(rightPoint + movementShape.extents.x, transform.position.y, transform.position.z);
                UpdateBounds();
                WallCling();
                SetHorizontalVelocity(0);//Stop horizontal Movement
                var hitWallVel = leftCaster.MovingPlatform?.GetVelocity() ?? Vector2.zero;
                //Get pushed by wall.
                if (hitWallVel.x > 0)
                {
                    adoptedVel = new Vector2(hitWallVel.x, adoptedVel.y); 
                }
                else
                {
                    if (wallClinging)
                    {
                        adoptedVel = new Vector2(hitWallVel.x, adoptedVel.y);

                    }
                    else
                    {
                        adoptedVel = new Vector2(0, adoptedVel.x);
                    }
                }

            }
            
            //todo: Deal with slopes.
        }

        void CastGroundFTick()
        {
            //Todo: Calculate the from/to dependent on current desiredDirection/velocity. 
            //This will mean that the last, saved, MovingPlatform will be your 'front foot' for stepping onto movingPlatforms
            
            var dir = Vector2.down;
            var bottomLeft = movementShape.BottomLeft()+-dir*skinWidth + Vector2.right* verticalCastPadding;
            var bottomRight = movementShape.BottomRight()+ -dir * skinWidth - Vector2.right* verticalCastPadding;
            bool down = downCaster.ArrayRaycast(dir, bottomLeft, bottomRight, (Mathf.Max(RealVelocity.y,0) * Time.deltaTime) +skinWidth);
            
            if (down && Velocity.y < 0)
            {
                float overlapPercentage = Mathf.Abs(Vector2.Dot(downCaster.Normal, Vector2.up));//1 if aligned, 0 if orthogonal.
                float top = downCaster.ResultsPointMaxY;
                // top = top * overlapPercentage;//todo: this is broken, we want to do it to the delta, not the absolute position
                transform.position = new Vector3(transform.position.x, top+movementShape.extents.y, transform.position.z);
                UpdateBounds();

                //stop moving vertically.
                SetVerticalVelocity(0);
                //adopt velocity of platform.
                
                //Wait, we don't want to do this if we hit something horizontal.
                var groundAdopted = downCaster.MovingPlatform?.GetVelocity() ?? Vector2.zero;
                if (groundAdopted.x > 0 && !_cantGoFurtherRightThisFrame)
                {
                    adoptedVel = groundAdopted;

                }else if (groundAdopted.x < 0 && !_cantGoFurtherLeftThisFrame)
                {
                    adoptedVel = groundAdopted;
                }
                else
                {
                    //we don't have control over it moving horizontally anymore, its being pushed or pulled. or zero.
                    adoptedVel = new Vector2(adoptedVel.x, groundAdopted.y);
                }

                _movingPlatform = downCaster.MovingPlatform;
            }
            
            //if we have an upwards gravity, we are not grounded, even if collisions.
            //we might be on an upwards moving platform, but i haven't coded those yet. We will have some "platformVelocity" that we track and apply to ourselves when we are in contact.
            if (down && Velocity.y <= 0)
            {
                Grounded = true;
            }
        }

        void CastCeilingFTick()
        {
            var dir = Vector2.up;
            var topLeft = movementShape.TopLeft() + -dir * skinWidth + Vector2.right * verticalCastPadding;
            var topRight = movementShape.TopRight() + -dir * skinWidth - Vector2.right * verticalCastPadding;
            bool up = downCaster.ArrayRaycast(dir, topLeft, topRight, (Mathf.Max(0,RealVelocity.y) * Time.deltaTime + skinWidth));

            if (up && Velocity.y > 0)
            {
                float overlapPercentage = Mathf.Abs(Vector2.Dot(downCaster.Normal, Vector2.down)); //1 if aligned, 0 if orthogonal.
                //Snap out from overlap to rest at hit point
                if (overlapPercentage > 0)
                {
                    //counts?
                }
                float bottom = downCaster.ResultsPointMinY;
                //todo: implement overlap percentage.
                transform.position = new Vector3(transform.position.x, bottom - movementShape.extents.y, transform.position.z);
                UpdateBounds();//todo: wrap this stuff (for all 3 cast functions) in a Translate function

                //stop moving vertically
                SetVerticalVelocity(0);
            }
        }

        #endregion

        void WallCling()
        {
            //Do nothing! todo: something!
            SetHorizontalVelocity(0);

            
            if (_desiredVelocity.x < -Mathf.Epsilon)
            {
                wallClingingLeft = true;
                //wall cling left. 
            }else if (_desiredVelocity.x > Mathf.Epsilon)
            {
                wallClingingRight = true;
                //wall cling right.
            }
            else
            {
                //Still touching wall but not pushing against it (with desiredVel).
                wallClingingLeft = false;
                wallClingingRight = false;
            }
        }

        #region DebugStuff
        void OnDrawGizmosSelected()
        {
            var topLeft = new Vector3(movementShape.min.x, movementShape.max.y, transform.position.z);
            var topRight = new Vector3(movementShape.max.x, movementShape.max.y, transform.position.z);
            var botLeft = new Vector3(movementShape.min.x, movementShape.min.y, transform.position.z);
            var botRight = new Vector3(movementShape.max.x, movementShape.min.y, transform.position.z);
            Gizmos.color = Color.Lerp(Color.white,Color.yellow,0.5f);
            Gizmos.DrawLine(topLeft,topRight);
            Gizmos.DrawLine(topLeft, botLeft);
            Gizmos.DrawLine(topRight, botRight);
            Gizmos.DrawLine(botLeft, botRight);
        }

        #endregion

    }
}