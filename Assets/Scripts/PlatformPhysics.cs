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
        private float movementDelta => Velocity.magnitude * Time.deltaTime;
        private float movementFixedDelta => Velocity.magnitude * Time.fixedDeltaTime;

        private void Awake()
        {
            if (lipHeight == 0)
            {
                //Cant actually be 0!
                lipHeight = Mathf.Epsilon;
            }
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
            CastCeilingFTick();
            CastHorizontalFTick();
            CastGroundFTick();
            //move character
            transform.position = transform.position + ((Vector3)Velocity * movementDelta);
        }

        public void SetVelocity(Vector2 vel)
        {
            _desiredVelocity = vel;
        }

        public void Move(float horizontal)
        {
            _desiredVelocity = new Vector2(horizontal*maxHorizontalSpeed,_desiredVelocity.y);
        }

        public void AddForce(Vector2 force)
        {
            Velocity = Velocity + force;
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

            if (wallClingingLeft || wallClingingRight)
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
                if (wallClingingLeft)
                {
                    jumps = 1;//reset to 0, as if grounded, then +1
                    tryJump = false;
                    timeSinceLastJumped = 0;
                    Grounded = false;
                    var jumpVector = Vector2.one.normalized*jumpForce;//do the hard math for me. This can be calculated ahead of time. its like .707 or such, some trig.
                    Velocity = new Vector2(jumpVector.x, jumpVector.y);
                }else if (wallClingingRight)
                {
                    jumps = 1;//reset to 0, then +1
                    tryJump = false;
                    timeSinceLastJumped = 0;
                    Grounded = false;
                    var jumpVector = new Vector2(-1,1).normalized * jumpForce;
                    Velocity = new Vector2(jumpVector.x, jumpVector.y);
                }//else...
                
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
                    Velocity = new Vector2(Velocity.x, jumpForce);
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
            
            //Set horizontal component
            Velocity= new Vector2(Mathf.MoveTowards(Velocity.x, _desiredVelocity.x, delta),Velocity.y);

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
            Velocity = Velocity + (GetGravity() * Time.deltaTime);
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

                if (wallClingingLeft || wallClingingRight)
                {
                    g *= wallClingingGravityModifier;
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

        void CastHorizontalFTick()
        {
            var dir = Vector2.right;
            //todo: cache these
            var lipDown = Vector2.down * lipHeight;
            var lipUp = Vector2.up * lipHeight;
            
            //From top to bottom, so the last raycast is the Normal that we can grab for going up a slope.
            bool right = rightCaster.ArrayRaycast(dir, movementShape.TopRight() + -dir * skinWidth+lipDown, movementShape.BottomRight() + -dir * skinWidth + lipUp, movementFixedDelta + skinWidth);

            if (!right)
            {
                wallClingingRight = false;
            }
            
            
            if (right && Velocity.x > 0)
            {
                //Todo: check for lips with rightCaster.Results
                //Snap out from overlap to rest at hit point
                float leftPoint = rightCaster.ResultsPointMinX;
                
                //Shift left.
                transform.position = new Vector3(leftPoint - movementShape.extents.x, transform.position.y,transform.position.z);
                UpdateBounds();
                //stop moving vertically.
                WallCling();
            }
            //We copy and paste the above and below, because we want to be able to jump up and have a moving platform get us from behind, so the code will need all directions.
            dir = Vector2.left;
            bool left = leftCaster.ArrayRaycast(dir, movementShape.TopLeft() + -dir * skinWidth + lipDown, movementShape.BottomLeft() + -dir * skinWidth + lipUp, movementFixedDelta + skinWidth);

            if (!left)
            {
                wallClingingLeft = false;
            }
            
            if (left && Velocity.x < 0)
            {
                //Todo: check for lips with rightCaster.Results
                //Snap out from overlap to rest at hit point
                float rightPoint = leftCaster.ResultsPointMaxX;

                //Shift left.
                transform.position = new Vector3(rightPoint + movementShape.extents.x, transform.position.y, transform.position.z);
                UpdateBounds();
                //stop moving vertically. WallCling.
                WallCling();
            }
            
            //todo: Deal with slopes.
        }

        void CastGroundFTick()
        {
            var dir = Vector2.down;
            var bottomLeft = movementShape.BottomLeft()+-dir*skinWidth + Vector2.right* verticalCastPadding;
            var bottomRight = movementShape.BottomRight()+ -dir * skinWidth - Vector2.right* verticalCastPadding;
            bool down = downCaster.ArrayRaycast(dir, bottomLeft, bottomRight, movementFixedDelta+skinWidth);
            
            if (down && Velocity.y < 0)
            {
                float overlapPercentage = Mathf.Abs(Vector2.Dot(downCaster.Normal, Vector2.up));//1 if aligned, 0 if orthogonal.
                //Snap out from overlap to rest at hit point
                float top = downCaster.ResultsPointMaxY;
                // top = top * overlapPercentage;//todo: this is broken, we want to do it to the delta, not the absolute position
                transform.position = new Vector3(transform.position.x, top+movementShape.extents.y, transform.position.z);
                UpdateBounds();

                //stop moving vertically.
                Velocity = new Vector2(Velocity.x, 0);
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
            bool up = downCaster.ArrayRaycast(dir, topLeft, topRight, movementFixedDelta + skinWidth);

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
                Velocity = new Vector2(Velocity.x, 0);
                
            }
        }

        void WallCling()
        {
            //Do nothing! todo: something!
            Velocity = new Vector2(0, Velocity.y);

            
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
    }
}