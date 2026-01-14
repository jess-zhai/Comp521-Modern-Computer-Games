using UnityEngine;
using System.Collections.Generic;

public class ObstacleGenerator : MonoBehaviour
{

    private int cylinderCount = 3;
    private int triangularPrismCount = 4;

    private int maxAttempts = 100;

    public float minX = -11f, maxX = 18f;
    public float minZ = -11f, maxZ = 11f;
    private float boardY = 1f;

    [Header("Prefabs")]
    public GameObject cylinderPrefab;
    public GameObject triangularPrismPrefab;

    private float minDistanceBetweenObstacles = 1f;
    private float minDistanceFromWall = 0.6f;
    private float prismXRotation = 90f;
    public Vector2 prismZRotationRange = new Vector2(10f, 80f);

    struct Placed { public GameObject go; public float radiusXZ; }
    private readonly List<Placed> placed = new();
    private const float kEps = 1e-3f;

    void Start()
    {
        GenerateObstacles();
    }

    public void GenerateObstacles()
    {
        // cleanup
        foreach (var p in placed) if (p.go) Destroy(p.go);
        placed.Clear();

        // cylinders
        {
            int target = Mathf.Max(0, cylinderCount);
            int placedThisType = 0;
            int attemptsTotal = 0;

            Quaternion rot = Quaternion.identity;
            float r = ComputePrefabXZRadiusUsingMesh(cylinderPrefab, rot);

            while (placedThisType < target && attemptsTotal < maxAttempts)
            {
                attemptsTotal++;

                if (TryFindValidPosition(r, out Vector3 pos))
                {
                    var go = Instantiate(cylinderPrefab, pos, rot);
                    placed.Add(new Placed { go = go, radiusXZ = r });
                    placedThisType++;
                }
                Debug.Log("try failed");
            }

            if (placedThisType < target)
                Debug.LogWarning($"Cylinders placed {placedThisType}/{target} after {attemptsTotal} attempts.");
        }

        // triangular prisms
        {
            int target = Mathf.Max(0, triangularPrismCount);
            int placedThisType = 0;
            int attemptsTotal = 0;

            while (placedThisType < target && attemptsTotal < maxAttempts * Mathf.Max(1, target))
            {
                attemptsTotal++;

                // try a new z rotation
                float zAbs = Random.Range(prismZRotationRange.x, prismZRotationRange.y);
                Quaternion rot = Quaternion.Euler(prismXRotation, 0f, zAbs);
                float r = ComputePrefabXZRadiusUsingMesh(triangularPrismPrefab, rot);

                if (TryFindValidPosition(r, out Vector3 pos))
                {
                    var go = Instantiate(triangularPrismPrefab, pos, rot);
                    placed.Add(new Placed { go = go, radiusXZ = r });
                    placedThisType++;
                }
                Debug.Log("try failed");
            }

            if (placedThisType < target)
                Debug.LogWarning($"Prisms placed {placedThisType}/{target} after {attemptsTotal} attempts.");
        }
    }

    float ComputePrefabXZRadiusUsingMesh(GameObject prefab, Quaternion rotation)
    {
        if (!prefab) return 1f;

        GameObject tmp = Instantiate(prefab);
        tmp.hideFlags = HideFlags.HideAndDontSave;
        tmp.transform.position = Vector3.zero;
        tmp.transform.rotation = rotation;
        tmp.transform.localScale = prefab.transform.localScale;

        float maxR = 0f;

        var mfs = tmp.GetComponentsInChildren<MeshFilter>(true);
        foreach (var mf in mfs)
        {
            var mesh = mf.sharedMesh;
            if (!mesh) continue;

            var trs = mf.transform;
            var verts = mesh.vertices;
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 w = trs.TransformPoint(verts[i]);
                float r = Mathf.Sqrt(w.x * w.x + w.z * w.z);
                if (r > maxR) maxR = r;
            }
        }

