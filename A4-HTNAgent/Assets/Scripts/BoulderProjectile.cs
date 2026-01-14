using UnityEngine;

public class BoulderProjectile : MonoBehaviour
{
    private bool hasHit;
    private float lifeTimer = 0f;
    private const float MAX_LIFETIME = 10f;

    // called right after the ogre throws the boulder
    public void Initialize(PlayerController player)
    {
        hasHit = false;
        lifeTimer = 0f;
    }

    private void Update()
    {
        // destroy boulder after max lifetime (in case it gets stuck)
        lifeTimer += Time.deltaTime;
        if (lifeTimer >= MAX_LIFETIME)
        {
            Debug.Log("[BoulderProjectile] Destroyed due to timeout");
            Destroy(gameObject);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // if already processed a hit, do nothing
        if (hasHit) return;

        // ignore ogre collision
        if (collision.collider.CompareTag("Ogre"))
        {
            Debug.Log("[BoulderProjectile] Grazed ogre, ignoring collision");
            return;
        }

        hasHit = true;

        // deal damage if hitting player
        var pc = collision.collider.GetComponent<PlayerController>();
        if (pc == null)
            pc = collision.collider.GetComponentInParent<PlayerController>();

        if (pc != null)
        {
            pc.TakeDamage();
            Debug.Log("[BoulderProjectile] Hit player!");
        }
        else
        {
            Debug.Log($"[BoulderProjectile] Hit {collision.collider.name}, destroying");
        }

        // destroy on collision with anything that isn't an ogre
        Destroy(gameObject);
    }
}