using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UIElements;

namespace KingdomTD.Flipbook.Samples
{
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public sealed class FlipbookGroundShadowTest : MonoBehaviour
    {
        private const float SampleInterval = 0.5f;

        [Min(1)] [SerializeField] private int _characterCount = 1;
        [SerializeField] private bool _showGroundShadows = true;
        [SerializeField] private FlipbookRenderer _character;
        [SerializeField] private Camera _viewCamera;
        [SerializeField] private Transform _ground;
        [SerializeField] private FlipbookGroundShadowSettings _shadowSettings;
        [SerializeField] private VisualTreeAsset _controls;

        private readonly List<FlipbookRenderer> _characters = new List<FlipbookRenderer>();
        private readonly FrameTiming[] _frameTimings = new FrameTiming[1];
        private VisualElement _panel;
        private IntegerField _countField;
        private Toggle _shadowToggle;
        private Button _pauseButton;
        private Label _frameRate;
        private Label _renderStats;
        private ProfilerRecorder _drawCalls;
        private Vector4 _framing;
        private bool _shadowsApplied;
        private bool _paused;
        private bool _captureTimings;
        private float _measureAfter;
        private float _elapsed;
        private int _frames;
        private long _lastDraws;

        private Vector4 Framing => new Vector4(
            _viewCamera.aspect, _shadowSettings.GroundHeight, _shadowSettings.Length, _shadowSettings.ScreenAngle);

        private void OnEnable()
        {
            if (_character == null || _viewCamera == null || _ground == null || _shadowSettings == null ||
                _controls == null)
            {
                Debug.LogError("Assign the character, camera, ground, shadow settings and controls.", this);
                enabled = false;
                return;
            }

            _panel = _controls.Instantiate();
            GetComponent<UIDocument>().rootVisualElement.Add(_panel);
            _countField = _panel.Q<IntegerField>("count");
            _shadowToggle = _panel.Q<Toggle>("shadows");
            _pauseButton = _panel.Q<Button>("pause");
            _frameRate = _panel.Q<Label>("frame-rate");
            _renderStats = _panel.Q<Label>("render-stats");
            _panel.Q<Button>("apply").clicked += () => SetCount(_countField.value);
            _panel.Q<Button>("detail").clicked += () => SetCount(1);
            _panel.Q<Button>("stress-1000").clicked += () => SetCount(1000);
            _panel.Q<Button>("stress-2000").clicked += () => SetCount(2000);
            _shadowToggle.RegisterValueChangedCallback(change => SetShadows(change.newValue));
            _pauseButton.clicked += () => SetPaused(!_paused);

            if (_characters.Count == 0)
            {
                _characters.Add(_character);
            }

            _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _captureTimings = FrameTimingManager.IsFeatureEnabled();
            SetShadows(_showGroundShadows);
            SetCount(_characterCount);
        }

        private void OnDisable()
        {
            _panel?.RemoveFromHierarchy();
            _drawCalls.Dispose();
            if (_paused && _character != null)
            {
                _character.Resume();
                _paused = false;
            }
        }

        private void Update()
        {
            if (_characterCount != _characters.Count)
            {
                SetCount(_characterCount);
            }
            else if (!_framing.Equals(Framing))
            {
                FrameCharacters();
                RestartMeasurement();
            }

            if (_showGroundShadows != _shadowsApplied)
            {
                SetShadows(_showGroundShadows);
            }

            UpdateMeasurement();
        }

        private void SetCount(int count)
        {
            _characterCount = Mathf.Max(1, count);
            _countField.SetValueWithoutNotify(_characterCount);
            while (_characters.Count < _characterCount)
            {
                FlipbookRenderer actor = Instantiate(_character, _character.transform.parent);
                // Spread playback over the clip so a whole batch does not advance frames at once.
                actor.SetNormalizedTime((_characters.Count % 29) / 29f);
                _characters.Add(actor);
            }

            while (_characters.Count > _characterCount)
            {
                int last = _characters.Count - 1;
                GameObject actor = _characters[last].gameObject;
                actor.SetActive(false);
                Destroy(actor);
                _characters.RemoveAt(last);
            }

            if (_characterCount > 1 && _paused)
            {
                SetPaused(false);
            }
            _pauseButton.style.display = _characterCount == 1 ? DisplayStyle.Flex : DisplayStyle.None;
            FrameCharacters();
            RestartMeasurement();
        }

        private void SetShadows(bool show)
        {
            _showGroundShadows = show;
            _shadowsApplied = show;
            _shadowToggle.SetValueWithoutNotify(show);
            foreach (FlipbookRenderer actor in _characters)
            {
                actor.GroundShadowEnabled = show;
            }
            RestartMeasurement();
        }

        private void SetPaused(bool paused)
        {
            _paused = paused;
            if (paused)
            {
                _character.Pause();
            }
            else
            {
                _character.Resume();
            }
            _pauseButton.text = paused ? "Resume" : "Pause";
        }

        private void FrameCharacters()
        {
            _framing = Framing;
            float aspect = _framing.x;
            Vector3 origin = Vector3.up * _framing.y;
            Transform view = _viewCamera.transform;
            Vector3 right = view.right;
            Vector3 up = view.up;
            Vector3 forward = view.forward;
            Vector3 depth = Vector3.Cross(right, Vector3.up);
            float pitch = Mathf.Max(0.01f, -forward.y);
            float width = Mathf.Abs(_character.transform.lossyScale.x);
            float height = Mathf.Abs(_character.transform.lossyScale.y);
            float footHeight = _character.GroundShadowFootHeight;
            float stepX = width * 1.2f;
            float stepZ = height * 1.5f;
            int columns = Mathf.Clamp(
                Mathf.CeilToInt(Mathf.Sqrt(_characterCount * aspect * stepZ * pitch / stepX)), 1, _characterCount);
            int rows = (_characterCount - 1) / columns + 1;
            Vector3 pivotOffset = up * ((0.5f - footHeight) * height);

            for (int i = 0; i < _characters.Count; i++)
            {
                int row = i / columns;
                int rowLength = Mathf.Min(columns, _characterCount - row * columns);
                float x = (i % columns - (rowLength - 1) * 0.5f) * stepX;
                float z = (row - (rows - 1) * 0.5f) * stepZ;
                _characters[i].transform.SetPositionAndRotation(origin + right * x + depth * z + pivotOffset,
                    view.rotation);
            }

            // Reserve the same framing in both shadow states. The camera is level across the horizon.
            float angle = _framing.w * Mathf.Deg2Rad;
            Vector2 heading = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 groundDirection = new Vector2(heading.x, heading.y / pitch).normalized;
            Vector2 projectedShadow = new Vector2(groundDirection.x, groundDirection.y * pitch) * (_framing.z * height);
            Vector2 shadowHalfWidth = new Vector2(Mathf.Abs(groundDirection.y), Mathf.Abs(groundDirection.x) * pitch) *
                                      (width * 0.5f);
            Vector2 shadowStart = projectedShadow * -footHeight;
            Vector2 shadowEnd = projectedShadow * (1f - footHeight);
            Vector2 min = Vector2.Min(new Vector2(-width * 0.5f, -height * footHeight),
                Vector2.Min(shadowStart, shadowEnd) - shadowHalfWidth);
            Vector2 max = Vector2.Max(new Vector2(width * 0.5f, height * (1f - footHeight)),
                Vector2.Max(shadowStart, shadowEnd) + shadowHalfWidth);
            Vector2 center = (min + max) * 0.5f;
            Vector2 size = max - min + new Vector2((columns - 1) * stepX, (rows - 1) * stepZ * pitch);
            float verticalSize = Mathf.Max(size.y, size.x / aspect) * 0.66f;
            float distance = verticalSize / pitch * 2f + 10f;
            Vector3 focus = origin + right * center.x + up * center.y;
            view.position = focus - forward * distance;
            _viewCamera.orthographicSize = verticalSize;
            _viewCamera.farClipPlane = distance * 2f;

            // The Unity Plane is ten units wide. Cover the view with a single, unlit surface.
            Vector3 floorCenter = focus + forward * ((focus.y - origin.y) / pitch);
            _ground.position = new Vector3(floorCenter.x, origin.y, floorCenter.z);
            _ground.localScale = new Vector3(verticalSize * aspect * 0.22f, 1f, verticalSize / pitch * 0.22f);
        }

        private void RestartMeasurement()
        {
            _measureAfter = Time.unscaledTime + 0.35f;
            _elapsed = 0f;
            _frames = 0;
            _lastDraws = 0;
            _frameRate.text = $"{_characters.Count:N0} active  |  Measuring...";
            _renderStats.text = string.Empty;
        }

        private void UpdateMeasurement()
        {
            if (_captureTimings)
            {
                FrameTimingManager.CaptureFrameTimings();
            }
            if (Time.unscaledTime < _measureAfter)
            {
                return;
            }

            if (_drawCalls.Valid)
            {
                long draws = _drawCalls.LastValue;
                if (draws > 0)
                {
                    _lastDraws = draws;
                }
            }

            _elapsed += Time.unscaledDeltaTime;
            _frames++;
            if (_elapsed < SampleInterval)
            {
                return;
            }

            _frameRate.text = $"{_characters.Count:N0} active  |  {_frames / _elapsed:F1} FPS  |  " +
                              $"{1000f * _elapsed / _frames:F2} ms";
            string cpu = "—";
            string gpu = "—";
            if (_captureTimings && FrameTimingManager.GetLatestTimings(1, _frameTimings) > 0)
            {
                FrameTiming timing = _frameTimings[0];
                cpu = FormatMilliseconds(timing.cpuMainThreadFrameTime);
                gpu = FormatMilliseconds(timing.gpuFrameTime);
            }
            string drawCount = _lastDraws > 0 ? _lastDraws.ToString("N0") : "—";
            _renderStats.text = $"CPU {cpu}  |  GPU {gpu}  |  Draws {drawCount}";
            _elapsed = 0f;
            _frames = 0;
        }

        private static string FormatMilliseconds(double milliseconds)
        {
            return milliseconds > 0d ? $"{milliseconds:F2} ms" : "—";
        }
    }
}
