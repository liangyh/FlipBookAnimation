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

        public int CurrentFrame => _currentFrame;
        public bool IsPlaying => _isPlaying && !_isPaused;
        public bool IsPaused => _isPaused;
        public string CurrentAnimationName => _currentAnimationName;

        public void SetSource(IReadOnlyList<FlipbookClipData> clips, string defaultAnimationName,
            int totalFrameCount)
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
            Reset();
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
            _loop = loop;
            _speed = Mathf.Max(0f, speed);
            _framePosition = startLocalFrame;
            _currentAbsoluteFrame = startLocalFrame;
            _isPlaying = true;
            _isPaused = false;
            SetFrame(clip.StartFrame + startLocalFrame, true);
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
                SetFrame(_currentClip.StartFrame + localFrame);
                return;
            }

            if (targetAbsoluteFrame >= _currentClip.FrameCount)
            {
                _currentAbsoluteFrame = _currentClip.FrameCount;
                SetFrame(_currentClip.StartFrame + _currentClip.FrameCount - 1);
                _isPlaying = false;
                Completed?.Invoke(_currentAnimationName);
                return;
            }

            _currentAbsoluteFrame = targetAbsoluteFrame;
            SetFrame(_currentClip.StartFrame + (int)targetAbsoluteFrame);
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
                SetFrame(_currentClip.StartFrame);
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
            SetFrame(_currentClip.StartFrame + localFrame);
        }

        public void Reset()
        {
            _currentClip = null;
            _currentAnimationName = null;
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
