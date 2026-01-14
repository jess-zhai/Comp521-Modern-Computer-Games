using UnityEngine;
// Attached to CavityArea, allows and limits projectiles to form platforms only within this area.
public class CavityArea : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<Projectiles>(out var p)) p.inCavity = true;
    }
    void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<Projectiles>(out var p)) p.inCavity = false;
    }
}
