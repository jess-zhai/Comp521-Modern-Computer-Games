using UnityEngine;
// Attached to MainCamera. Handles shooting projectile.
public class Shoot : MonoBehaviour
{
    public GameObject projectilePrefab;
    public float speed = 35f; // flying speed

    PlayerInventory inv;

    void Start()
    {
        inv = FindFirstObjectByType<PlayerInventory>();
        if (!inv) Debug.Log("no inv");
    }

    void Update()
    {
        if (!Input.GetButtonDown("Fire1")) return; // left mouse click
        //Debug.Log("firing");
        if (inv.ProjectileInFlight) return; // only one at a time
        if (!inv.ConsumeOne()) return; // must have at least one pickups in hand

        // spawn a projectile from the position of camera (center of screen), moving in the direction that the camera is viewing at
        var go = Instantiate(projectilePrefab, transform.position, transform.rotation); 
        if (go.TryGetComponent<Rigidbody>(out var rb))
        {
            // update the location of the projectile
            rb.linearVelocity = transform.forward * speed;
        }
        inv.ProjectileInFlight = true;
    }
}
