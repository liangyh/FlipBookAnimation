using System.Collections.Generic;
using UnityEngine;

namespace KingdomTD.Flipbook
{
    [CreateAssetMenu(fileName = "NewFlipbookAnimation", menuName = "KingdomTD/Flipbook Animation")]
    public sealed class FlipbookAnimationAsset : ScriptableObject
    {
        private static readonly int MainTextureId = Shader.PropertyToID("_MainTex");
        private static readonly int EffectTextureId = Shader.PropertyToID("_EffectTex");
        private static readonly int ColumnsId = Shader.PropertyToID("_Columns");
        private static readonly int RowsId = Shader.PropertyToID("_Rows");

        [SerializeField] private Texture2D _mainTexture;
        [SerializeField] private Texture2D _effectTexture;
        [SerializeField] private Material _worldMaterial;
        [Min(1)] [SerializeField] private int _columns = 1;
        [Min(1)] [SerializeField] private int _rows = 1;
        [SerializeField] private string _defaultAnimationName = "Idle";
        [SerializeField] private List<FlipbookClipData> _clips = new List<FlipbookClipData>();
        [Tooltip("为空表示单方向。角度为屏幕空间：0 = 右，90 = 上（远离相机），逆时针。")]
        [SerializeField] private List<FlipbookDirectionData> _directions = new List<FlipbookDirectionData>();

        private Dictionary<string, FlipbookClipData> _clipByName;

        public Texture2D MainTexture => _mainTexture;
        public Texture2D EffectTexture => _effectTexture;
        public Material WorldMaterial => _worldMaterial;
        public int Columns => Mathf.Max(1, _columns);
        public int Rows => Mathf.Max(1, _rows);
        public int TotalFrameCount => Columns * Rows;
        public string DefaultAnimationName => ResolveDefaultAnimationName();
        public IReadOnlyList<FlipbookClipData> Clips => _clips;
        public IReadOnlyList<FlipbookDirectionData> Directions => _directions;
        public int DirectionCount => Mathf.Max(1, _directions.Count);
        public int AuthoredDirectionCount => FlipbookDirectionResolver.GetAuthoredDirectionCount(_directions);
        public Vector2 FramePixelSize => _mainTexture == null
            ? Vector2.zero
            : new Vector2((float)_mainTexture.width / Columns, (float)_mainTexture.height / Rows);

        public bool TryGetClip(string animationName, out FlipbookClipData clip)
        {
            BuildClipCache();
            return _clipByName.TryGetValue(animationName, out clip);
        }

        public Rect GetFrameUvRect(int frame)
        {
            int clampedFrame = Mathf.Clamp(frame, 0, TotalFrameCount - 1);
            int row = clampedFrame / Columns;
            int column = clampedFrame - row * Columns;
            float width = 1f / Columns;
            float height = 1f / Rows;
            return new Rect(column * width, (Rows - 1 - row) * height, width, height);
        }

        public bool SynchronizeWorldMaterial()
        {
            if (_worldMaterial == null)
            {
                return false;
            }

            bool changed = SynchronizeTexture(_worldMaterial, MainTextureId, _mainTexture);
            changed |= SynchronizeTexture(_worldMaterial, EffectTextureId, _effectTexture);
            changed |= SynchronizeFloat(_worldMaterial, ColumnsId, Columns);
            changed |= SynchronizeFloat(_worldMaterial, RowsId, Rows);
            return changed;
        }

        public List<string> GetValidationErrors(List<string> results = null)
        {
            results ??= new List<string>();
            results.Clear();

            if (_mainTexture == null)
            {
                results.Add("MainTexture 不能为空。");
            }

            if (_columns <= 0 || _rows <= 0)
            {
                results.Add("Columns 和 Rows 必须大于 0。");
            }

            if (_worldMaterial != null && _worldMaterial.HasProperty("_MainTex") &&
                _mainTexture != null && _worldMaterial.GetTexture("_MainTex") != _mainTexture)
            {
                results.Add("WorldMaterial 的 _MainTex 与 MainTexture 不一致。");
            }

            ValidateDirections(results);
            int authoredDirectionCount = AuthoredDirectionCount;

            HashSet<string> names = new HashSet<string>();
            for (int i = 0; i < _clips.Count; i++)
            {
                FlipbookClipData clip = _clips[i];
                if (clip == null)
                {
                    results.Add($"Clips[{i}] 不能为空。");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(clip.AnimationName))
                {
                    results.Add($"Clips[{i}] 的 AnimationName 不能为空。");
                }
                else if (!names.Add(clip.AnimationName))
                {
                    results.Add($"动画名称重复：{clip.AnimationName}。");
                }

                if (clip.StartFrame < 0 || clip.FrameCount <= 0 ||
                    clip.StartFrame + clip.FrameCount > TotalFrameCount)
                {
                    results.Add($"动画 {clip.AnimationName} 的帧范围超出纹理网格。");
                }

                ValidateClipDirections(clip, authoredDirectionCount, results);

                if (clip.FrameRate <= 0f || clip.Speed <= 0f)
                {
                    results.Add($"动画 {clip.AnimationName} 的 FrameRate 和 Speed 必须大于 0。");
                }

                if (clip.Events == null)
                {
                    continue;
                }

                for (int eventIndex = 0; eventIndex < clip.Events.Count; eventIndex++)
                {
                    FlipbookEventData eventData = clip.Events[eventIndex];
                    if (eventData == null || eventData.Frame < 0 || eventData.Frame >= clip.FrameCount)
                    {
                        results.Add($"动画 {clip.AnimationName} 的事件 {eventIndex} 帧位置无效。");
                    }
                }
            }

            if (_clips.Count > 0 && !string.IsNullOrEmpty(_defaultAnimationName) &&
                !names.Contains(_defaultAnimationName))
            {
                results.Add($"默认动画 {_defaultAnimationName} 不存在。");
            }

            return results;
        }

