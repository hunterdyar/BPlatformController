using UnityEngine;

namespace BloopsPlatform
{
	public class PlatformerInput : MonoBehaviour
	{
		private PlatformPhysics _physics;

		private void Awake()
		{
			_physics = GetComponent<PlatformPhysics>();
		}

		public void Update()
		{
			_physics.Move(Input.GetAxisRaw("Horizontal"));
			if (Input.GetButtonDown("Jump"))
			{
				_physics.JumpPress();
			}

			if (Input.GetButtonUp("Jump"))
			{
				_physics.JumpRelease();
			}
		}
	}
}