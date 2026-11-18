using UnityEngine;

namespace KingdomTD.Flipbook
{
    internal static class FlipbookQuadMesh
    {
        private static Mesh _sharedMesh;
        private static Mesh _sharedShadowMesh;

        public static Mesh Get(bool withGroundShadow = false)
        {
            if (withGroundShadow)
            {
                if (_sharedShadowMesh == null)
                {
                    _sharedShadowMesh = Create(true);
                }

                return _sharedShadowMesh;
            }

            if (_sharedMesh == null)
            {
                _sharedMesh = Create(false);
            }

            return _sharedMesh;
        }

        private static Mesh Create(bool withGroundShadow)
        {
            Vector2[] corners = { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            int count = withGroundShadow ? 8 : 4;
            Vector3[] positions = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                Vector2 corner = corners[i % 4];
                positions[i] = new Vector3(corner.x - 0.5f, corner.y - 0.5f, 0f);
            }

            Mesh mesh = new Mesh
            {
                name = withGroundShadow ? "Flipbook Quad + Ground Shadow" : "Flipbook Quad",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = positions
            };
            if (withGroundShadow)
            {
                // UV.z identifies just the four vertices projected onto the ground.
                Vector3[] uvs = new Vector3[count];
                for (int i = 0; i < count; i++)
                {
                    Vector2 corner = corners[i % 4];
                    uvs[i] = new Vector3(corner.x, corner.y, i < 4 ? 0f : 1f);
                }

                mesh.SetUVs(0, uvs);
                mesh.triangles = new[] { 0, 1, 2, 2, 3, 0, 4, 5, 6, 6, 7, 4 };
            }
            else
            {
                mesh.uv = corners;
                mesh.triangles = new[] { 0, 1, 2, 2, 3, 0 };
            }

            return mesh;
        }
    }
}
