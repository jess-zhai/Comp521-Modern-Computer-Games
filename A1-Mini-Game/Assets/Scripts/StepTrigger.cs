using UnityEngine;
// Attached to StepTrigger object under Platform Prefab. Handles disappearance of platforms and close of forest wall.
public class StepTrigger : MonoBehaviour
{
    [SerializeField] float destroyDelay = 0.25f; // prevent destroy from gliches
    static bool forestClosedOnce = false;        // we close forest wall as soon as the player steps on the first platform.
    bool playerInside = false;  
    PlatformDie platform;

    void Awake()
    {
        platform = GetComponentInParent<PlatformDie>();
        //if (!platform) Debug.Log("no platform found");
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInside = true;    // player is on platform

        // close the forest gate only the first time any platform is stepped on
        if (!forestClosedOnce)
        {
            forestClosedOnce = true;
            GateManager.I.CloseForest();
            Debug.Log("Forest gate closed");
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (!playerInside) return;

        // delete platform after player leaves
        platform.Kill(destroyDelay);
    }
}
