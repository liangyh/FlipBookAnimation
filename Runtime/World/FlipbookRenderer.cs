using System;
using UnityEngine;
using UnityEngine.Events;

namespace KingdomTD.Flipbook
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [AddComponentMenu("KingdomTD/Flipbook Renderer")]
    public sealed class FlipbookRenderer : MonoBehaviour
    {
        [Header("Asset")]
        [SerializeField] private FlipbookAnimationAsset _animationAsset;
        [SerializeField, HideInInspector] private MeshFilter _meshFilter;
        [SerializeField, HideInInspector] private MeshRenderer _meshRenderer;

        [Header("Playback")]
        [SerializeField] private string _defaultAnimationName;
        [SerializeField] private FlipbookTimeMode _timeMode = FlipbookTimeMode.Scaled;
        [SerializeField] private bool _playOnEnable = true;
        [SerializeField] private bool _loop = true;
        [SerializeField] private bool _randomizeLoopStartFrame = true;
        [Min(0f)] [SerializeField] private float _speed = 1f;

        [Header("Direction")]
        [Tooltip("尚未调用 SetFacing 系列方法时使用的屏幕朝向角度：0 = 右，90 = 上，270 = 下。")]
        [SerializeField] private float _initialFacingAngle = 270f;
        [Tooltip("方向边界附近的回差角度，避免朝向在两个方向之间来回抖动。")]
        [Range(0f, 45f)] [SerializeField] private float _directionHysteresis = 5f;

        [Header("Billboard")]
        [SerializeField] private bool _faceCamera = true;
        [Tooltip("启用时旋转父节点；父节点不存在时旋转自身。")]
        [SerializeField] private bool _rotateParentForBillboard = true;
        [SerializeField] private bool _useCameraForward = true;

        [Header("Events")]
        [SerializeField] private UnityEvent<string> _frameEvent = new UnityEvent<string>();
        [SerializeField] private UnityEvent<string> _animationCompleted = new UnityEvent<string>();

        private static readonly int CurrentFrameId = Shader.PropertyToID("_CurrentFrame");
        private static readonly int ColumnsId = Shader.PropertyToID("_Columns");
        private static readonly int RowsId = Shader.PropertyToID("_Rows");
        private static readonly int FlipXId = Shader.PropertyToID("_FlipX");
        private static readonly int ChangeColorId = Shader.PropertyToID("_ChangeColor");
        private static readonly int ChangeRateId = Shader.PropertyToID("_ChangeRate");
        private static readonly int EffectChangeRateId = Shader.PropertyToID("_EffectChangeRate");

        private static int _cachedCameraFrame = -1;
        private static Camera _cachedCamera;
        private static Quaternion _cachedCameraRotation;

        private readonly FlipbookPlayback _playback = new FlipbookPlayback();
        private Renderer[] _colorRenderers = Array.Empty<Renderer>();
        private MaterialPropertyBlock _propertyBlock;
        private bool _callbacksAttached;
        private bool _isReady;
        private bool _hasFacingAngle;
        private float _facingAngle;

        public event Action<string> FrameEvent;
        public event Action<string> AnimationCompleted;

        public FlipbookAnimationAsset AnimationAsset => _animationAsset;
        public MeshRenderer TargetRenderer => _meshRenderer;
        public MaterialPropertyBlock PropertyBlock => _propertyBlock;
        public Renderer[] ColorRenderers => _isReady ? _colorRenderers : Array.Empty<Renderer>();
        public int CurrentFrame => _playback.CurrentFrame;
        public string CurrentAnimationName => _playback.CurrentAnimationName;
        public bool IsPlaying => _playback.IsPlaying;
        public bool IsReady => _isReady;
        public int CurrentDirectionIndex => _playback.CurrentDirectionIndex;
        public bool IsFlippedX => _playback.FlipX;
        public float FacingAngle => _hasFacingAngle ? _facingAngle : _initialFacingAngle;

        private void Reset()
        {
            SynchronizeRendererResources();
        }

        private void Awake()
        {
            SynchronizeRendererResources();
            AttachPlaybackCallbacks();
        }

        private void OnEnable()
        {
            if (!Initialize())
            {
                return;
            }

            if (_playOnEnable)
            {
                Play(ResolveDefaultAnimationName(), _loop, _speed);
            }
        }

        private void OnDisable()
        {
            _playback.Pause();
        }

        private void Update()
        {
            if (!_isReady)
            {
                return;
            }

            float deltaTime = _timeMode == FlipbookTimeMode.Unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            _playback.Tick(deltaTime);
        }

        private void LateUpdate()
        {
            if (_isReady)
            {
                RestoreRequiredPropertiesIfNeeded();
                ApplyBillboard();
            }
        }

        public bool Initialize()
        {
            if (_isReady)
            {
                return true;
            }

            SynchronizeRendererResources();
            AttachPlaybackCallbacks();
            if (_animationAsset == null || _meshFilter == null || _meshRenderer == null)
            {
                return false;
            }

            if (_animationAsset.WorldMaterial == null)
            {
                Debug.LogError($"{name}: FlipbookRenderer 的资产缺少 WorldMaterial。", this);
                return false;
            }

            if (_animationAsset.MainTexture != null && _animationAsset.WorldMaterial.HasProperty("_MainTex") &&
                _animationAsset.WorldMaterial.GetTexture("_MainTex") != _animationAsset.MainTexture)
            {
                Debug.LogError($"{name}: FlipbookRenderer 的 WorldMaterial 与 MainTexture 不一致。", this);
                return false;
            }

            _propertyBlock ??= new MaterialPropertyBlock();
            _colorRenderers = new Renderer[] { _meshRenderer };
            _isReady = true;
            ResetPresentation();
            _playback.SetSource(_animationAsset.Clips, ResolveDefaultAnimationName(),
                _animationAsset.TotalFrameCount, _animationAsset.Directions);
            _playback.SetFacingAngle(FacingAngle);
            SetFlipX(_playback.FlipX);
            return true;
        }

        public void Deinitialize()
        {
            _playback.Stop(false);
            _isReady = false;
        }

        public void SetAsset(FlipbookAnimationAsset animationAsset)
        {
            if (_animationAsset == animationAsset && _isReady)
            {
                return;
            }

            Deinitialize();
            _animationAsset = animationAsset;
            if (!Initialize())
            {
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
        /// 设置屏幕空间朝向角度（0 = 右，90 = 上/远离相机，逆时针）。播放中切换方向不会重播动画。
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

        /// <summary>
        /// 设置地面（XZ 平面）上的世界朝向，例如移动速度。会根据主相机的水平朝向转换为屏幕角度。
        /// 零向量会被忽略并保持当前方向。
        /// </summary>
        public bool SetFacingWorld(Vector3 worldDirection)
        {
            Camera camera = GetCachedCamera();
            if (!FlipbookDirectionResolver.TryGetScreenAngle(worldDirection,
                    camera != null ? camera.transform : null, Vector3.up, out float angle))
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

        public void SetChangeColor(Color color)
        {
            if (!_isReady)
            {
                return;
            }

            _propertyBlock.SetColor(ChangeColorId, color);
            ApplyPropertyBlock();
        }

        public void SetChangeRate(float rate)
        {
            if (!_isReady)
            {
                return;
            }

            _propertyBlock.SetFloat(ChangeRateId, Mathf.Clamp01(rate));
            ApplyPropertyBlock();
        }

        public void SetEffectChangeRate(float rate)
        {
            if (!_isReady)
            {
                return;
            }

            _propertyBlock.SetFloat(EffectChangeRateId, Mathf.Clamp01(rate));
            ApplyPropertyBlock();
        }

        private void EnsureComponents()
        {
            _meshFilter ??= GetComponent<MeshFilter>();
            _meshRenderer ??= GetComponent<MeshRenderer>();
        }

        private void SynchronizeRendererResources()
        {
            EnsureComponents();
            if (_meshFilter != null)
            {
                _meshFilter.sharedMesh = FlipbookQuadMesh.Get();
            }

            if (_meshRenderer != null)
            {
                _meshRenderer.sharedMaterial = _animationAsset != null ? _animationAsset.WorldMaterial : null;
            }
        }

        private void AttachPlaybackCallbacks()
        {
            if (_callbacksAttached)
            {
                return;
            }

            _playback.FrameChanged += SetFrame;
            _playback.FlipXChanged += SetFlipX;
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

        private void ResetPresentation()
        {
            _propertyBlock.Clear();
            _propertyBlock.SetColor(ChangeColorId, Color.white);
            _propertyBlock.SetFloat(ChangeRateId, 0f);
            _propertyBlock.SetFloat(EffectChangeRateId, 0f);
            _propertyBlock.SetFloat(CurrentFrameId, 0f);
            _propertyBlock.SetFloat(FlipXId, 0f);
            _propertyBlock.SetFloat(ColumnsId, _animationAsset.Columns);
            _propertyBlock.SetFloat(RowsId, _animationAsset.Rows);
            ApplyPropertyBlock();
        }

        private void SetFrame(int frame)
        {
            if (!_isReady)
            {
                return;
            }

            _propertyBlock.SetFloat(CurrentFrameId, frame);
            ApplyPropertyBlock();
        }

        private void SetFlipX(bool flipX)
        {
            if (!_isReady)
            {
                return;
            }

            _propertyBlock.SetFloat(FlipXId, flipX ? 1f : 0f);
            ApplyPropertyBlock();
        }

        private void RestoreRequiredPropertiesIfNeeded()
        {
            if (_propertyBlock.GetFloat(ColumnsId) >= 1f && _propertyBlock.GetFloat(RowsId) >= 1f)
            {
                return;
            }

            _propertyBlock.SetFloat(CurrentFrameId, _playback.CurrentFrame);
            _propertyBlock.SetFloat(FlipXId, _playback.FlipX ? 1f : 0f);
            _propertyBlock.SetFloat(ColumnsId, _animationAsset.Columns);
            _propertyBlock.SetFloat(RowsId, _animationAsset.Rows);
            ApplyPropertyBlock();
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

        private void ApplyBillboard()
        {
            if (!_faceCamera)
            {
                return;
            }

            Camera camera = GetCachedCamera();
            if (camera == null)
            {
                return;
            }

            Transform billboardTarget = _rotateParentForBillboard && transform.parent != null
                ? transform.parent
                : transform;
            billboardTarget.rotation = _useCameraForward
                ? _cachedCameraRotation
                : Quaternion.LookRotation(billboardTarget.position - camera.transform.position, camera.transform.up);
        }

        private static Camera GetCachedCamera()
        {
            if (_cachedCameraFrame == Time.frameCount)
            {
                return _cachedCamera;
            }

            _cachedCameraFrame = Time.frameCount;
            _cachedCamera = Camera.main;
            if (_cachedCamera != null)
            {
                _cachedCameraRotation = Quaternion.LookRotation(_cachedCamera.transform.forward,
                    _cachedCamera.transform.up);
            }

            return _cachedCamera;
        }

        private void ApplyPropertyBlock()
        {
            _meshRenderer.SetPropertyBlock(_propertyBlock);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            SynchronizeRendererResources();
            if (_isReady && !_hasFacingAngle)
            {
                _playback.SetFacingAngle(_initialFacingAngle);
            }
        }
#endif
    }
}
