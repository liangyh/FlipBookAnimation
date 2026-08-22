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
                _animationAsset.TotalFrameCount);
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

        private void RestoreRequiredPropertiesIfNeeded()
        {
            if (_propertyBlock.GetFloat(ColumnsId) >= 1f && _propertyBlock.GetFloat(RowsId) >= 1f)
            {
                return;
            }

            _propertyBlock.SetFloat(CurrentFrameId, _playback.CurrentFrame);
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
        }
#endif
    }
}
