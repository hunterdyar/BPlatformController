using UnityEngine;

namespace BloopsPlatform
{
	public static class Extensions
	{
		public static Vector2 BottomLeft(this Bounds bounds)
		{
			return bounds.min;
		}

		public static Vector2 TopRight(this Bounds bounds)
		{
			return bounds.max;
		}

		public static Vector2 BottomRight(this Bounds bounds)
		{
			return new Vector2(bounds.max.x,bounds.min.y);
		}

		public static Vector2 TopLeft(this Bounds bounds)
		{
			return new Vector2(bounds.min.x, bounds.max.y);
		}
	}
}