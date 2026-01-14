using UnityEngine;
using System.Collections.Generic;

public enum PathfindingMode
{
    NaiveRVG,
    ImprovedRDP
}

public class ImprovedPathfinder : MonoBehaviour
{
    [Header("Optimization Settings")]
    public float epsilon = 2.0f;
    public int maxRecursionDepth = 10; 
    public int terrainSamples = 5;

    Pathfinder pf;

    private void Start()
    {
        pf = FindObjectsByType<Pathfinder>(FindObjectsSortMode.None)[0];
    }

    public List<Vector3> FindPathWithRDP(Vector3 start, Vector3 goal, PathfindingMode mode)
    {
        //find base rvg first
        List<Vector3> basePath = pf.FindPath(start, goal);

        if (mode == PathfindingMode.NaiveRVG || basePath.Count <= 2)
            return basePath;

        return RecursivePathOptimization(basePath, 0, basePath.Count - 1, 0);
    }

    private List<Vector3> RecursivePathOptimization(List<Vector3> path, int startIdx, int endIdx, int depth)
    {
        if (endIdx <= startIdx + 1 || depth >= maxRecursionDepth)
        {
            return path.GetRange(startIdx, endIdx - startIdx + 1);
        }

        Vector3 startPoint = path[startIdx];
        Vector3 endPoint = path[endIdx];

        // check if we can directly link start and end, and if cost is better than original
        bool canConnectDirectly = IsDirectPathClear(startPoint, endPoint);
        float directCost = GetSegmentCost(startPoint, endPoint);
        float originalCost = GetPathSegmentCost(path, startIdx, endIdx);

        if (canConnectDirectly && directCost < originalCost * (1f - epsilon * 0.01f))
        {
            Debug.Log($"[Optimization] Direct connection improved: {originalCost:F2} → {directCost:F2} " +
                     $"(saved {endIdx - startIdx - 1} points)");

            List<Vector3> result = new List<Vector3>();
            result.Add(startPoint);
            result.Add(endPoint);
            return result;
        }

        int splitIndex = FindBestSplitPoint(path, startIdx, endIdx);

        // recurse left and right of middle point
        List<Vector3> leftPart = RecursivePathOptimization(path, startIdx, splitIndex, depth + 1);
        List<Vector3> rightPart = RecursivePathOptimization(path, splitIndex, endIdx, depth + 1);

        List<Vector3> resultPath = new List<Vector3>();
        resultPath.AddRange(leftPart);
        resultPath.AddRange(rightPart.GetRange(1, rightPart.Count - 1)); // skip duplicates

        return resultPath;
    }

    private int FindBestSplitPoint(List<Vector3> path, int startIdx, int endIdx)
    {
        Vector3 startPoint = path[startIdx];
        Vector3 endPoint = path[endIdx];

        int bestSplit = startIdx + 1;
        float bestScore = 0f;

        for (int i = startIdx + 1; i < endIdx; i++)
        {
            Vector3 currentPoint = path[i];

            float perpendicularDist = PerpendicularDistance(currentPoint, startPoint, endPoint);
            float terrainCost = SampleTerrainCostAroundPoint(currentPoint);
            float score = perpendicularDist * (1f + terrainCost * 0.3f);

            if (score > bestScore)
            {
                bestScore = score;
                bestSplit = i;
            }
        }

        Debug.Log($"[Optimization] Best split at index {bestSplit}, score: {bestScore:F3}");
        return bestSplit;
    }

    private float GetPathSegmentCost(List<Vector3> path, int startIdx, int endIdx)
    {
        float totalCost = 0f;
        for (int i = startIdx; i < endIdx; i++)
        {
            totalCost += GetSegmentCost(path[i], path[i + 1]);
        }
        return totalCost;
    }

    private float PerpendicularDistance(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
    {
        Vector3 lineDir = (lineEnd - lineStart).normalized;
        Vector3 pointDir = point - lineStart;
        float projection = Vector3.Dot(pointDir, lineDir);
        projection = Mathf.Clamp(projection, 0f, Vector3.Distance(lineStart, lineEnd));

        Vector3 closestPoint = lineStart + lineDir * projection;
        return Vector3.Distance(point, closestPoint);
    }

    private float SampleTerrainCostAroundPoint(Vector3 point)
    {
        LevelGenerator level = FindObjectsByType<LevelGenerator>(FindObjectsSortMode.None)[0];
        var regions = level.GetRegions();
        float totalCost = 0f;
        int validSamples = 0;

        for (int i = 0; i < terrainSamples; i++)
        {
            float angle = (i * 360f) / terrainSamples;
            Vector3 offset = Quaternion.Euler(0, angle, 0) * Vector3.forward * 0.5f;
            Vector3 samplePoint = point + offset;
            totalCost += GetTerrainCostAtPoint(samplePoint, regions);
            validSamples++;
        }

        return validSamples > 0 ? totalCost / validSamples : 1f;
    }


    private bool IsDirectPathClear(Vector3 from, Vector3 to)
    {
        Vector3 dir = (to - from).normalized;
        float distance = Vector3.Distance(from, to);

        RaycastHit[] hits = Physics.RaycastAll(from, dir, distance);
        foreach (var hit in hits)
        {
            if (hit.collider.CompareTag("Obstacle"))
            {
                Debug.Log($"[Collision] Ray blocked by: {hit.collider.name}");
                return false;
            }
        }

        return true;
    }

    public float GetSegmentCost(Vector3 a, Vector3 b)
    {
        LevelGenerator level = FindObjectsByType<LevelGenerator>(FindObjectsSortMode.None)[0];
        var regions = level.GetRegions();

        int samples = 10;
        float totalCost = 0f;

        for (int s = 0; s < samples; s++)
        {
            float t = (float)s / (samples - 1);
            Vector3 p = Vector3.Lerp(a, b, t);
            totalCost += GetTerrainCostAtPoint(p, regions);
        }

        return totalCost * Vector3.Distance(a, b) / samples;
    }

    public float GetTerrainCostAtPoint(Vector3 point, List<LevelGenerator.TerrainRegion> regions)
    {
        foreach (var region in regions)
        {
            if (region.bounds.Contains(new Vector2(point.x, point.z)))
            {
                return region.cost;
            }
        }
        return 1f; 
    }


}