using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class Pathfinder : MonoBehaviour
{
    private RVGGenerator rvg;

    void Awake()
    {
        rvg = FindObjectsByType<RVGGenerator>(FindObjectsSortMode.None)[0];
    }

    public List<Vector3> FindPath(Vector3 startPos, Vector3 goalPos)
    {
        List<Vector3> vertices = rvg.GetVertices();
        var edges = rvg.GetEdges();

        // find closest rvg edges to start and end
        int startIdx = GetNearestVertex(startPos, vertices);
        int goalIdx = GetNearestVertex(goalPos, vertices);

        // A* data structures
        var openSet = new HashSet<int> { startIdx };
        var cameFrom = new Dictionary<int, int>();
        var gScore = new Dictionary<int, float>();
        var fScore = new Dictionary<int, float>();

        for (int i = 0; i < vertices.Count; i++)
        {
            gScore[i] = float.PositiveInfinity;
            fScore[i] = float.PositiveInfinity;
        }

        gScore[startIdx] = 0f;
        fScore[startIdx] = Heuristic(vertices[startIdx], vertices[goalIdx]);

        while (openSet.Count > 0)
        {
            // get node with lowest fScore
            int current = openSet.OrderBy(n => fScore[n]).First();

            if (current == goalIdx)
                return ReconstructPath(cameFrom, current, vertices);

            openSet.Remove(current);

            // explore neighbors
            foreach (var (i, j, cost) in edges)
            {
                int neighbor = (i == current) ? j :
                               (j == current) ? i : -1;
                if (neighbor == -1) continue;

                float tentativeG = gScore[current] + cost;

                if (tentativeG < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    fScore[neighbor] = tentativeG + Heuristic(vertices[neighbor], vertices[goalIdx]);
                    openSet.Add(neighbor);
                }
            }
        }

        //Debug.LogWarning("A* failed to find path.");
        return new List<Vector3>();
    }

    private List<Vector3> ReconstructPath(Dictionary<int, int> cameFrom, int current, List<Vector3> vertices)
    {
        List<Vector3> path = new List<Vector3> { vertices[current] };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Insert(0, vertices[current]);
        }
        return path;
    }

    private int GetNearestVertex(Vector3 pos, List<Vector3> verts)
    {
        int best = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i < verts.Count; i++)
        {
            float d = Vector3.Distance(pos, verts[i]);
            if (d < bestDist)
            {
                bestDist = d;
                best = i;
            }
        }
        return best;
    }

    private float Heuristic(Vector3 a, Vector3 b)
    {
        float dist = Vector3.Distance(a, b);

        LevelGenerator level = FindObjectsByType<LevelGenerator>(FindObjectsSortMode.None)[0];
        var regions = level.GetRegions();

        int samples = 10;
        float totalCost = 0f;

        for (int s = 0; s < samples; s++)
        {
            float t = (float)s / (samples - 1);
            Vector3 p = Vector3.Lerp(a, b, t);

            bool foundRegion = false;
            foreach (var region in regions)
            {
                if (region.bounds.Contains(new Vector2(p.x, p.z)))
                {
                    totalCost += region.cost;
                    foundRegion = true;
                    break;
                }
            }

            if (!foundRegion)
                totalCost += 1f;
        }

        float avgCost = totalCost / samples;
        return dist * avgCost;
    }
}
