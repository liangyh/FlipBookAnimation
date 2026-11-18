using UnityEngine;

namespace KingdomTD.Flipbook
{
    internal struct FlipbookGroundShadowBounds
    {
        // Keep a thin slab around the plane when Unity converts min/max to center/extents.
        private const float FootprintPadding = 0.001f;
        private Matrix4x4 _lastMatrix;
        private float _lastPlaneY;
        private float _lastLength;
        private float _lastFootHeight;
        private bool _usesWorldBounds;
        private Vector3 _lastHorizontalPosition;

        public void Update(MeshRenderer renderer, float footHeight, float planeY, float length, bool force)
        {
            Matrix4x4 matrix = renderer.localToWorldMatrix;
            Vector3 horizontalPosition = new Vector3(matrix.m03, 0f, matrix.m23);
            // Horizontal translation moves the body and its entire footprint together.
            matrix.m03 = 0f;
            matrix.m23 = 0f;
            if (!force && matrix.Equals(_lastMatrix) && planeY == _lastPlaneY &&
                length == _lastLength && footHeight == _lastFootHeight &&
                (!_usesWorldBounds || horizontalPosition.Equals(_lastHorizontalPosition)))
            {
                return;
            }

            _lastMatrix = matrix;
            _lastPlaneY = planeY;
            _lastLength = length;
            _lastFootHeight = footHeight;
            Vector3 foot = new Vector3(0f, footHeight - 0.5f, 0f);
            Vector3 worldFoot = matrix.MultiplyPoint3x4(foot);
            worldFoot.y = planeY;
            float halfWidth = matrix.MultiplyVector(Vector3.right).magnitude * 0.5f;
            float height = matrix.MultiplyVector(Vector3.up).magnitude;
            float radius = halfWidth + height * length * Mathf.Max(footHeight, 1f - footHeight) + FootprintPadding;

            // Enclose every camera direction, including perspective views from the edge of the screen.
            Matrix4x4 inverse = default;
            if (!Matrix4x4.Inverse3DAffine(matrix, ref inverse))
            {
                // A sprite with zero Z scale can still cast a full footprint outside its local XY plane.
                Vector3 bodySize = new Vector3(Mathf.Abs(matrix.m00) + Mathf.Abs(matrix.m01),
                    Mathf.Abs(matrix.m10) + Mathf.Abs(matrix.m11), Mathf.Abs(matrix.m20) + Mathf.Abs(matrix.m21));
                Bounds worldBounds = new Bounds(horizontalPosition + Vector3.up * matrix.m13, bodySize);
                Vector3 reach = new Vector3(radius, FootprintPadding, radius);
                worldBounds.Encapsulate(horizontalPosition + worldFoot - reach);
                worldBounds.Encapsulate(horizontalPosition + worldFoot + reach);
                renderer.bounds = worldBounds;
                _usesWorldBounds = true;
                _lastHorizontalPosition = horizontalPosition;
                return;
            }

            if (_usesWorldBounds)
            {
                renderer.ResetBounds();
                _usesWorldBounds = false;
            }
            Vector3 center = inverse.MultiplyPoint3x4(worldFoot);
            Vector3 extent = new Vector3(Mathf.Abs(inverse.m00) + Mathf.Abs(inverse.m02),
                Mathf.Abs(inverse.m10) + Mathf.Abs(inverse.m12),
                Mathf.Abs(inverse.m20) + Mathf.Abs(inverse.m22)) * radius;
            extent += new Vector3(Mathf.Abs(inverse.m01), Mathf.Abs(inverse.m11), Mathf.Abs(inverse.m21)) *
                      FootprintPadding;
            Bounds bounds = new Bounds(Vector3.zero, new Vector3(1f, 1f, 0f));
            bounds.Encapsulate(center - extent);
            bounds.Encapsulate(center + extent);
            renderer.localBounds = bounds;
        }

        public void Reset(MeshRenderer renderer)
        {
            if (_usesWorldBounds)
            {
                renderer.ResetBounds();
                _usesWorldBounds = false;
            }
            renderer.ResetLocalBounds();
        }
    }
}
