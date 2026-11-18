using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.SceneManagement;
#endif

namespace KingdomTD.Flipbook
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("KingdomTD/Flipbook Ground Shadow Settings")]
    public sealed class FlipbookGroundShadowSettings : MonoBehaviour
    {
        private const float DefaultAngle = 45f;
        private const float DefaultLength = 0.7f;
        private const float SurfaceBias = 0.001f;
        private const float DefaultDirectionComponent = 0.70710678f;
        private static readonly Color DefaultColor = new Color(0.18f, 0.17f, 0.2f, 1f);
        private static readonly List<FlipbookGroundShadowSettings> Settings = new List<FlipbookGroundShadowSettings>();
#if UNITY_EDITOR
        internal static Func<Scene, bool> IsPreviewScene { get; set; }
#endif

        [ColorUsage(false)] [SerializeField] private Color _shadowColor = DefaultColor;
        [Tooltip("屏幕空间角度，0° 向右，90° 向上。")]
        [Range(0f, 360f)] [SerializeField] private float _screenAngle = DefaultAngle;
        [Tooltip("图中一个角色高度对应的地面投影长度。")]
        [Min(0f)] [SerializeField] private float _length = DefaultLength;
        [Tooltip("水平地面的世界 Y 坐标。")]
        [SerializeField] private float _groundHeight;

        private float _cachedScreenAngle = DefaultAngle;
        private Vector2 _screenDirection = new Vector2(DefaultDirectionComponent, DefaultDirectionComponent);

        public Color ShadowColor { get => _shadowColor; set => _shadowColor = value; }
        public float ScreenAngle { get => _screenAngle; set => _screenAngle = Mathf.Repeat(value, 360f); }
        public float Length { get => _length; set => _length = Mathf.Max(0f, value); }
        public float GroundHeight { get => _groundHeight; set => _groundHeight = value; }

        private void OnEnable()
        {
            Settings.Add(this);
        }

        private void OnDisable()
        {
            Settings.Remove(this);
        }

        internal static void GetProjection(GameObject target, out Vector4 projection, out Color color)
        {
            FlipbookGroundShadowSettings settings = FindActiveSettings(target);
            if (settings is null)
            {
                projection = new Vector4(SurfaceBias, DefaultLength, DefaultDirectionComponent, DefaultDirectionComponent);
                color = DefaultColor;
                return;
            }

            if (settings._cachedScreenAngle != settings._screenAngle)
            {
                settings._cachedScreenAngle = settings._screenAngle;
                float angle = Mathf.Repeat(settings._screenAngle, 360f);
                float radians = angle * Mathf.Deg2Rad;
                float vertical = angle == 0f || angle == 180f ? 0f : Mathf.Sin(radians);
                settings._screenDirection = new Vector2(Mathf.Cos(radians), vertical);
            }

            Vector2 direction = settings._screenDirection;
            projection = new Vector4(settings._groundHeight + SurfaceBias, Mathf.Max(0f, settings._length),
                direction.x, direction.y);
            color = settings._shadowColor;
        }

        private static FlipbookGroundShadowSettings FindActiveSettings(GameObject target)
        {
            if (Settings.Count == 0)
            {
                return null;
            }

#if UNITY_EDITOR
            if (IsPreviewScene != null)
            {
                Scene scene = target.scene;
                bool inPreviewScene = IsPreviewScene(scene);
                for (int i = Settings.Count - 1; i >= 0; i--)
                {
                    FlipbookGroundShadowSettings settings = Settings[i];
                    Scene settingsScene = settings.gameObject.scene;
                    if (settingsScene == scene || (!inPreviewScene && !IsPreviewScene(settingsScene)))
                    {
                        return settings;
                    }
                }

                return null;
            }
#endif
            return Settings[Settings.Count - 1];
        }
    }
}
