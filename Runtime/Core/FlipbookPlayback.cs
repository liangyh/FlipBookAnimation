using System;
using System.Collections.Generic;
using UnityEngine;

namespace KingdomTD.Flipbook
{
    public sealed class FlipbookPlayback
    {
        private const float MinAnimationSpeed = 0.0001f;

        private readonly Dictionary<string, FlipbookClipData> _clipByName =
            new Dictionary<string, FlipbookClipData>();
        private readonly List<FlipbookDirectionData> _directions = new List<FlipbookDirectionData>();
        private int[] _directionFrameSources = { 0 };
        private bool[] _directionFlips = { false };
        private int _directionIndex;
        private int _currentStartFrame;
        private int _currentLocalFrame;
        private bool _flipX;
        private FlipbookClipData _currentClip;
        private string _defaultAnimationName;
        private string _currentAnimationName;
        private double _framePosition;
        private long _currentAbsoluteFrame;
        private int _totalFrameCount = 1;
        private int _currentFrame;
        private float _speed = 1f;
        private bool _loop;
        private bool _isPlaying;
        private bool _isPaused;

        public event Action<int> FrameChanged;
        public event Action<string> FrameEvent;
        public event Action<string> Completed;
        public event Action<bool> FlipXChanged;

        public int CurrentFrame => _currentFrame;
        public bool IsPlaying => _isPlaying && !_isPaused;
        public bool IsPaused => _isPaused;
        public string CurrentAnimationName => _currentAnimationName;
        public int CurrentDirectionIndex => _directionIndex;
        public int DirectionCount => _directionFrameSources.Length;
        public bool FlipX => _flipX;
        public IReadOnlyList<FlipbookDirectionData> Directions => _directions;

        public void SetSource(IReadOnlyList<FlipbookClipData> clips, string defaultAnimationName,
            int totalFrameCount)
        {
            SetSource(clips, defaultAnimationName, totalFrameCount, null);
        }

        public void SetSource(IReadOnlyList<FlipbookClipData> clips, string defaultAnimationName,
            int totalFrameCount, IReadOnlyList<FlipbookDirectionData> directions)
        {
            _clipByName.Clear();
            if (clips != null)
            {
                for (int i = 0; i < clips.Count; i++)
                {
                    FlipbookClipData clip = clips[i];
                    if (clip != null && !string.IsNullOrEmpty(clip.AnimationName) &&
                        !_clipByName.ContainsKey(clip.AnimationName))
                    {
                        _clipByName.Add(clip.AnimationName, clip);
                    }
                }
            }

            _defaultAnimationName = ResolveDefaultAnimationName(defaultAnimationName);
            _totalFrameCount = Mathf.Max(1, totalFrameCount);
            BuildDirectionCache(directions);
            Reset();
        }

        /// <summary>
        /// 切换到指定方向。播放中切换会保留当前局部帧与播放进度，不会重复触发帧事件。
        /// </summary>
        public bool SetDirection(int directionIndex)
        {
            if (directionIndex < 0 || directionIndex >= _directionFrameSources.Length)
            {
                return false;
            }

            if (directionIndex == _directionIndex)
            {
                return true;
            }

            _directionIndex = directionIndex;
            ApplyDirection();
            return true;
        }

        /// <summary>
        /// 按屏幕空间角度（0 = 右，逆时针）选择最近的方向。hysteresis 用于避免在方向边界附近来回抖动。
        /// </summary>
        public int SetFacingAngle(float angle, float hysteresis = 0f)
        {
            if (_directions.Count > 0)
            {
                SetDirection(FlipbookDirectionResolver.ResolveIndex(_directions, angle, _directionIndex,
                    Mathf.Max(0f, hysteresis)));
            }

            return _directionIndex;
        }

        public bool TryGetClip(string animationName, out FlipbookClipData clip)
        {
            return _clipByName.TryGetValue(ResolveAnimationName(animationName), out clip);
        }

        public bool Play(string animationName, bool loop = true, float speed = 1f,
            bool randomizeLoopStartFrame = false, bool forceRestart = true)
        {
            string resolvedAnimationName = ResolveAnimationName(animationName);
            if (!_clipByName.TryGetValue(resolvedAnimationName, out FlipbookClipData clip))
            {
                return false;
            }

            if (!forceRestart && _isPlaying && _currentClip == clip)
            {
                _loop = loop;
                _speed = Mathf.Max(0f, speed);
                _isPaused = false;
                return true;
            }

            int startLocalFrame = randomizeLoopStartFrame && loop && clip.FrameCount > 1
                ? UnityEngine.Random.Range(0, clip.FrameCount)
                : 0;

            _currentClip = clip;
            _currentAnimationName = resolvedAnimationName;
            _currentStartFrame = clip.GetStartFrame(_directionFrameSources[_directionIndex]);
            _loop = loop;
            _speed = Mathf.Max(0f, speed);
            _framePosition = startLocalFrame;
            _currentAbsoluteFrame = startLocalFrame;
            _isPlaying = true;
            _isPaused = false;
            SetLocalFrame(startLocalFrame, true);
            TriggerEventsAtLocalFrame(startLocalFrame);
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!_isPlaying || _isPaused || _currentClip == null ||
                _speed <= MinAnimationSpeed || deltaTime <= 0f)
            {
                return;
            }

            _framePosition += deltaTime * Mathf.Max(MinAnimationSpeed, _currentClip.FrameRate) *
                              Mathf.Max(MinAnimationSpeed, _currentClip.Speed) * _speed;
            long targetAbsoluteFrame = (long)Math.Floor(_framePosition);
            if (targetAbsoluteFrame <= _currentAbsoluteFrame)
            {
                return;
            }

