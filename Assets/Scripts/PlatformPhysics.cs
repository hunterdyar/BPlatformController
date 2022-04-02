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

        [Header("Coyote Time")] [SerializeField]
        private float coyoteTime;

        [SerializeField] private float jumpBuffer;
        [Header("Running Movement Settings")]
        [SerializeField] private float maxHorizontalSpeed;
        [SerializeField] private float maxHorizontalAcceleration = 35;
        [SerializeField] private float maxHorizontalDeccelToZero = 100;
        [SerializeField] private float maxInAirHorizontalAcceleration = 20;
        private Caster downCaster;
        
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
            timeSinceGrounded = 0;
            timeSinceLastJumped = 0;
            timeSinceLeftGround = 0;
            _pressingJump = false;
            tryJump = false;
            boundsOffset = movementShape.center;
        }

        void Start()
        {
            downCaster = new Caster(layerMask, 10);
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
        private void Update()
        {
            TickTimers();
            ApplyDesired();
            ApplyGravity();
            ApplyJump();
            CastGroundFTick();
            //move character
            transform.position = transform.position + ((Vector3)Velocity * movementDelta);
            

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

            timeSinceLastJumped += Time.deltaTime;
            timeSincePressedJump += Time.deltaTime;
        }

        private void ApplyJump()
        {
            //press jump button, or pressed within jumpBuffer window
            //|| (Grounded && _pressingJump && timeSincePressedJump < jumpBuffer)
            if (tryJump )
            {
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
            UpdateBoundsTick();
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
            }
            return Vector2.up * g;//we write gravity as "-" because that makes sense. so "up" is positive 1 times the negative number, g
        }

        private void UpdateBoundsTick()
        {
            movementShape.center = transform.position + (Vector3)boundsOffset;
        }

        void CastGroundFTick()
        {
            var dir = Vector2.down;
            bool down = downCaster.ArrayRaycast(dir, movementShape.BottomLeft()+-dir*skinWidth, movementShape.BottomRight()+ -dir * skinWidth, movementFixedDelta+skinWidth);
            
            if (down && Velocity.y < 0)
            {
                //Snap to hit point
                float top = downCaster.ResultsPointMaxY;
                float difference = top - transform.position.y;
                transform.position = new Vector3(transform.position.x, top+movementShape.extents.y, transform.position.z);
                
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