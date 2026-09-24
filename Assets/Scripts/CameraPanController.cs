using UnityEngine;
using UnityEngine.EventSystems;

// Geser-geser kamera drag pakai jari/mouse (map ngikutin jari, bukan kamera yang "ngesot" arbitrer),
// pinch buat zoom, plus batas area biar kamera gak bisa digeser keluar map.
[RequireComponent(typeof(Camera))]
public class CameraPanController : MonoBehaviour
{
    [Header("Pan")]
    [Tooltip("Tinggi bidang tanah (world Y) yang dipakai buat itung drag. Samain kira-kira sama tinggi map-nya.")]
    [SerializeField] private float groundHeight = 0f;
    [SerializeField] private bool useInertia = true;
    [Tooltip("Makin gede makin cepet berhenti pas abis di-drag.")]
    [SerializeField] private float inertiaDamping = 4f;

    [Header("Batas Area")]
    [SerializeField] private bool clampToBounds = true;
    [Tooltip("Batas geser kamera di sumbu X dan Z (world space).")]
    [SerializeField] private Vector2 minBounds = new Vector2(-20f, -20f);
    [SerializeField] private Vector2 maxBounds = new Vector2(20f, 20f);

    [Header("Zoom (opsional)")]
    [SerializeField] private bool enableZoom = true;
    [SerializeField] private float pinchZoomSpeed = 0.02f;
    [SerializeField] private float scrollZoomSpeed = 1f;
    [SerializeField] private float minOrthoSize = 3f;
    [SerializeField] private float maxOrthoSize = 15f;
    [SerializeField] private float minFov = 20f;
    [SerializeField] private float maxFov = 70f;

    private Camera cam;
    private Plane groundPlane;
    private bool dragging;
    private Vector3 lastGroundPoint;
    private Vector3 velocity;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        groundPlane = new Plane(Vector3.up, new Vector3(0f, groundHeight, 0f));
    }

    private void Update()
    {
        HandlePan();
        if (enableZoom) HandleZoom();
        if (clampToBounds) ClampPosition();
    }

    private void HandlePan()
    {
        if (IsPointerOverUI()) return;

        if (Input.GetMouseButtonDown(0))
        {
            dragging = TryGetGroundPoint(Input.mousePosition, out lastGroundPoint);
            velocity = Vector3.zero;
        }
        else if (Input.GetMouseButton(0) && dragging)
        {
            if (TryGetGroundPoint(Input.mousePosition, out Vector3 current))
            {
                Vector3 delta = lastGroundPoint - current;
                transform.position += delta;
                velocity = delta / Mathf.Max(Time.deltaTime, 0.0001f);
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            dragging = false;
        }
        else if (!dragging && useInertia && velocity.sqrMagnitude > 0.0001f)
        {
            transform.position += velocity * Time.deltaTime;
            velocity = Vector3.Lerp(velocity, Vector3.zero, inertiaDamping * Time.deltaTime);
        }
    }

    private void HandleZoom()
    {
        if (Input.touchCount == 2)
        {
            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);
            Vector2 prevT0 = t0.position - t0.deltaPosition;
            Vector2 prevT1 = t1.position - t1.deltaPosition;

            float prevDist = (prevT0 - prevT1).magnitude;
            float curDist = (t0.position - t1.position).magnitude;

            Zoom((curDist - prevDist) * pinchZoomSpeed);
        }
        else
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f) Zoom(scroll * scrollZoomSpeed * 10f);
        }
    }

    private void Zoom(float amount)
    {
        if (cam.orthographic)
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize - amount, minOrthoSize, maxOrthoSize);
        else
            cam.fieldOfView = Mathf.Clamp(cam.fieldOfView - amount, minFov, maxFov);
    }

    private void ClampPosition()
    {
        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, minBounds.x, maxBounds.x);
        pos.z = Mathf.Clamp(pos.z, minBounds.y, maxBounds.y);
        transform.position = pos;
    }

    private bool TryGetGroundPoint(Vector3 screenPos, out Vector3 point)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (groundPlane.Raycast(ray, out float enter))
        {
            point = ray.GetPoint(enter);
            return true;
        }
        point = Vector3.zero;
        return false;
    }

    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;
        if (Input.touchCount > 0) return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        return EventSystem.current.IsPointerOverGameObject();
    }

    private void OnDrawGizmosSelected()
    {
        if (!clampToBounds) return;
        Gizmos.color = Color.yellow;
        Vector3 center = new Vector3((minBounds.x + maxBounds.x) / 2f, groundHeight, (minBounds.y + maxBounds.y) / 2f);
        Vector3 size = new Vector3(maxBounds.x - minBounds.x, 0.05f, maxBounds.y - minBounds.y);
        Gizmos.DrawWireCube(center, size);
    }
}