            long lastEventFrame = _loop
                ? targetAbsoluteFrame
                : Math.Min(targetAbsoluteFrame, _currentClip.FrameCount - 1L);

            if (_currentClip.Events != null && _currentClip.Events.Count > 0)
            {
                for (long absoluteFrame = _currentAbsoluteFrame + 1;
                     absoluteFrame <= lastEventFrame;
                     absoluteFrame++)
                {
                    TriggerEventsAtLocalFrame((int)(absoluteFrame % _currentClip.FrameCount));
                }
            }

            if (_loop)
            {
                int localFrame = (int)(targetAbsoluteFrame % _currentClip.FrameCount);
                _currentAbsoluteFrame = targetAbsoluteFrame;
                SetLocalFrame(localFrame);
                return;
            }

            if (targetAbsoluteFrame >= _currentClip.FrameCount)
            {
                _currentAbsoluteFrame = _currentClip.FrameCount;
                SetLocalFrame(_currentClip.FrameCount - 1);
                _isPlaying = false;
                Completed?.Invoke(_currentAnimationName);
                return;
            }

            _currentAbsoluteFrame = targetAbsoluteFrame;
            SetLocalFrame((int)targetAbsoluteFrame);
        }

        public void Pause()
        {
            if (_isPlaying)
            {
                _isPaused = true;
            }
        }

        public void Resume()
        {
            if (_isPlaying)
            {
                _isPaused = false;
            }
        }

        public void Stop(bool resetToFirstFrame = true)
        {
            _isPlaying = false;
            _isPaused = false;
            _framePosition = 0d;
            _currentAbsoluteFrame = 0;
            if (resetToFirstFrame && _currentClip != null)
            {
                SetLocalFrame(0);
            }
        }

        public void SetSpeed(float speed)
        {
            _speed = Mathf.Max(0f, speed);
        }

        public void SetNormalizedTime(float normalizedTime)
        {
            if (_currentClip == null)
            {
                return;
            }

            float clampedTime = Mathf.Clamp01(normalizedTime);
            int localFrame = clampedTime >= 1f
                ? _currentClip.FrameCount - 1
                : Mathf.FloorToInt(clampedTime * _currentClip.FrameCount);
            _currentAbsoluteFrame = localFrame;
            _framePosition = localFrame;
            SetLocalFrame(localFrame);
        }

        public void Reset()
        {
            _currentClip = null;
            _currentAnimationName = null;
            _currentStartFrame = 0;
            _currentLocalFrame = 0;
            _framePosition = 0d;
            _currentAbsoluteFrame = 0;
            _speed = 1f;
            _loop = false;
            _isPlaying = false;
            _isPaused = false;
            SetFrame(0, true);
        }

        private string ResolveAnimationName(string animationName)
        {
            return string.IsNullOrEmpty(animationName) ? _defaultAnimationName : animationName;
        }

        private string ResolveDefaultAnimationName(string defaultAnimationName)
        {
            if (!string.IsNullOrEmpty(defaultAnimationName) && _clipByName.ContainsKey(defaultAnimationName))
            {
                return defaultAnimationName;
            }

            if (_clipByName.ContainsKey("Idle"))
            {
                return "Idle";
            }

            foreach (KeyValuePair<string, FlipbookClipData> pair in _clipByName)
            {
                return pair.Key;
            }

            return string.Empty;
        }

        private void BuildDirectionCache(IReadOnlyList<FlipbookDirectionData> directions)
        {
            _directions.Clear();
            if (directions != null)
            {
                for (int i = 0; i < directions.Count; i++)
                {
                    _directions.Add(directions[i]);
                }
            }

            int directionCount = Mathf.Max(1, _directions.Count);
            if (_directionFrameSources.Length != directionCount)
            {
                _directionFrameSources = new int[directionCount];
                _directionFlips = new bool[directionCount];
            }

            _directionFrameSources[0] = 0;
            _directionFlips[0] = false;
            for (int i = 0; i < _directions.Count; i++)
            {
                FlipbookDirectionResolver.ResolveSource(_directions, i, out _directionFrameSources[i],
                    out _directionFlips[i]);
            }

            _directionIndex = 0;
            SetFlipX(_directionFlips[0]);
        }

        private void ApplyDirection()
        {
            SetFlipX(_directionFlips[_directionIndex]);
            if (_currentClip == null)
            {
                return;
            }

            _currentStartFrame = _currentClip.GetStartFrame(_directionFrameSources[_directionIndex]);
            SetFrame(_currentStartFrame + _currentLocalFrame);
        }

        private void SetFlipX(bool flipX)
        {
            if (_flipX == flipX)
            {
                return;
            }

            _flipX = flipX;
            FlipXChanged?.Invoke(_flipX);
        }

        private void SetLocalFrame(int localFrame, bool force = false)
        {
            _currentLocalFrame = localFrame;
            SetFrame(_currentStartFrame + localFrame, force);
        }

        private void SetFrame(int frame, bool force = false)
        {
            int clampedFrame = Mathf.Clamp(frame, 0, _totalFrameCount - 1);
            if (!force && _currentFrame == clampedFrame)
            {
                return;
            }

            _currentFrame = clampedFrame;
            FrameChanged?.Invoke(_currentFrame);
        }

        private void TriggerEventsAtLocalFrame(int localFrame)
        {
            if (_currentClip?.Events == null)
            {
                return;
            }

            for (int i = 0; i < _currentClip.Events.Count; i++)
            {
                FlipbookEventData eventData = _currentClip.Events[i];
                if (eventData != null && eventData.Frame == localFrame &&
                    !string.IsNullOrEmpty(eventData.Name))
                {
                    FrameEvent?.Invoke(eventData.Name);
                }
            }
        }
    }
}
