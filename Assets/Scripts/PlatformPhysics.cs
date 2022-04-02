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
        [SerializeField] private int maxJumps;
        [SerializeField] private float jumpForce;
        //gravity holding timer...
        [SerializeField] private float jumpHeldGravityModifier;
        private int jumps = 0;
        private bool tryJump;
        [Header("Running Movement Settings")]
        [SerializeField] private float maxHorizontalSpeed;
        [SerializeField] private float maxHorizontalAcceleration = 35;
        [SerializeField] private float maxHorizontalDeccelToZero = 100;
        [SerializeField] private float maxInAirHorizontalAcceleration = 20;
        private Caster downCaster;
        //
        public bool Grounded;
        public Vector2 Velocity;
        private Vector2 _desiredVelocity;
        private float movementDelta => Velocity.magnitude * Time.deltaTime;
        private float movementFixedDelta => Velocity.magnitude * Time.fixedDeltaTime;

        private void Awake()
        {
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
            ApplyDesired();
            ApplyGravity();
            ApplyJump();
            CastGroundFTick();
            //move character
            transform.position = transform.position + ((Vector3)Velocity * movementDelta);
            
            //reset jumps
            if (Grounded)
            {
                jumps = 0;
            }
        }

        private void ApplyJump()
        {
            if (tryJump && jumps < maxJumps)
            {
                //
                jumps++;
                tryJump = false;
                //jump
                Velocity.y = jumpForce;
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
            Velocity.x = Mathf.MoveTowards(Velocity.x, _desiredVelocity.x, delta);
            // //Vertical
            // Velocity.y = _desiredVelocity.y;
        }

        public void Jump()
        {
            tryJump = true;
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
            Grounded = downCaster.ArrayRaycast(dir, movementShape.BottomLeft()+-dir*skinWidth, movementShape.BottomRight()+ -dir * skinWidth, movementFixedDelta+skinWidth);
            
            if (Grounded && Velocity.y < 0)
            {
                //Snap to hit point
                float top = downCaster.ResultsPointMaxY;
                float difference = top - transform.position.y;
                transform.position = new Vector3(transform.position.x, top+movementShape.extents.y, transform.position.z);
                
                //stop moving vertically.
                Velocity = new Vector2(Velocity.x, 0);
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