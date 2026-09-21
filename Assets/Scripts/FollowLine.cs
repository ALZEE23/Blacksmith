using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

[RequireComponent(typeof(SplineContainer))]
public class FollowLine : MonoBehaviour
{
    public enum EndBehaviour { Stop, Loop, PingPong }

    [SerializeField] private GameObject npcPrefab;
    [SerializeField] private float speed = 1f;
    [SerializeField] private EndBehaviour onEnd = EndBehaviour.Stop;
    [SerializeField] private bool faceMoveDirection = true;

    [Header("Checkpoint")]
    [Tooltip("Index knot (mulai dari 0) tempat NPC berhenti. Knot 3 di editor = index 3.")]
    [SerializeField] private List<int> checkpointKnots = new List<int>();
    [SerializeField] private float doubleClickTime = 0.3f;

    private SplineContainer container;
    private Rigidbody npcBody;
    private float length;
    private float distance;
    private int direction = 1;
    private readonly List<float> checkpointDistances = new List<float>();
    private bool waiting;
    private bool resumeRequested;
    private float lastClickTime = -10f;

    private void Awake()
    {
        container = GetComponent<SplineContainer>();
    }

    private void Start()
    {
        if (npcPrefab == null)
        {
            Debug.LogWarning("FollowLine: npcPrefab belum di-assign", this);
            enabled = false;
            return;
        }

        length = container.CalculateLength();
        Vector3 startPos = container.EvaluatePosition(0f);
        GameObject npc = Instantiate(npcPrefab, startPos, Quaternion.identity);

        npcBody = npc.GetComponent<Rigidbody>();
        if (npcBody == null)
        {
            Debug.LogWarning("FollowLine: npcPrefab tidak punya Rigidbody", this);
            enabled = false;
            return;
        }

        Spline spline = container.Spline;
        foreach (int knot in checkpointKnots)
        {
            if (knot < 0 || knot >= spline.Count)
            {
                Debug.LogWarning($"FollowLine: checkpoint knot {knot} di luar range", this);
                continue;
            }
            float t = spline.ConvertIndexUnit(knot, PathIndexUnit.Knot, PathIndexUnit.Normalized);
            checkpointDistances.Add(t * length);
        }
    }

    private void Update()
    {
        if (!waiting || !Input.GetMouseButtonDown(0)) return;

        if (Time.time - lastClickTime <= doubleClickTime)
        {
            resumeRequested = true;
            lastClickTime = -10f;
        }
        else
        {
            lastClickTime = Time.time;
        }
    }

    private void FixedUpdate()
    {
        if (length <= 0f) return;

        if (waiting)
        {
            if (!resumeRequested) return;
            waiting = false;
            resumeRequested = false;
        }

        float next = distance + direction * speed * Time.fixedDeltaTime;

        if (TryGetCheckpoint(distance, next, out float checkpoint))
        {
            distance = checkpoint;
            waiting = true;
        }
        else
        {
            distance = next;
            HandleEnd();
        }

        float t = distance / length;
        npcBody.MovePosition(container.EvaluatePosition(t));

        if (faceMoveDirection)
        {
            Vector3 tangent = (Vector3)container.EvaluateTangent(t) * direction;
            tangent.y = 0f;
            if (tangent.sqrMagnitude > 0.0001f)
                npcBody.MoveRotation(Quaternion.LookRotation(tangent));
        }
    }

    private bool TryGetCheckpoint(float from, float to, out float result)
    {
        result = 0f;
        bool found = false;
        float best = float.MaxValue;

        foreach (float cp in checkpointDistances)
        {
            bool crossed = direction > 0 ? (cp > from && cp <= to) : (cp < from && cp >= to);
            if (!crossed) continue;

            float dist = Mathf.Abs(cp - from);
            if (dist < best)
            {
                best = dist;
                result = cp;
                found = true;
            }
        }
        return found;
    }

    private void HandleEnd()
    {
        if (distance < length && distance > 0f) return;

        switch (onEnd)
        {
            case EndBehaviour.Stop:
                distance = Mathf.Clamp(distance, 0f, length);
                break;
            case EndBehaviour.Loop:
                distance = Mathf.Repeat(distance, length);
                break;
            case EndBehaviour.PingPong:
                distance = Mathf.Clamp(distance, 0f, length);
                direction = -direction;
                break;
        }
    }
}
