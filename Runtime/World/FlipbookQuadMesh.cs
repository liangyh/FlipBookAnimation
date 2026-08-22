using UnityEngine;

namespace KingdomTD.Flipbook
{
    internal static class FlipbookQuadMesh
    {
        private static Mesh _sharedMesh;

        public static Mesh Get()
        {
            if (_sharedMesh != null)
            {
                return _sharedMesh;
            }

            _sharedMesh = new Mesh
            {
                name = "Flipbook Quad",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f),
                    new Vector3(0.5f, -0.5f, 0f)
                },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(1f, 0f)
                },
                triangles = new[] { 0, 1, 2, 2, 3, 0 }
            };
            _sharedMesh.RecalculateBounds();
            return _sharedMesh;
        }
    }
}