        private void ValidateDirections(List<string> results)
        {
            if (_directions.Count == 0)
            {
                return;
            }

            bool hasAuthoredDirection = false;
            for (int i = 0; i < _directions.Count; i++)
            {
                FlipbookDirectionData direction = _directions[i];
                if (direction == null)
                {
                    results.Add($"Directions[{i}] 不能为空。");
                    continue;
                }

                string label = string.IsNullOrEmpty(direction.Name) ? $"Directions[{i}]" : direction.Name;
                if (direction.HasOwnFrames)
                {
                    hasAuthoredDirection = true;
                }
                else if (direction.SourceDirection == i)
                {
                    results.Add($"方向 {label} 的 SourceDirection 不能指向自己，使用自己的帧请填 -1。");
                }
                else if (direction.SourceDirection >= _directions.Count)
                {
                    results.Add($"方向 {label} 的 SourceDirection {direction.SourceDirection} 超出方向表范围。");
                }
                else if (_directions[direction.SourceDirection] == null ||
                         !_directions[direction.SourceDirection].HasOwnFrames)
                {
                    results.Add($"方向 {label} 的 SourceDirection 必须指向一个使用自己帧的方向（不支持链式借用）。");
                }

                for (int other = 0; other < i; other++)
                {
                    FlipbookDirectionData otherDirection = _directions[other];
                    if (otherDirection != null &&
                        Mathf.Approximately(Mathf.DeltaAngle(otherDirection.Angle, direction.Angle), 0f))
                    {
                        results.Add($"方向 {label} 与 Directions[{other}] 的角度重复。");
                    }
                }
            }

            if (!hasAuthoredDirection)
            {
                results.Add("方向表中至少需要一个 SourceDirection = -1 的方向。");
            }
        }

        private void ValidateClipDirections(FlipbookClipData clip, int authoredDirectionCount,
            List<string> results)
        {
            if (_directions.Count == 0)
            {
                return;
            }

            int configuredCount = clip.DirectionStartFrames?.Count ?? 0;
            if (configuredCount != authoredDirectionCount && authoredDirectionCount > 1)
            {
                results.Add($"动画 {clip.AnimationName} 的 DirectionStartFrames 数量为 {configuredCount}，" +
                            $"应与使用自己帧的方向数 {authoredDirectionCount} 一致（缺项将使用 StartFrame）。");
            }

            for (int i = 0; i < configuredCount; i++)
            {
                int startFrame = clip.DirectionStartFrames[i];
                if (startFrame < 0 || startFrame + clip.FrameCount > TotalFrameCount)
                {
                    results.Add($"动画 {clip.AnimationName} 的 DirectionStartFrames[{i}] 帧范围超出纹理网格。");
                }
            }
        }

        private string ResolveDefaultAnimationName()
        {
            if (!string.IsNullOrEmpty(_defaultAnimationName))
            {
                return _defaultAnimationName;
            }

            return _clips.Count > 0 && _clips[0] != null ? _clips[0].AnimationName : string.Empty;
        }

        private void BuildClipCache()
        {
            if (_clipByName != null)
            {
                return;
            }

            _clipByName = new Dictionary<string, FlipbookClipData>(_clips.Count);
            for (int i = 0; i < _clips.Count; i++)
            {
                FlipbookClipData clip = _clips[i];
                if (clip != null && !string.IsNullOrEmpty(clip.AnimationName) &&
                    !_clipByName.ContainsKey(clip.AnimationName))
                {
                    _clipByName.Add(clip.AnimationName, clip);
                }
            }
        }

        private void OnValidate()
        {
            _clipByName = null;
            SynchronizeWorldMaterial();
        }

        private static bool SynchronizeTexture(Material material, int propertyId, Texture texture)
        {
            if (!material.HasProperty(propertyId) || material.GetTexture(propertyId) == texture)
            {
                return false;
            }

            material.SetTexture(propertyId, texture);
            return true;
        }

        private static bool SynchronizeFloat(Material material, int propertyId, float value)
        {
            if (!material.HasProperty(propertyId) || Mathf.Approximately(material.GetFloat(propertyId), value))
            {
                return false;
            }

            material.SetFloat(propertyId, value);
            return true;
        }
    }
}
