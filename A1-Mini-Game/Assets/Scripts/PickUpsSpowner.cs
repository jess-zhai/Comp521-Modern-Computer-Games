using UnityEngine;
// Attached to ForestArea object. Spawns 10 Pickups at the start of the game. 
public class PickUpsSpawner : MonoBehaviour
{
    public GameObject pickupPrefab;
    public int count = 10;
    public LayerMask treeMask;     // colliders not to overlap, i.e. trees
    public LayerMask groundMask;    // colliders to be on
    public float avoidRadius = 1f;

    BoxCollider area;   // the ForestArea object's collider
    private int offset = 100;   // bound to smaller part inside ForestArea collider, to prevent 1. spawning in mountains, 2. player accidentally
    // fall to cavity / trigger forest gate when trying to pick up Pickups. 
    private int maxSlopeAngle = 45; // the same as player controller, further prevent generation on mountains.

    void Awake()
    {
        area = GetComponent<BoxCollider>();
    }

    void Start()
    {
        if (!pickupPrefab) return;
        var center = area.bounds.center;
        var ext = area.bounds.extents;

        int spawned = 0, guard = 0;
        while (spawned < count && guard < 2000) // generates 10; prevent stuck in while loop
        {
            guard++;
            Vector3 p = new Vector3( // random possible point of generation of pickup
                Random.Range(center.x - ext.x + offset, center.x + ext.x - offset), // smaller than whole of forest area
                center.y + ext.y + 2f, // slightly from top of ground
                Random.Range(center.z - ext.z + offset, center.z + ext.z - offset)
            );

            // shoots a lightray to the bottom, ensures the pickup must be generated on a ground (e.g. not cavity or some holes)
            if (!Physics.Raycast(p, Vector3.down, out var hit, 100f, groundMask, QueryTriggerInteraction.Ignore))
                continue;

            // avoid trees
            if (Physics.CheckSphere(hit.point, avoidRadius, treeMask, QueryTriggerInteraction.Ignore))
                continue;

            // prevent it to generate on steep parts (which is possibly mountains)
            float slope = Vector3.Angle(hit.normal, Vector3.up);
            if (slope > maxSlopeAngle) continue;

            // if everything is ok, generates pickup a bit up from ground to prevent clipping
            Instantiate(pickupPrefab, hit.point + Vector3.up * 0.5f, Quaternion.identity);
            spawned++;
        }
    }
}
