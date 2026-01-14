using UnityEngine;
// Attached to GoalArea, to know that the player has entered the goal area and ban
// going back to cavity area.
[RequireComponent(typeof(Collider))]
public class CavityGateCloser : MonoBehaviour
{
    void Reset() { GetComponent<Collider>().isTrigger = true; }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        GateManager.I.CloseGoal(); // ban going from Goal to cavity
        //Debug.Log("closed cavity");
    }
}
