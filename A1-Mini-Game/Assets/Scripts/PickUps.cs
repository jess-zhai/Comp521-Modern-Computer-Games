using UnityEngine;
// Attached to Pickups prefab. A trigger that as soons as player collides with it,
// it destroys itself and adds one to player's inventory.
[RequireComponent(typeof(Collider))]
public class Pickups : MonoBehaviour
{
    void Reset() { GetComponent<Collider>().isTrigger = true; }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        other.GetComponent<PlayerInventory>().Add(1);
        Destroy(gameObject);
    }
}
