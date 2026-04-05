// MapCamera.cs
// Orthographic camera with smooth zoom/pan. Port of MapCamera.gd.
// Zoom: mouse wheel → lerp. Pan: WASD/arrows with acceleration.
// Pan speed normalized by zoom. Periodic X-wrap.

using UnityEngine;

namespace WarStrategy.Map
{
    [RequireComponent(typeof(Camera))]
    public class MapCamera : MonoBehaviour
    {
        public const float MAP_WIDTH = 16384f;
        public const float MAP_HEIGHT = 8192f;

        [Header("Zoom")]
        public float ZoomMin = 0.5f;
        public float ZoomMax = 40f;
        public float ZoomStep = 0.15f;
        public float ZoomLerp = 12f;

        [Header("Pan")]
        public float PanSpeed = 9000f;
        public float PanAccel = 14f;

        private Camera _cam;
        private float _targetZoom;
        private float _currentZoom;
        private Vector2 _velocity;

        // Middle-mouse drag
        private bool _dragging;
        private Vector3 _dragOrigin;

        /// <summary>
        /// When true, keyboard/mouse/scroll input is ignored.
        /// Used by GameFlowController to disable manual camera control during menus
        /// and by MenuCameraController during autopan.
        /// </summary>
        public bool InputLocked { get; set; }

        /// <summary>True while a PanTo coroutine is running.</summary>
        public bool IsPanning { get; private set; }

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;

            // Enforce zoom floor from the start — never wider than map
            float aspect = _cam.aspect > 0f ? _cam.aspect : 16f / 9f;
            float minZoom = GetMinZoom(aspect);

            _currentZoom = Mathf.Max(minZoom, 0.7f); // Start nicely framed
            _targetZoom = _currentZoom;
            _cam.orthographicSize = MAP_HEIGHT / (2f * _currentZoom);
            transform.position = new Vector3(MAP_WIDTH / 2f, -MAP_HEIGHT / 2f, -10f);
        }

        private void Update()
        {
            if (!InputLocked)
            {
                HandleZoom();
                HandleKeyboardPan();
                HandleMouseDrag();
            }
            WrapCamera();
            ClampVertical();
        }

        // ── Zoom (mouse wheel → smooth lerp) ──

        private float GetMinZoom(float aspect)
        {
            // View can't be wider than map: orthoSize * aspect * 2 <= MAP_WIDTH
            // View can't be taller than map: orthoSize * 2 <= MAP_HEIGHT
            // orthoSize = MAP_HEIGHT / (2 * zoom)
            // Width constraint: zoom >= MAP_HEIGHT * aspect / MAP_WIDTH
            // Height constraint: zoom >= 1.0 (orthoSize = MAP_HEIGHT/2 means entire map height visible)
            float widthMin = (MAP_HEIGHT * aspect) / MAP_WIDTH;
            float heightMin = 1.0f; // Entire map height visible at zoom=1
            return Mathf.Max(ZoomMin, widthMin, heightMin);
        }

        private void HandleZoom()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (scroll != 0f)
            {
                if (scroll > 0f)
                    _targetZoom *= (1f + ZoomStep);
                else
                    _targetZoom /= (1f + ZoomStep);

                float minZoom = GetMinZoom(_cam.aspect);
                _targetZoom = Mathf.Clamp(_targetZoom, minZoom, ZoomMax);
            }

