using UnityEngine;
// Attached to the Player. Handles leaving trailmarks behind.
public class TrailMarkDropper : MonoBehaviour
{
    public Transform cameraTransform;
    public GameObject trailMarkPrefab;  // the trailmark object
    public float stepDistance = 2f;     // leave one every 2 units
    public LayerMask groundMask;        // include Ground only to drop only on it, not on trees etc.

    CharacterController cc; 
    Vector3 lastDropPos;
    Transform marksParent;  // store everything in this to keep tidy

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        marksParent = new GameObject("TrailMarks").transform;
    }

    void Start()
    {
        // init it to spawning position of player
        lastDropPos = transform.position;
    }

    void Update()
    {
        if (AreaRegistry.Current != AreaType.Forest) return; // only leave in forest area
        if (!cc.isGrounded) return;     // don't leave in mid air

        var a = new Vector3(transform.position.x, 0, transform.position.z);
        var b = new Vector3(lastDropPos.x, 0, lastDropPos.z);
        if (Vector3.Distance(a, b) >= stepDistance) // only leave again after some distance form last mark
        {
            DropOneMark();
            lastDropPos = transform.position;
        }
    }

    void DropOneMark()
    {
        
        float topOfCapsule = cc.height * 0.5f + 0.2f;   // prevent hitting ourselves
        Vector3 origin = transform.position + Vector3.up * topOfCapsule;
        // cast ray down from just above the controller to get slope of the ground
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 20f, groundMask, QueryTriggerInteraction.Ignore))
        {
            // align marks to surface normal
            Quaternion align = Quaternion.FromToRotation(Vector3.forward, hit.normal);

            // lift slightly to avoid stuck in ground and gittering
            Vector3 pos = hit.point + hit.normal * 0.02f;

            Instantiate(trailMarkPrefab, pos, align, marksParent);
        }
    }
}
