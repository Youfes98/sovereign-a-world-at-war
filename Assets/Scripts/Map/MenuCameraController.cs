// MenuCameraController.cs
// Slow sinusoidal autopan for the main menu background.
// Added/enabled by GameFlowController during MainMenu phase.
// MapCamera.InputLocked must be true while this is active.

using UnityEngine;

namespace WarStrategy.Map
{
    [RequireComponent(typeof(Camera))]
    public class MenuCameraController : MonoBehaviour
    {
        [Header("Pan")]
        [Tooltip("Horizontal pan speed in world units per second.")]
        public float panSpeed = 200f;

        [Header("Zoom Oscillation")]
        [Tooltip("Minimum orthographic zoom level.")]
        public float zoomMin = 1.5f;

        [Tooltip("Maximum orthographic zoom level.")]
        public float zoomMax = 2.5f;

        [Tooltip("Full oscillation period in seconds.")]
        public float zoomPeriod = 20f;

        private Camera _cam;
        private float _zoomTime;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
        }

        private void OnEnable()
        {
            if (_cam == null)
                _cam = GetComponent<Camera>();

            // Reset oscillation phase so zoom starts smoothly from current size
            // Solve for t: currentSize = MAP_HEIGHT / (2 * zoom) where zoom = midpoint
            // Start at the midpoint of the oscillation so there's no jarring snap
            _zoomTime = 0f;

            // Set initial zoom to the midpoint
            float midZoom = (zoomMin + zoomMax) * 0.5f;
            _cam.orthographicSize = MapCamera.MAP_HEIGHT / (2f * midZoom);
        }

        private void Update()
        {
            if (_cam == null) return;

            // ── Horizontal pan (continuous rightward drift, wraps at map edge) ──
            Vector3 pos = transform.position;
            pos.x += panSpeed * Time.deltaTime;

            // Periodic X-wrap — keep camera in valid range
            if (pos.x > MapCamera.MAP_WIDTH * 1.5f)
                pos.x -= MapCamera.MAP_WIDTH;
            else if (pos.x < -MapCamera.MAP_WIDTH * 0.5f)
                pos.x += MapCamera.MAP_WIDTH;

            transform.position = pos;

            // ── Zoom oscillation (sinusoidal between zoomMin and zoomMax) ──
            _zoomTime += Time.deltaTime;

            // sin wave: -1..1 mapped to zoomMin..zoomMax
            float t = Mathf.Sin(_zoomTime * 2f * Mathf.PI / zoomPeriod);
            float zoom = Mathf.Lerp(zoomMin, zoomMax, (t + 1f) * 0.5f);

            _cam.orthographicSize = MapCamera.MAP_HEIGHT / (2f * zoom);
        }
    }
}
