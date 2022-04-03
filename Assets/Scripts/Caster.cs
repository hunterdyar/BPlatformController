#define DEBUG_DRAWCASTLINES
using System.Linq;
using UnityEngine;


namespace BloopsPlatform
{
	//Wrap up our various physics casts and things into an object.
	
	//This makes the method signatures cleaner because we can "cache" physics settings in the Caster object.
	
	//Some might find this kind of utility wrapper needless and annoying... and it probably is for most projects
	//but after working on Shadowbright, where I did a lot of custom fenangling of physics to get it to behave nicely, 
	//I just want all the dang raycasts to be in the same place
	public class Caster
	{
		private readonly int _defaultNumberCasts;
		private readonly LayerMask _layerMask;
		public RaycastHit2D[] Results => _results;
		readonly RaycastHit2D[] _results;
		private readonly Vector2[] _normals;
		public Vector2 Normal { get; private set; }
		public float ResultsPointMaxY { get; private set; }
		public float ResultsPointMinY { get; private set; }
		public float ResultsPointMaxX { get; private set; }
		public float ResultsPointMinX { get; private set; }
		public bool ResultsSinceReset { get; private set; }
		public Caster(LayerMask layerMask, int defaultNumberCasts)
		{
			ResultsSinceReset = false;
			_layerMask = layerMask;
			_defaultNumberCasts = defaultNumberCasts;
			_results = new RaycastHit2D[defaultNumberCasts];
			_normals = new Vector2[defaultNumberCasts];
		}
		
		//
		/// <summary>
		/// A series of raycasts in direction along a given line (defined by fromEdge and toEdge). Will complete all raycasts regardless of hits.
		/// Results are stored in the casters Results variable.
		/// </summary>
		/// <param name="direction">Raycast Direction</param>
		/// <param name="edgeFrom">Starting point of line along which to originate casts.</param>
		/// <param name="edgeTo">Ending point of line along which to originate casts.</param>
		/// <param name="distance">Raycast Distance.</param>
		/// <returns></returns>
		public bool ArrayRaycast(Vector2 direction, Vector2 edgeFrom, Vector2 edgeTo, float distance)
		{
			ResetResultData();
			float edgeLength = Vector2.Distance(edgeFrom, edgeTo);
			float separation = edgeLength / (float)(_defaultNumberCasts-1);
			bool hitAnything = false;
			
			for (int i = 0; i < _defaultNumberCasts; i++)
			{
				Vector2 origin = Vector2.Lerp(edgeFrom, edgeTo, i * separation);
				this._results[i] = Physics2D.Raycast(origin, direction, distance, _layerMask);

				if (_results[i].collider != null)
				{
					_normals[i] = _results[i].normal;
					Normal = _normals[i];//i cant think of a good way to do average of only some of the rays collide, so lets just hope that the last array (the TO) has the right normal? hmmm
					hitAnything = true;
					CompareMinMax(_results[i].point);
				}

#if DEBUG_DRAWCASTLINES
				Color debugColor = this._results[i].collider == null ? Color.yellow : Color.red;
				Debug.DrawLine(origin,origin+direction*distance,debugColor,Time.deltaTime);
#endif
			}

			ResultsSinceReset = hitAnything;
			return hitAnything;
		}

		private void CompareMinMax(Vector2 point)
		{
			if (point.x > ResultsPointMaxX)
			{
				ResultsPointMaxX = point.x;
			}

			if (point.x < ResultsPointMinX)
			{
				ResultsPointMinX = point.x;
			}

			if (point.y > ResultsPointMaxY)
			{
				ResultsPointMaxY = point.y;
			}

			if (point.y < ResultsPointMinY)
			{
				ResultsPointMinY = point.y;
			}
		}


		void ResetResultData()
		{
			_normals.Initialize();//sets all to 0... ? calls a parameterless constructor on each element of a value-type array.
			ResultsSinceReset = false;
			ResultsPointMaxY = Mathf.NegativeInfinity;
			ResultsPointMinY = Mathf.Infinity;
			ResultsPointMaxX = Mathf.NegativeInfinity;
			ResultsPointMinX = Mathf.Infinity;
		}
	}
}