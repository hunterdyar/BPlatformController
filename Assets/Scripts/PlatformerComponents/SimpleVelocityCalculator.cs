using System;
using UnityEngine;

namespace BloopsPlatform.PlatformerComponents
{
	public class SimpleVelocityCalculator : MonoBehaviour, IMovingPlatform
	{
		private Vector3 _previousPosition;
		private Vector2 _velocity;
		private void Update()
		{
			_velocity = (transform.position - _previousPosition) / Time.deltaTime;
			
			//
			_previousPosition = transform.position;
		}

		public Vector2 GetVelocity()
		{
			return _velocity;
		}
	}
}