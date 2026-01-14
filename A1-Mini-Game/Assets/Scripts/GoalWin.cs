using UnityEngine;
// Attached to GoalTrigger Object, child of Goal object. Detects if the player is in the actual goal,
// and if so, triggers game win.
[RequireComponent(typeof(Collider))]
public class GoalWin : MonoBehaviour
{
    void Reset() { GetComponent<Collider>().isTrigger = true; }
    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        GameFlow.I.Win();
    }
}
