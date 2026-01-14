using UnityEngine;
// Attached to Projectiles prefab. Handles live of projectiles and behaviour of forming platforms.
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class Projectiles : MonoBehaviour
{
    public GameObject platformPrefab;   // platform to generate

    float maxLifetime = 5f;     // fly for 5 seconds max
    float cavityY = 180f;       // the height where platforms should be
    float outOfBoundsY = 400f;  // limit from too high/low
    float minY = 100f;

    PlayerInventory inv;    // player inventory
    float spawnTime;        // to keep track of flying time
    [HideInInspector] public bool inCavity = false; // we can only generate platforms in cavity area

    void Start()
    {
        inv = FindFirstObjectByType<PlayerInventory>(); 
        // if (!inv) Debug.Log("no inv!");
        spawnTime = Time.time;
        // make sure it doesn’t collide with Player and disappear immediately
        Physics.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Player"), true);
    }

    void Update()
    {
        // destroy when flying too long / too high / too low
        if (Time.time - spawnTime > maxLifetime || transform.position.y > outOfBoundsY || transform.position.y < minY)
            Cleanup();

        // form platform when it hits cavity, at top of cavity height
        if (inCavity && transform.position.y <= cavityY + 0.05f)
        {
            SpawnPlatformAt(transform.position);
        }
    }

    void OnCollisionEnter(Collision col)
    {
        // Hit tree/ground/wall/etc -> disappear
        Cleanup();
    }

    void SpawnPlatformAt(Vector3 pos)
    {
        pos.y = cavityY;
        Instantiate(platformPrefab, pos, Quaternion.identity);
        Cleanup();
    }

    void Cleanup()
    {
        if (inv) inv.ProjectileInFlight = false;
        Destroy(gameObject);
    }
}
