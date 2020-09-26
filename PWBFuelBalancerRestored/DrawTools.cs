
/*
 * Code from this file used with license permission below. Fixes by DefiantZombie.
 */

/*
The MIT License(MIT)

Copyright(c) 2014 sarbian

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
using UnityEngine;


namespace PWBFuelBalancer
{
	public static class DrawTools
	{
		private static Material _material;
		private static int _glDepth;
		public static float NearPlane = 0f;


		private static Material DrawMaterial => _material ?? (_material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended")));


		public static void NewFrame()
		{
			_glDepth = 0;
		}


		private static void GLStart()
		{
			if (_glDepth == 0)
			{
				GL.PushMatrix();
				DrawMaterial.SetPass(0);
				GL.LoadPixelMatrix();
				GL.Begin(GL.LINES);
			}
			_glDepth++;
		}


		private static void GLEnd()
		{
			_glDepth--;

			if (_glDepth != 0) return;

			GL.End();
			GL.PopMatrix();
		}


		public static Camera GetActiveCam()
		{
			Camera cam;

			if (HighLogic.LoadedSceneIsEditor)
				cam = EditorLogic.fetch.editorCamera;
			else if (HighLogic.LoadedSceneIsFlight)
				cam = FlightCamera.fetch.mainCamera;
			else
				cam = Camera.main;

			return cam;
		}


		private static Vector3 Tangent(Vector3 normal)
		{
			var tangent = Vector3.Cross(normal, Vector3.right);

			if (tangent.sqrMagnitude <= 0.0001f)
				tangent = Vector3.Cross(normal, Vector3.up);

			return tangent;
		}


		private static void DrawLine(Vector3 origin, Vector3 destination, Color color)
		{
			var cam = GetActiveCam();

			var screenPoint1 = cam.WorldToScreenPoint(origin);
			var screenPoint2 = cam.WorldToScreenPoint(destination);

			GL.Color(color);
			GL.Vertex3(screenPoint1.x, screenPoint1.y, NearPlane);
			GL.Vertex3(screenPoint2.x, screenPoint2.y, NearPlane);
		}


		private static void DrawRay(Vector3 origin, Vector3 direction, Color color)
		{
			var cam = GetActiveCam();

			var screenPoint1 = cam.WorldToScreenPoint(origin);
			var screenPoint2 = cam.WorldToScreenPoint(origin + direction);

			GL.Color(color);
			GL.Vertex3(screenPoint1.x, screenPoint1.y, NearPlane);
			GL.Vertex3(screenPoint2.x, screenPoint2.y, NearPlane);
		}


		public static void DrawTransform(Transform t, float scale = 1.0f)
		{
			GLStart();

			DrawRay(t.position, t.up * scale, Color.green);
			DrawRay(t.position, t.right * scale, Color.red);
			DrawRay(t.position, t.forward * scale, Color.blue);

			GLEnd();
		}


		public static void DrawPoint(Vector3 position, Color color, float scale = 1.0f)
		{
			GLStart();
			GL.Color(color);

			DrawRay(position + Vector3.up * (scale * 0.5f), -Vector3.up * scale, color);
			DrawRay(position + Vector3.right * (scale * 0.5f), -Vector3.right * scale, color);
			DrawRay(position + Vector3.forward * (scale * 0.5f), -Vector3.forward * scale, color);

			GLEnd();
		}


		public static void DrawCircle(Vector3 position, Vector3 up, Color color, float radius = 1.0f)
		{
			const int segments = 36;
			const float step = Mathf.Deg2Rad * 360.0f / segments;

			var upNormal = up.normalized * radius;
			var forwardNormal = Tangent(upNormal).normalized * radius;
			var rightNormal = Vector3.Cross(upNormal, forwardNormal).normalized * radius;

			var matrix = new Matrix4x4
			{
				[0] = rightNormal.x,
				[1] = rightNormal.y,
				[2] = rightNormal.z,
				[4] = upNormal.x,
				[5] = upNormal.y,
				[6] = upNormal.z,
				[8] = forwardNormal.x,
				[9] = forwardNormal.y,
				[10] = forwardNormal.z
			};




			var lastPoint = position + matrix.MultiplyPoint3x4(Vector3.right);

			GLStart();
			GL.Color(color);

			for (var i = 0; i <= segments; i++)
			{
				Vector3 nextPoint;
				var angle = i * step;
				nextPoint.x = Mathf.Cos(angle);
				nextPoint.z = Mathf.Sin(angle);
				nextPoint.y = 0;

				nextPoint = position + matrix.MultiplyPoint3x4(nextPoint);

				DrawLine(lastPoint, nextPoint, color);

				lastPoint = nextPoint;
			}

			GLEnd();

		}


		public static void DrawCone(Vector3 position, Vector3 direction, Color color, float angle = 45.0f)
		{
			var length = direction.magnitude;

			var forward = direction;
			var up = Tangent(forward).normalized;
			var right = Vector3.Cross(forward, up).normalized;

			var radius = length * Mathf.Tan(Mathf.Deg2Rad * angle);

			GLStart();
			GL.Color(color);

			DrawRay(position, direction + radius * up, color);
			DrawRay(position, direction - radius * up, color);
			DrawRay(position, direction + radius * right, color);
			DrawRay(position, direction - radius * right, color);

			GLEnd();

			DrawCircle(position + forward, direction, color, radius);
			DrawCircle(position + forward * 0.5f, direction, color, radius * 0.5f);
		}


		public static void DrawArrow(Vector3 position, Vector3 direction, Color color)
		{
			GLStart();
			GL.Color(color);

			DrawRay(position, direction, color);

			GLEnd();

			DrawCone(position + direction, -direction * 0.333f, color, 15);
		}

#if false
		public static void DrawLocalMesh(Transform transform, Mesh mesh, Color color)
		{
			if (mesh?.triangles == null || mesh.vertices == null)
				return;

			var triangles = mesh.triangles;
			var vertices = mesh.vertices;

			GLStart();
			GL.Color(color);

			for (var i = 0; i < triangles.Length; i += 3)
			{
				var p1 = transform.TransformPoint(vertices[triangles[i]]);
				var p2 = transform.TransformPoint(vertices[triangles[i + 1]]);
				var p3 = transform.TransformPoint(vertices[triangles[i + 2]]);
				DrawLine(p1, p2, color);
				DrawLine(p2, p3, color);
				DrawLine(p3, p1, color);
			}

			GLEnd();
		}


		public static void DrawBounds(Bounds bounds, Color color)
		{
			var center = bounds.center;

			var x = bounds.extents.x;
			var y = bounds.extents.y;
			var z = bounds.extents.z;

			var topa = center + new Vector3(x, y, z);
			var topb = center + new Vector3(x, y, -z);
			var topc = center + new Vector3(-x, y, z);
			var topd = center + new Vector3(-x, y, -z);

			var bota = center + new Vector3(x, -y, z);
			var botb = center + new Vector3(x, -y, -z);
			var botc = center + new Vector3(-x, -y, z);
			var botd = center + new Vector3(-x, -y, -z);

			GLStart();
			GL.Color(color);

			// Top
			DrawLine(topa, topc, color);
			DrawLine(topa, topb, color);
			DrawLine(topc, topd, color);
			DrawLine(topb, topd, color);

			// Sides
			DrawLine(topa, bota, color);
			DrawLine(topb, botb, color);
			DrawLine(topc, botc, color);
			DrawLine(topd, botd, color);

			// Bottom
			DrawLine(bota, botc, color);
			DrawLine(bota, botb, color);
			DrawLine(botc, botd, color);
			DrawLine(botd, botb, color);

			GLEnd();
		}


		public static void DrawLocalCube(Transform transform, Vector3 size, Color color,
			Vector3 center = default(Vector3))
		{
			var topa = transform.TransformPoint(center + new Vector3(-size.x, size.y, -size.z) * 0.5f);
			var topb = transform.TransformPoint(center + new Vector3(size.x, size.y, -size.z) * 0.5f);

			var topc = transform.TransformPoint(center + new Vector3(size.x, size.y, size.z) * 0.5f);
			var topd = transform.TransformPoint(center + new Vector3(-size.x, size.y, size.z) * 0.5f);

			var bota = transform.TransformPoint(center + new Vector3(-size.x, -size.y, -size.z) * 0.5f);
			var botb = transform.TransformPoint(center + new Vector3(size.x, -size.y, -size.z) * 0.5f);

			var botc = transform.TransformPoint(center + new Vector3(size.x, -size.y, size.z) * 0.5f);
			var botd = transform.TransformPoint(center + new Vector3(-size.x, -size.y, size.z) * 0.5f);

			GLStart();
			GL.Color(color);

			// Top
			DrawLine(topa, topb, color);
			DrawLine(topb, topc, color);
			DrawLine(topc, topd, color);
			DrawLine(topd, topa, color);

			// Sides
			DrawLine(topa, bota, color);
			DrawLine(topb, botb, color);
			DrawLine(topc, botc, color);
			DrawLine(topd, botd, color);

			// Bottom
			DrawLine(bota, botb, color);
			DrawLine(botb, botc, color);
			DrawLine(botc, botd, color);
			DrawLine(botd, bota, color);

			GLEnd();
		}


		public static void DrawCapsule(Vector3 start, Vector3 end, Color color, float radius = 1.0f)
		{
			const int segments = 18;
			const float segmentsInv = 1.0f / segments;

			var up = (end - start).normalized * radius;
			var forward = Tangent(up).normalized * radius;
			var right = Vector3.Cross(up, forward).normalized * radius;

			var height = (start - end).magnitude;
			var sideLength = Mathf.Max(0, height * 0.5f - radius);
			var middle = (end + start) * 0.5f;

			start = middle + (start - middle).normalized * sideLength;
			end = middle + (end - middle).normalized * sideLength;

			// Radial circles
			DrawCircle(start, up, color, radius);
			DrawCircle(end, -up, color, radius);

			GLStart();
			GL.Color(color);

			// Side lines
			DrawLine(start + right, end + right, color);
			DrawLine(start - right, end - right, color);

			DrawLine(start + forward, end + forward, color);
			DrawLine(start - forward, end - forward, color);

			for (var i = 1; i <= segments; i++)
			{
				var stepFwd = i * segmentsInv;
				var stepBack = (i - 1) * segmentsInv;

				// Start endcap
				DrawLine(Vector3.Slerp(right, -up, stepFwd) + start, Vector3.Slerp(right, -up, stepBack) + start, color);
				DrawLine(Vector3.Slerp(-right, -up, stepFwd) + start, Vector3.Slerp(-right, -up, stepBack) + start, color);
				DrawLine(Vector3.Slerp(forward, -up, stepFwd) + start, Vector3.Slerp(forward, -up, stepBack) + start, color);
				DrawLine(Vector3.Slerp(-forward, -up, stepFwd) + start, Vector3.Slerp(-forward, -up, stepBack) + start, color);

				// End endcap
				DrawLine(Vector3.Slerp(right, up, stepFwd) + end, Vector3.Slerp(right, up, stepBack) + end, color);
				DrawLine(Vector3.Slerp(-right, up, stepFwd) + end, Vector3.Slerp(-right, up, stepBack) + end, color);
				DrawLine(Vector3.Slerp(forward, up, stepFwd) + end, Vector3.Slerp(forward, up, stepBack) + end, color);
				DrawLine(Vector3.Slerp(-forward, up, stepFwd) + end, Vector3.Slerp(-forward, up, stepBack) + end, color);
			}

			GLEnd();
		}


		public static void DrawSphere(Vector3 position, Color color, float radius = 1.0f)
		{
			const int segments = 36;
			const float step = Mathf.Deg2Rad * 360.0f / segments;

			var x = new Vector3(position.x, position.y, position.z + radius);
			var y = new Vector3(position.x + radius, position.y, position.z);
			var z = new Vector3(position.x + radius, position.y, position.z);

			GLStart();
			GL.Color(color);

			for (var i = 1; i <= segments; i++)
			{
				var angle = step * i;

				var nextX = new Vector3(position.x, position.y + radius * Mathf.Sin(angle), position.z + radius * Mathf.Cos(angle));
				var nextY = new Vector3(position.x + radius * Mathf.Cos(angle), position.y,
					position.z + radius * Mathf.Sin(angle));
				var nextZ = new Vector3(position.x + radius * Mathf.Cos(angle), position.y + radius * Mathf.Sin(angle),
					position.z);

				DrawLine(x, nextX, color);
				DrawLine(y, nextY, color);
				DrawLine(z, nextZ, color);

				x = nextX;
				y = nextY;
				z = nextZ;
			}

			GLEnd();
		}


		public static void DrawCylinder(Vector3 start, Vector3 end, Color color, float radius = 1.0f)
		{
			var up = (end - start).normalized * radius;
			var forward = Tangent(up);
			var right = Vector3.Cross(up, forward).normalized * radius;

			// Radial circles
			DrawCircle(start, up, color, radius);
			DrawCircle(end, -up, color, radius);
			DrawCircle((start + end) * 0.5f, up, color, radius);

			GLStart();
			GL.Color(color);

			// Sides
			DrawLine(start + right, end + right, color);
			DrawLine(start - right, end - right, color);

			DrawLine(start + forward, end + forward, color);
			DrawLine(start - forward, end - forward, color);

			// Top
			DrawLine(start - right, start + right, color);
			DrawLine(start - forward, start + forward, color);

			// Bottom
			DrawLine(end - right, end + right, color);
			DrawLine(end - forward, end + forward, color);

			GLEnd();
		}
#endif
	}
}
