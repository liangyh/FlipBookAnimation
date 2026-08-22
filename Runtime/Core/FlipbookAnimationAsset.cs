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

        private Dictionary<string, FlipbookClipData> _clipByName;

        public Texture2D MainTexture => _mainTexture;
        public Texture2D EffectTexture => _effectTexture;
        public Material WorldMaterial => _worldMaterial;
        public int Columns => Mathf.Max(1, _columns);
        public int Rows => Mathf.Max(1, _rows);
        public int TotalFrameCount => Columns * Rows;
        public string DefaultAnimationName => ResolveDefaultAnimationName();
        public IReadOnlyList<FlipbookClipData> Clips => _clips;
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
