using System;
using System.Collections;
using UnityEngine;
using BloopsPlatform;
//just a basic example of moving a platform (implementing IMovingPlatform)
public class MovingPlatform : MonoBehaviour
{
	[SerializeField] Vector2 moveOffset;
	[SerializeField] float moveSpeed;
	//
	private Vector2 _start;
	private Vector2 _end;
	private float timeToMove;
	private void Awake()
	{
		_start = transform.position;
		_end = transform.position + (Vector3)moveOffset;
	}

	IEnumerator Start()
	{
		//loop the coroutine forever. Disabling the gameobject breaks the movement, but doesnt bork everything else.
		while (gameObject.activeSelf)
		{
			yield return StartCoroutine(MoveTo(_end));
			yield return StartCoroutine(MoveTo(_start));
		}
	}
	

	public IEnumerator MoveTo(Vector2 end)
	{
		Vector2 start = transform.position;
		timeToMove = Vector3.Distance(start,end) / moveSpeed;
		float t = 0;
		//move there
		while (t < 1)
		{
			transform.position = Vector3.Lerp(start, end, t);
			
			t += Time.deltaTime/timeToMove;
			yield return null;
		}

		transform.position = end;
	}
}
