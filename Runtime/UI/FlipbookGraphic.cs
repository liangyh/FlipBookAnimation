using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KingdomTD.Flipbook
{
    [DisallowMultipleComponent]
    [AddComponentMenu("UI (Canvas)/Flipbook Graphic", 13)]
    public sealed class FlipbookGraphic : MaskableGraphic
    {
        [Header("Asset")]
        [SerializeField] private FlipbookAnimationAsset _animationAsset;

        [Header("Playback")]
        [SerializeField] private string _defaultAnimationName;
        [SerializeField] private FlipbookTimeMode _timeMode = FlipbookTimeMode.Unscaled;
        [SerializeField] private bool _playOnEnable = true;
        [SerializeField] private bool _loop = true;
        [SerializeField] private bool _randomizeLoopStartFrame;
        [SerializeField] private bool _pauseWhenCulled = true;
        [SerializeField] private bool _preserveAspect = true;
        [Min(0f)] [SerializeField] private float _speed = 1f;

        [Header("Direction")]
        [Tooltip("尚未调用 SetFacing 系列方法时使用的屏幕朝向角度：0 = 右，90 = 上，270 = 下。")]
        [SerializeField] private float _initialFacingAngle = 270f;
        [Tooltip("方向边界附近的回差角度，避免朝向在两个方向之间来回抖动。")]
        [Range(0f, 45f)] [SerializeField] private float _directionHysteresis = 5f;

        [Header("Events")]
        [SerializeField] private UnityEvent<string> _frameEvent = new UnityEvent<string>();
        [SerializeField] private UnityEvent<string> _animationCompleted = new UnityEvent<string>();

        private readonly FlipbookPlayback _playback = new FlipbookPlayback();
        private bool _callbacksAttached;
        private bool _isReady;
        private bool _hasFacingAngle;
        private float _facingAngle;

        public event Action<string> FrameEvent;
        public event Action<string> AnimationCompleted;

        public FlipbookAnimationAsset AnimationAsset => _animationAsset;
        public int CurrentFrame => _playback.CurrentFrame;
        public string CurrentAnimationName => _playback.CurrentAnimationName;
        public bool IsPlaying => _playback.IsPlaying;
        public bool IsReady => _isReady;
        public int CurrentDirectionIndex => _playback.CurrentDirectionIndex;
        public bool IsFlippedX => _playback.FlipX;
        public float FacingAngle => _hasFacingAngle ? _facingAngle : _initialFacingAngle;
        public bool PreserveAspect
        {
            get => _preserveAspect;
            set
            {
                if (_preserveAspect == value)
                {
                    return;
                }

                _preserveAspect = value;
                SetVerticesDirty();
            }
        }

        public override Texture mainTexture => _animationAsset != null && _animationAsset.MainTexture != null
            ? _animationAsset.MainTexture
            : s_WhiteTexture;

        protected override void Awake()
        {
            base.Awake();
            useLegacyMeshGeneration = false;
            AttachPlaybackCallbacks();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (!Initialize())
            {
                return;
            }

            if (_playOnEnable)
            {
                Play(ResolveDefaultAnimationName(), _loop, _speed);
            }
        }

        protected override void OnDisable()
        {
            _playback.Pause();
            base.OnDisable();
        }

        private void Update()
        {
            if (!_isReady || (_pauseWhenCulled && canvasRenderer.cull))
            {
                return;
            }

            float deltaTime = _timeMode == FlipbookTimeMode.Unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            _playback.Tick(deltaTime);
        }

        public bool Initialize()
        {
            if (_isReady)
            {
                return true;
            }

            AttachPlaybackCallbacks();
            if (_animationAsset == null || _animationAsset.MainTexture == null)
            {
                _isReady = false;
                return false;
            }

            _playback.SetSource(_animationAsset.Clips, ResolveDefaultAnimationName(),
                _animationAsset.TotalFrameCount, _animationAsset.Directions);
            _playback.SetFacingAngle(FacingAngle);
            _isReady = _animationAsset.Clips.Count > 0;
            SetMaterialDirty();
            SetVerticesDirty();
            return _isReady;
        }

        public void SetAsset(FlipbookAnimationAsset animationAsset)
        {
            if (_animationAsset == animationAsset && _isReady)
            {
                return;
            }

            _playback.Stop(false);
            _isReady = false;
            _animationAsset = animationAsset;
            if (!Initialize())
            {
                SetMaterialDirty();
                SetVerticesDirty();
                return;
            }

            if (isActiveAndEnabled && _playOnEnable)
            {
                Play(ResolveDefaultAnimationName(), _loop, _speed);
            }
        }

        public bool Play(string animationName = null, bool loop = true, float speed = 1f,
            bool forceRestart = true)
        {
            return _isReady && _playback.Play(animationName, loop, speed, _randomizeLoopStartFrame, forceRestart);
        }

        /// <summary>
        /// 设置朝向后播放。方向由角度自动选择最接近的方向。
        /// </summary>
        public bool Play(string animationName, float facingAngle, bool loop = true, float speed = 1f,
            bool forceRestart = true)
        {
            SetFacingAngle(facingAngle);
            return Play(animationName, loop, speed, forceRestart);
        }

        /// <summary>
        /// 设置屏幕空间朝向角度（0 = 右，90 = 上，逆时针）。播放中切换方向不会重播动画。
        /// </summary>
        public void SetFacingAngle(float screenAngleDegrees)
        {
            _facingAngle = Mathf.Repeat(screenAngleDegrees, 360f);
            _hasFacingAngle = true;
            if (_isReady)
            {
                _playback.SetFacingAngle(_facingAngle, _directionHysteresis);
            }
        }

        /// <summary>
        /// 设置屏幕空间朝向（x = 右，y = 上）。零向量会被忽略并保持当前方向。
        /// </summary>
        public bool SetFacing(Vector2 screenDirection)
        {
            if (!FlipbookDirectionResolver.TryGetAngle(screenDirection, out float angle))
            {
                return false;
            }

            SetFacingAngle(angle);
            return true;
        }

        public bool SetDirection(int directionIndex)
        {
            return _isReady && _playback.SetDirection(directionIndex);
        }

        public void Pause()
        {
            _playback.Pause();
        }

        public void Resume()
        {
            _playback.Resume();
        }

        public void Stop(bool resetToFirstFrame = true)
        {
            _playback.Stop(resetToFirstFrame);
        }

        public void SetSpeed(float speed)
        {
            _speed = Mathf.Max(0f, speed);
            _playback.SetSpeed(_speed);
        }

        public void SetTimeMode(FlipbookTimeMode timeMode)
        {
            _timeMode = timeMode;
        }

        public void SetNormalizedTime(float normalizedTime)
        {
            _playback.SetNormalizedTime(normalizedTime);
        }

        public override void SetNativeSize()
        {
            if (_animationAsset == null || _animationAsset.MainTexture == null)
            {
                return;
            }

            Vector2 frameSize = _animationAsset.FramePixelSize;
            rectTransform.anchorMax = rectTransform.anchorMin;
            rectTransform.sizeDelta = new Vector2(Mathf.RoundToInt(frameSize.x), Mathf.RoundToInt(frameSize.y));
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (!_isReady || _animationAsset == null || _animationAsset.MainTexture == null)
            {
                return;
            }

            Rect drawRect = GetPixelAdjustedRect();
            if (_preserveAspect)
            {
                drawRect = GetAspectFittedRect(drawRect, _animationAsset.FramePixelSize);
            }

            Rect uvRect = _animationAsset.GetFrameUvRect(_playback.CurrentFrame);
            bool flipX = _playback.FlipX;
            float uvLeft = flipX ? uvRect.xMax : uvRect.xMin;
            float uvRight = flipX ? uvRect.xMin : uvRect.xMax;
            Color32 vertexColor = color;
            vertexHelper.AddVert(new Vector3(drawRect.xMin, drawRect.yMin), vertexColor,
                new Vector2(uvLeft, uvRect.yMin));
            vertexHelper.AddVert(new Vector3(drawRect.xMin, drawRect.yMax), vertexColor,
                new Vector2(uvLeft, uvRect.yMax));
            vertexHelper.AddVert(new Vector3(drawRect.xMax, drawRect.yMax), vertexColor,
                new Vector2(uvRight, uvRect.yMax));
            vertexHelper.AddVert(new Vector3(drawRect.xMax, drawRect.yMin), vertexColor,
                new Vector2(uvRight, uvRect.yMin));
            vertexHelper.AddTriangle(0, 1, 2);
            vertexHelper.AddTriangle(2, 3, 0);
        }

        private void AttachPlaybackCallbacks()
        {
            if (_callbacksAttached)
            {
                return;
            }

            _playback.FrameChanged += OnFrameChanged;
            _playback.FlipXChanged += OnFlipXChanged;
            _playback.FrameEvent += OnFrameEvent;
            _playback.Completed += OnAnimationCompleted;
            _callbacksAttached = true;
        }

        private string ResolveDefaultAnimationName()
        {
            return string.IsNullOrEmpty(_defaultAnimationName)
                ? _animationAsset?.DefaultAnimationName
                : _defaultAnimationName;
        }

        private void OnFrameChanged(int frame)
        {
            SetVerticesDirty();
        }

        private void OnFlipXChanged(bool flipX)
        {
            SetVerticesDirty();
        }

        private void OnFrameEvent(string eventName)
        {
            _frameEvent?.Invoke(eventName);
            FrameEvent?.Invoke(eventName);
        }

        private void OnAnimationCompleted(string animationName)
        {
            _animationCompleted?.Invoke(animationName);
            AnimationCompleted?.Invoke(animationName);
        }

        private Rect GetAspectFittedRect(Rect rect, Vector2 frameSize)
        {
            if (frameSize.x <= 0f || frameSize.y <= 0f || rect.width <= 0f || rect.height <= 0f)
            {
                return rect;
            }

            float frameAspect = frameSize.x / frameSize.y;
            float rectAspect = rect.width / rect.height;
            if (rectAspect > frameAspect)
            {
                float width = rect.height * frameAspect;
                rect.x += (rect.width - width) * rectTransform.pivot.x;
                rect.width = width;
            }
            else
            {
                float height = rect.width / frameAspect;
                rect.y += (rect.height - height) * rectTransform.pivot.y;
                rect.height = height;
            }

            return rect;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (_isReady && !_hasFacingAngle)
            {
                _playback.SetFacingAngle(_initialFacingAngle);
            }

            SetMaterialDirty();
            SetVerticesDirty();
        }
#endif
    }
}