            // Smooth lerp toward target with zoom-to-cursor correction
            if (Mathf.Abs(_currentZoom - _targetZoom) > 0.001f)
            {
                // Remember world position under mouse before zoom change
                Vector3 mouseBefore = _cam.ScreenToWorldPoint(Input.mousePosition);

                _currentZoom = Mathf.Lerp(_currentZoom, _targetZoom, ZoomLerp * Time.deltaTime);
                _cam.orthographicSize = MAP_HEIGHT / (2f * _currentZoom);

                // Correct camera position so the point under cursor stays fixed
                Vector3 mouseAfter = _cam.ScreenToWorldPoint(Input.mousePosition);
                transform.position += mouseBefore - mouseAfter;
            }
        }

        // ── Keyboard pan (WASD/arrows with acceleration) ──

        private void HandleKeyboardPan()
        {
            if (IsPanning) return;

            Vector2 dir = Vector2.zero;

            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) dir.x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) dir.x += 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) dir.y += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) dir.y -= 1f;

            // Normalize and scale by zoom (faster pan when zoomed out)
            Vector2 targetVel = dir.normalized * PanSpeed / _currentZoom;

            // Smooth acceleration
            _velocity = Vector2.Lerp(_velocity, targetVel, PanAccel * Time.deltaTime);

            if (_velocity.sqrMagnitude > 0.5f)
            {
                transform.position += new Vector3(_velocity.x, _velocity.y, 0) * Time.deltaTime;
            }
        }

        // ── Middle-mouse drag ──

        private void HandleMouseDrag()
        {
            if (Input.GetMouseButtonDown(2))
            {
                _dragging = true;
                _dragOrigin = _cam.ScreenToWorldPoint(Input.mousePosition);
            }

            if (Input.GetMouseButtonUp(2))
            {
                _dragging = false;
            }

            if (_dragging)
            {
                Vector3 currentPos = _cam.ScreenToWorldPoint(Input.mousePosition);
                Vector3 delta = _dragOrigin - currentPos;
                transform.position += delta;
            }
        }

        // ── Periodic X-wrap (keep near center tile) ──

        private void WrapCamera()
        {
            Vector3 pos = transform.position;
            if (pos.x < -MAP_WIDTH * 0.5f)
                pos.x += MAP_WIDTH;
            else if (pos.x > MAP_WIDTH * 1.5f)
                pos.x -= MAP_WIDTH;
            transform.position = pos;
        }

        // ── Y-bounds (no scrolling above/below map) ──

        private void ClampVertical()
        {
            Vector3 pos = transform.position;
            float halfViewH = _cam.orthographicSize;
            // Clamp so camera view never extends beyond map top (y=0) or bottom (y=-MAP_HEIGHT)
            float minY = -MAP_HEIGHT + halfViewH;
            float maxY = -halfViewH;
            // If view is taller than map (shouldn't happen with zoom lock), center it
            if (minY > maxY) { minY = maxY = -MAP_HEIGHT / 2f; }
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
            transform.position = pos;
        }

        // ── Public API ──

        public float CurrentZoom => _currentZoom;
        public Camera Camera => _cam;

        public void SyncZoomState()
        {
            _currentZoom = MAP_HEIGHT / (2f * _cam.orthographicSize);
            _targetZoom = _currentZoom;
        }

        /// <summary>
        /// Smoothly pan the camera to a world position over the given duration.
        /// targetZoom controls the final orthographic zoom level.
        /// </summary>
        public void PanTo(Vector2 worldTarget, float duration, float targetZoom)
        {
            StopAllCoroutines();
            StartCoroutine(PanToCoroutine(worldTarget, duration, targetZoom));
        }

        private System.Collections.IEnumerator PanToCoroutine(Vector2 target, float duration, float zoom)
        {
            IsPanning = true;
            Vector3 startPos = transform.position;
            Vector3 endPos = new Vector3(target.x, target.y, startPos.z);

            float startSize = _cam.orthographicSize;
            float endSize = MAP_HEIGHT / (2f * Mathf.Clamp(zoom, ZoomMin, ZoomMax));

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

                transform.position = Vector3.Lerp(startPos, endPos, t);
                _cam.orthographicSize = Mathf.Lerp(startSize, endSize, t);

                yield return null;
            }

            transform.position = endPos;
            _cam.orthographicSize = endSize;

            // Sync internal zoom state so manual zoom continues from here
            _currentZoom = MAP_HEIGHT / (2f * _cam.orthographicSize);
            _targetZoom = _currentZoom;
            IsPanning = false;
        }

        public Vector2 ScreenToMapUV(Vector2 screenPos)
        {
            Vector3 worldPos = _cam.ScreenToWorldPoint(screenPos);
            float x = worldPos.x % MAP_WIDTH;
            if (x < 0) x += MAP_WIDTH;
            float y = -worldPos.y; // 0 at top of map, MAP_HEIGHT at bottom

            // Unity texture UV: y=0 is bottom, y=1 is top
            // Top of map (y=0) → UV.y=1, Bottom of map (y=MAP_HEIGHT) → UV.y=0
            return new Vector2(x / MAP_WIDTH, 1.0f - (y / MAP_HEIGHT));
        }

        public Vector2 ScreenToMapWorld(Vector2 screenPos)
        {
            Vector3 worldPos = _cam.ScreenToWorldPoint(screenPos);
            float x = worldPos.x % MAP_WIDTH;
            if (x < 0) x += MAP_WIDTH;
            return new Vector2(x, -worldPos.y);
        }
    }
}
