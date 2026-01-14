using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class LevelGenerator : MonoBehaviour
{
    [Header("Board Settings")]
    public GameObject plane;
    public GameObject[] obstaclePrefabs;
    public int minObstacles = 8;
    public int maxObstacles = 12;

    // padding to border edge
    public float edgePadding = 6.0f;

    private readonly List<GameObject> obstacles = new List<GameObject>();
    private Vector3 planeSize;
    private Vector3 planeCenter;

    [System.Serializable]
    public struct TerrainRegion
    {
        public Rect bounds;
        public float cost;
    }

    private List<TerrainRegion> regions = new List<TerrainRegion>();
    public List<TerrainRegion> GetRegions() => regions;
    void Start()
    {
        var planeRenderer = plane.GetComponent<Renderer>();
        planeSize = planeRenderer.bounds.size;
        planeCenter = planeRenderer.bounds.center;

        GenerateObstacles();
        DivideSubAreas();
    }

    void GenerateObstacles()
    {
        int numObstacles = Random.Range(minObstacles, maxObstacles + 1);
        //Debug.Log($"Try to generate {numObstacles} obstacles");

        int safetyGlobal = 0;
        int placed = 0;

        while (placed < numObstacles && safetyGlobal < 1500)
        {
            safetyGlobal++;

            GameObject prefab = obstaclePrefabs[Random.Range(0, obstaclePrefabs.Length)];
            Vector3 pos = GetRandomPosition();
            Quaternion rot = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

            GameObject candidate = Instantiate(prefab, pos, rot);

            // Compute geometry
            ObstacleGeometry geo = candidate.GetComponent<ObstacleGeometry>();
            if (geo == null)
            {
                DestroyImmediate(candidate);
                continue;
            }

            geo.CalculateGeometryData();
            Vector3 worldCenter = candidate.transform.TransformPoint(
                new Vector3(geo.BoundingCenterLocal.x, 0f, geo.BoundingCenterLocal.y)
            );
            float r = geo.BoundingRadius;

            if (!InsideBoard(worldCenter, r))
            {
                DestroyImmediate(candidate);
                continue;
            }

            // Expanded spacing check: small padding to avoid clipping
            if (!NonOverlapping(worldCenter, r, 1.0f))
            {
                DestroyImmediate(candidate);
                continue;
            }

            obstacles.Add(candidate);
            placed++;
        }

        //Debug.Log($"successfully placed {placed}/{numObstacles} obstacles after {safetyGlobal} attempts.");
    }


    Vector3 GetRandomPosition()
    {
        float x = Random.Range(
            planeCenter.x - planeSize.x / 2 + edgePadding,
            planeCenter.x + planeSize.x / 2 - edgePadding);
        float z = Random.Range(
            planeCenter.z - planeSize.z / 2 + edgePadding,
            planeCenter.z + planeSize.z / 2 - edgePadding);
        return new Vector3(x, 1f, z);
    }

    bool InsideBoard(Vector3 position, float radius)
    {
        if (position.x - radius < planeCenter.x - planeSize.x / 2 + edgePadding) return false;
        if (position.x + radius > planeCenter.x + planeSize.x / 2 - edgePadding) return false;
        if (position.z - radius < planeCenter.z - planeSize.z / 2 + edgePadding) return false;
        if (position.z + radius > planeCenter.z + planeSize.z / 2 - edgePadding) return false;
        return true;
    }

    bool NonOverlapping(Vector3 candidateCenter, float candidateRadius, float minGap = 0.5f)
    {
        foreach (var obs in obstacles)
        {
            var geo = obs.GetComponent<ObstacleGeometry>();
            if (geo == null) continue;

            Vector3 otherCenter = obs.transform.TransformPoint(
                new Vector3(geo.BoundingCenterLocal.x, 0f, geo.BoundingCenterLocal.y)
            );
            float otherR = geo.BoundingRadius;

            float dist = Vector3.Distance(candidateCenter, otherCenter);
            if (dist < candidateRadius + otherR + minGap)
                return false;
        }
        return true;
    }



    void DivideSubAreas()
    {
        regions.Clear();

        int rows = 2;
        int cols = 3;
        float subWidth = planeSize.x / cols;
        float subHeight = planeSize.z / rows;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                float x = planeCenter.x - planeSize.x / 2 + subWidth * col;
                float z = planeCenter.z - planeSize.z / 2 + subHeight * row;
                Rect rect = new Rect(x, z, subWidth, subHeight);

                TerrainRegion region = new TerrainRegion
                {
                    bounds = rect,
                    cost = Random.Range(0.5f, 5.0f)
                };
                regions.Add(region);
            }
        }

        //Debug.Log($"Generated {regions.Count} terrain regions with variable walking costs.");
    }


    public List<GameObject> GetObstacles() => obstacles;
}
