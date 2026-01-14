using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class RVGGenerator : MonoBehaviour
{
    private float edgeExtension = 0.5f;
    private float raycastHeight = 1.2f;
    public bool autoGenerate = true;

    [Header("Debug Visualization")]
    public bool showExtendedLines = true;
    public bool showAcceptedEdges = true;
    public bool showRejectedEdges = false;
    public bool showCostLabels = false;

    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<(int, int, float)> edges = new List<(int, int, float)>(); // includes cost
    private readonly List<(Vector3, Vector3, bool)> debugEdges = new List<(Vector3, Vector3, bool)>();

    private Transform startPoint;
    private Transform goalPoint;

    private int lastStartIndex = -1;
    private int lastGoalIndex = -1;

    public PathfindingMode displayMode = PathfindingMode.NaiveRVG;

    void OnEnable()
    {
        if (autoGenerate)
            Invoke(nameof(GenerateBaseRVG), 0.2f);
    }

    [ContextMenu("Generate Base RVG")]
    public void GenerateBaseRVG()
    {
        vertices.Clear();
        edges.Clear();
        debugEdges.Clear();

        ObstacleGeometry[] geos = FindObjectsByType<ObstacleGeometry>(FindObjectsSortMode.None);

        foreach (var geo in geos)
        {
            var verts = geo.GetOffsetWorldVerticesXZ(0.01f);
            foreach (var v2 in verts)
            {
                vertices.Add(new Vector3(v2.x, raycastHeight, v2.y));
            }
        }

        for (int i = 0; i < vertices.Count; i++)
        {
            for (int j = i + 1; j < vertices.Count; j++)
            {
                bool isVisible = IsVisible(vertices[i], vertices[j]);
                if (isVisible)
                {
                    float cost = ComputeEdgeCost(vertices[i], vertices[j]);
                    edges.Add((i, j, cost));
                }

                debugEdges.Add((vertices[i], vertices[j], isVisible));
            }
        }

        lastStartIndex = -1;
        lastGoalIndex = -1;
        Debug.Log($"[RVG] Base graph built: {vertices.Count} vertices, {edges.Count} edges.");
    }

    public void AddDynamicPoints(Transform start, Transform goal)
    {
        RemoveLastDynamicPoints();

        startPoint = start;
        goalPoint = goal;

        int startIndex = -1, goalIndex = -1;

        if (startPoint != null)
        {
            startIndex = vertices.Count;
            vertices.Add(new Vector3(startPoint.position.x, raycastHeight, startPoint.position.z));
        }
        if (goalPoint != null)
        {
            goalIndex = vertices.Count;
            vertices.Add(new Vector3(goalPoint.position.x, raycastHeight, goalPoint.position.z));
        }

        List<int> newPoints = new List<int>();
        if (startIndex >= 0) newPoints.Add(startIndex);
        if (goalIndex >= 0) newPoints.Add(goalIndex);

        foreach (int i in newPoints)
        {
            for (int j = 0; j < vertices.Count; j++)
            {
                if (i == j) continue;
                bool isVisible = IsVisible(vertices[i], vertices[j]);
                if (isVisible)
                {
                    float cost = ComputeEdgeCost(vertices[i], vertices[j]);
                    edges.Add((i, j, cost));
                }

                debugEdges.Add((vertices[i], vertices[j], isVisible));
            }
        }

        lastStartIndex = startIndex;
        lastGoalIndex = goalIndex;
        //Debug.Log($"[RVG] Added agent/goal nodes. Total vertices={vertices.Count}, edges={edges.Count}");
    }

    public void RemoveLastDynamicPoints()
    {
        if (lastStartIndex < 0 && lastGoalIndex < 0) return;

        List<int> toRemove = new List<int>();
        if (lastStartIndex >= 0 && lastStartIndex < vertices.Count) toRemove.Add(lastStartIndex);
        if (lastGoalIndex >= 0 && lastGoalIndex < vertices.Count) toRemove.Add(lastGoalIndex);

        if (toRemove.Count == 0) return;

        edges.RemoveAll(e => toRemove.Contains(e.Item1) || toRemove.Contains(e.Item2));
        debugEdges.RemoveAll(d => toRemove.Exists(idx => d.Item1 == vertices[idx] || d.Item2 == vertices[idx]));

        toRemove.Sort((a, b) => b.CompareTo(a));
        foreach (int idx in toRemove)
        {
            if (idx >= 0 && idx < vertices.Count)
                vertices.RemoveAt(idx);
        }

        lastStartIndex = -1;
        lastGoalIndex = -1;
    }

    private float ComputeEdgeCost(Vector3 a, Vector3 b)
    {
        float dist = Vector3.Distance(a, b);

        LevelGenerator level = FindObjectsByType<LevelGenerator>(FindObjectsSortMode.None)[0];
        var regions = level.GetRegions();

        // sample terrain cost along the segment
        int samples = 10;
        float totalCost = 0f;

        for (int s = 0; s < samples; s++)
        {
            float t = (s + 0.5f) / samples;
            Vector3 p = Vector3.Lerp(a, b, t);
            float localCost = 1f;
            foreach (var region in regions)
            {
                if (region.bounds.Contains(new Vector2(p.x, p.z)))
                {
                    localCost = region.cost;
                    break;
                }
            }
            totalCost += localCost;
        }

        float avgCost = totalCost / samples;
        return dist * avgCost; // weighted movement cost
    }

    private bool IsVisible(Vector3 a, Vector3 b)
    {
        Vector3 dir = (b - a).normalized;
        Vector3 rayStart = a - dir * edgeExtension;
        Vector3 rayEnd = b + dir * edgeExtension;
        float rayLength = Vector3.Distance(rayStart, rayEnd);

        RaycastHit[] hits = Physics.RaycastAll(rayStart, dir, rayLength);
        System.Array.Sort(hits, (a1, a2) => a1.distance.CompareTo(a2.distance));

        foreach (var hit in hits)
        {
            if (!hit.collider.CompareTag("Obstacle"))
                continue;
            return false;
        }

        RaycastHit[] hits2 = Physics.RaycastAll(rayEnd, -dir, rayLength);
        System.Array.Sort(hits2, (a1, a2) => a1.distance.CompareTo(a2.distance));

        foreach (var hit in hits2)
        {
            if (!hit.collider.CompareTag("Obstacle"))
                continue;
            return false;
        }

        return true;
    }

    private void OnDrawGizmos()
    {
        if (vertices == null || vertices.Count == 0) return;

        Gizmos.color = Color.cyan;
        foreach (var v in vertices)
            Gizmos.DrawSphere(v, 0.12f);

        if (showAcceptedEdges)
        {
            // color based on mode
            Gizmos.color = (displayMode == PathfindingMode.NaiveRVG) ? Color.black : new Color(0.5f, 0.25f, 0f); // brown

            foreach (var (i, j, cost) in edges)
            {
                if (i >= vertices.Count || j >= vertices.Count) continue;
                Gizmos.DrawLine(vertices[i], vertices[j]);
#if UNITY_EDITOR
                if (showCostLabels)
                {
                    Vector3 mid = (vertices[i] + vertices[j]) * 0.5f;
                    UnityEditor.Handles.Label(mid + Vector3.up * 0.2f, cost.ToString("F1"));
                }
#endif
            }
        }

        if (showRejectedEdges && debugEdges.Count > 0)
        {
            Gizmos.color = Color.red;
            foreach (var (a, b, isVisible) in debugEdges)
            {
                if (!isVisible)
                    Gizmos.DrawLine(a, b);
            }
        }
    }

    public List<Vector3> GetVertices() => vertices;
    public List<(int, int, float)> GetEdges() => edges;
}
