// TransitionController.cs
// Fullscreen fade overlay for phase transitions.
// Uses VisualElement.schedule for animation since UI Toolkit has no async/await support.
// Non-MonoBehaviour — owned by GameFlowController.

using System;
using UnityEngine.UIElements;

namespace WarStrategy.UI
{
    public class TransitionController
    {
        private VisualElement _overlay;
        private bool _animating;

        /// <summary>
        /// True while a fade animation is in progress.
        /// </summary>
        public bool IsAnimating => _animating;

        /// <summary>
        /// Creates the fullscreen overlay element and adds it to the root.
        /// Must be called before any fade operations.
        /// </summary>
        public void Initialize(VisualElement root)
        {
            _overlay = new VisualElement
            {
                name = "transition-overlay",
                pickingMode = PickingMode.Ignore
            };

            // Fullscreen absolute positioning
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0;
            _overlay.style.top = 0;
            _overlay.style.right = 0;
            _overlay.style.bottom = 0;

            // Dark navy background matching the game's visual style
            _overlay.style.backgroundColor = new StyleColor(
                new UnityEngine.Color(11f / 255f, 20f / 255f, 38f / 255f, 1f) // #0B1426
            );

            // Start hidden
            _overlay.style.opacity = 0f;
            _overlay.style.display = DisplayStyle.None;

            root.Add(_overlay);
        }

        /// <summary>
        /// Fade to black (opacity 0 → 1). Calls onComplete when finished.
        /// Blocks input during animation.
        /// </summary>
        public void FadeOut(float duration, Action onComplete)
        {
            if (_animating) return;
            _animating = true;

            _overlay.style.display = DisplayStyle.Flex;
            _overlay.style.opacity = 0f;
            _overlay.pickingMode = PickingMode.Position; // Block clicks during fade

            float elapsed = 0f;
            long lastTimeMs = CurrentTimeMs();

            IVisualElementScheduledItem anim = null;
            anim = _overlay.schedule.Execute(() =>
            {
                long now = CurrentTimeMs();
                float deltaTime = (now - lastTimeMs) / 1000f;
                lastTimeMs = now;

                elapsed += deltaTime;
                float t = elapsed / duration;

                if (t >= 1f)
                {
                    _overlay.style.opacity = 1f;
                    _animating = false;
                    anim?.Pause();
                    onComplete?.Invoke();
                    return;
                }

                _overlay.style.opacity = t;
            }).Every(16); // ~60fps tick rate
        }

        /// <summary>
        /// Fade from black (opacity 1 → 0). Calls onComplete when finished.
        /// Hides overlay and restores input when done.
        /// </summary>
        public void FadeIn(float duration, Action onComplete)
        {
            if (_animating) return;
            _animating = true;

            _overlay.style.display = DisplayStyle.Flex;
            _overlay.style.opacity = 1f;
            _overlay.pickingMode = PickingMode.Position; // Block clicks during fade

            float elapsed = 0f;
            long lastTimeMs = CurrentTimeMs();

            IVisualElementScheduledItem anim = null;
            anim = _overlay.schedule.Execute(() =>
            {
                long now = CurrentTimeMs();
                float deltaTime = (now - lastTimeMs) / 1000f;
                lastTimeMs = now;

                elapsed += deltaTime;
                float t = elapsed / duration;

                if (t >= 1f)
                {
                    _overlay.style.opacity = 0f;
                    _overlay.style.display = DisplayStyle.None;
                    _overlay.pickingMode = PickingMode.Ignore;
                    _animating = false;
                    anim?.Pause();
                    onComplete?.Invoke();
                    return;
                }

                _overlay.style.opacity = 1f - t;
            }).Every(16);
        }

        /// <summary>
        /// Immediately show the overlay at full opacity (no animation).
        /// </summary>
        public void ShowImmediate()
        {
            _overlay.style.display = DisplayStyle.Flex;
            _overlay.style.opacity = 1f;
            _overlay.pickingMode = PickingMode.Position;
        }

        /// <summary>
        /// Immediately hide the overlay (no animation).
        /// </summary>
        public void HideImmediate()
        {
            _overlay.style.display = DisplayStyle.None;
            _overlay.style.opacity = 0f;
            _overlay.pickingMode = PickingMode.Ignore;
            _animating = false;
        }

        private static long CurrentTimeMs()
        {
            return System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}