        // if no mesh, return collider bounds
        if (maxR <= 0f)
        {
            var cols = tmp.GetComponentsInChildren<Collider>(true);
            foreach (var col in cols)
            {
                Bounds b = col.bounds;
                // approximate
                float r = Mathf.Max(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x), Mathf.Abs(b.min.z), Mathf.Abs(b.max.z));
                if (r > maxR) maxR = r;
            }
            if (maxR <= 0f) maxR = 1f;
        }

        DestroyImmediate(tmp);
        return maxR;
    }

    bool TryFindValidPosition(float obstacleRadius, out Vector3 position)
    {
        // calc usable locations
        float marginX = minDistanceFromWall + obstacleRadius;
        float marginZ = minDistanceFromWall + obstacleRadius;

        float availableMinX = minX + marginX;
        float availableMaxX = maxX - marginX;
        float availableMinZ = minZ + marginZ;
        float availableMaxZ = maxZ - marginZ;

        // check for enough room
        if (availableMinX >= availableMaxX || availableMinZ >= availableMaxZ)
        {
            position = Vector3.zero;
            return false;
        }

        int localAttempts = 0;
        while (localAttempts++ < maxAttempts)
        {
            // random location
            float x = Random.Range(availableMinX, availableMaxX);
            float z = Random.Range(availableMinZ, availableMaxZ);
            position = new Vector3(x, boardY, z);

            if (!OverlapsPlaced(position, obstacleRadius)) return true; 
        }

        position = Vector3.zero;
        return false;
    }

    bool OverlapsPlaced(Vector3 pos, float r)
    {
        Vector2 p = new Vector2(pos.x, pos.z);
        foreach (var it in placed)
        {
            if (!it.go) continue;
            Vector3 c = it.go.transform.position;
            Vector2 q = new Vector2(c.x, c.z);
            float d = Vector2.Distance(p, q);
            // find overlaps between obstacles
            float sum = r + it.radiusXZ + minDistanceBetweenObstacles;

            if (d + kEps < sum) return true;
        }
        return false;
    }

    //float ComputeObjectXZRadiusFast(GameObject obj)
    //{
    //    float maxR = 0f;
    //    var mfs = obj.GetComponentsInChildren<MeshFilter>(true);
    //    foreach (var mf in mfs)
    //    {
    //        var mesh = mf.sharedMesh;
    //        if (!mesh) continue;
    //        var trs = mf.transform;
    //        foreach (var v in mesh.vertices)
    //        {
    //            Vector3 w = trs.TransformPoint(v);
    //            float r = Mathf.Sqrt(w.x * w.x + w.z * w.z);
    //            if (r > maxR) maxR = r;
    //        }
    //    }
    //    if (maxR <= 0f) maxR = 3.0f;
    //    return maxR;
    //}

    //void OnDrawGizmosSelected()
    //{
    //    Gizmos.color = new Color(1, 0.5f, 0, 0.25f);
    //    Vector3 center = new Vector3((minX + maxX) * 0.5f, boardY, (minZ + maxZ) * 0.5f);
    //    Vector3 size = new Vector3(maxX - minX, 0.1f, maxZ - minZ);
    //    Gizmos.DrawCube(center, size);

    //    Gizmos.color = Color.yellow;
    //    Gizmos.DrawWireCube(center, size);

    //    Gizmos.color = Color.green;
    //    foreach (var p in placed)
    //    {
    //        if (!p.go) continue;
    //        DrawCircleXZ(p.go.transform.position, p.radiusXZ, 32);
    //    }
    //}

    //static void DrawCircleXZ(Vector3 c, float r, int seg)
    //{
    //    Vector3 prev = c + new Vector3(r, 0f, 0f);
    //    for (int i = 1; i <= seg; i++)
    //    {
    //        float ang = (i * Mathf.PI * 2f) / seg;
    //        Vector3 p = c + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
    //        Gizmos.DrawLine(prev, p);
    //        prev = p;
    //    }
    //}
}
