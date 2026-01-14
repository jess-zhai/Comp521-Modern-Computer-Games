using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(BoxCollider))]
public class ObstacleSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject boulderPrefab;
    public GameObject mushroomPrefab;

    private int boulderMinCount = 10;
    private int boulderMaxCount = 20;
    private int mushroomMinCount = 5;
    private int mushroomMaxCount = 10;
    public float minScale = 0.5f;
    public float maxScale = 1.3f;

    [Header("Placement")]
    public LayerMask avoidMask;
    public float fallbackRadius = 1.0f;
    public int maxAttempts = 1000;
    public float boulderY = 1.25f;
    public float mushroomY = 0.35f;
    public static List<Transform> Mushrooms = new List<Transform>();
    public static HashSet<Transform> ReservedMushrooms = new HashSet<Transform>();
    public static List<Transform> Boulders = new List<Transform>();

    [Header("Debug")]
    public bool showDebugSpheres = true;
    public float debugSphereHeight = 0.2f;

    private BoxCollider area;
    private List<GameObject> spawned = new List<GameObject>();

    private float boulderRadius;
    private float mushroomRadius;

    void Awake()
    {
        area = GetComponent<BoxCollider>();
        // For boulders, estimate with the same rotation they'll be spawned with
        boulderRadius = EstimateRadiusFromPrefab(boulderPrefab, Quaternion.Euler(0f, 0f, 90f));
        mushroomRadius = EstimateRadiusFromPrefab(mushroomPrefab, Quaternion.identity);

        Debug.Log($"Boulder radius: {boulderRadius}, Mushroom radius: {mushroomRadius}");
    }

    void Start()
    {
        int boulderCount = Random.Range(boulderMinCount, boulderMaxCount + 1);
        int mushroomCount = Random.Range(mushroomMinCount, mushroomMaxCount + 1);
        // spawn boulders and mushrooms
        SpawnMany(boulderPrefab, boulderCount, boulderRadius, boulderY, isBoulder: true);
        SpawnMany(mushroomPrefab, mushroomCount, mushroomRadius, mushroomY, isBoulder: false);
    }

    public static Transform GetNearestBoulder(Vector3 fromPos)
    {
        Transform best = null;
        float bestSqr = float.PositiveInfinity;

        foreach (var t in Boulders)
        {
            if (t == null) continue;

            float sqr = (t.position - fromPos).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = t;
            }
        }

        return best;
    }

    public static Transform GetNearestFreeMushroom(Vector3 fromPos)
    {
        Transform best = null;
        float bestSqr = float.PositiveInfinity;

        foreach (var t in Mushrooms)
        {
            if (t == null) continue;
            if (ReservedMushrooms.Contains(t)) continue; // already taken, prevent both ogre taking one mushroom

            // find nearest
            float sqr = (t.position - fromPos).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = t;
            }
        }
        if (best != null)
        {
            ReservedMushrooms.Add(best);
        }

        return best;
    }

    public static void RemoveMushroom(Transform t)
    {
        Mushrooms.Remove(t);
        ReservedMushrooms.Remove(t);
    }

    public static void ReleaseMushroom(Transform t)
    {
        ReservedMushrooms.Remove(t);
    }

    float EstimateRadiusFromPrefab(GameObject prefab, Quaternion rotation)
    {
        if (prefab == null)
            return fallbackRadius;

        // temporarily instantiate with the rotation it will be spawned at
        GameObject temp = Instantiate(prefab, Vector3.zero, rotation);

        float radius = fallbackRadius;
        // use collider to find bound
        Collider col = temp.GetComponentInChildren<Collider>();
        if (col != null)
        {
            // get bounds in world space
            Vector3 ext = col.bounds.extents;
            // find XZ plane footprint
            radius = Mathf.Max(ext.x, ext.z);
            Debug.Log($"Collider bounds for {prefab.name} (rotation {rotation.eulerAngles}): extents={ext}, calculated radius={radius}");
        }

        Destroy(temp);
        return radius;
    }

    void SpawnMany(GameObject prefab, int count, float baseRadius, float fixedY, bool isBoulder)
    {
        if (!prefab || !area) return;

        var bounds = area.bounds;
        Vector3 center = bounds.center;
        Vector3 ext = bounds.extents;

        int created = 0;
        int guard = 0;
        int rejectedByDistance = 0;

        while (created < count && guard < maxAttempts)
        {
            guard++;

            // decide scale & radius for curr candidate
            float scale = 1f;
            float radius = baseRadius;

            if (isBoulder)
            {
                // random size for boulders
                scale = Random.Range(minScale, maxScale);
                radius = baseRadius * scale;
            }

            Vector3 spawnPos = new Vector3(
                Random.Range(center.x - ext.x, center.x + ext.x),
                fixedY*scale,
                Random.Range(center.z - ext.z, center.z + ext.z)
            );

            // check within arena bound
            if (!IsInsideBounds(spawnPos, radius))
            {
                rejectedByDistance++;
                continue;
            }

            // check distance from already spawned objects
            bool tooClose = false;
            foreach (GameObject existingObj in spawned)
            {
                if (existingObj == null) continue;

                float existingRadius = GetRadiusForObject(existingObj);
                Vector3 existingPos = existingObj.transform.position;

                float distance = Vector3.Distance(
                    new Vector3(spawnPos.x, 0, spawnPos.z),
                    new Vector3(existingPos.x, 0, existingPos.z)
                );

                float minDistance = (radius + existingRadius) * 1.5f;
                if (distance < minDistance)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose)
            {
                rejectedByDistance++;
                continue;
            }

            // rotation
            Quaternion rot = isBoulder
                ? Quaternion.Euler(0f, Random.Range(-30f, 30f), 90f)
                : Quaternion.identity;

            GameObject obj = Instantiate(prefab, spawnPos, rot);

            if (isBoulder)
            {
                obj.transform.localScale *= scale;
                ObstacleSpawner.Boulders.Add(obj.transform);
            }
            else
            {
                Mushrooms.Add(obj.transform);
            }

            spawned.Add(obj);
            created++;
        }

        Debug.Log($"Spawned {created}/{count} {prefab.name}. Rejected: {rejectedByDistance}, Attempts: {guard}");
    }

    float GetRadiusForObject(GameObject obj)
    {
        if (obj.name.Contains("Boulder")) return boulderRadius;
        if (obj.name.Contains("Mushroom")) return mushroomRadius;
        return fallbackRadius;
    }

    bool IsInsideBounds(Vector3 position, float radius)
    {
        var bounds = area.bounds;
        Vector3 center = bounds.center;
        Vector3 ext = bounds.extents;

        if (position.x - radius < center.x - ext.x) return false;
        if (position.x + radius > center.x + ext.x) return false;
        if (position.z - radius < center.z - ext.z) return false;
        if (position.z + radius > center.z + ext.z) return false;
        return true;
    }


    void OnDrawGizmos()
    {
        if (!area) area = GetComponent<BoxCollider>();
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(area.bounds.center, area.bounds.size);
        if (showDebugSpheres && Application.isPlaying)
        {
            Gizmos.color = new Color(1, 0, 0);
            foreach (GameObject obj in spawned)
            {
                if (obj != null)
                {
                    float radius = obj.name.Contains("Boulder") ? boulderRadius * 1.5f : mushroomRadius * 1.5f;
                    Vector3 gizmoPos = new Vector3(obj.transform.position.x, debugSphereHeight, obj.transform.position.z);
                    Gizmos.DrawWireSphere(gizmoPos, radius);
                }
            }
        }
    }
}