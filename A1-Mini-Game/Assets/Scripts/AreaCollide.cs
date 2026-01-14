using UnityEngine;
// a file attached to ForestArea and GoalArea. To let AreaRegistry know where the player is at.
[RequireComponent(typeof(Collider))]
public class AreaCollide : MonoBehaviour
{
    public AreaType type; // set to Forest or Goal

    void Reset() { GetComponent<Collider>().isTrigger = true; }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) AreaRegistry.Enter(type);
    }
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) AreaRegistry.Exit(type);
    }
}
