using UnityEngine;
// Attached to GateManager (empty object), first disables all gates to let player progress, then close
// gates when player reaches certain area. e.g. close forest gate to prevent player from going from cavity
// back to forest, and close cavity gate to prevent player form going from goal to cavity. 
public class GateManager : MonoBehaviour
{
    public static GateManager I { get; private set; }
    [SerializeField] Collider forestGate;
    [SerializeField] Collider cavityGate;

    bool forestClosed, cavityClosed;

    void Awake() { I = this; DisableAll(); }

    void DisableAll()
    {
        if (forestGate) forestGate.enabled = false;
        if (cavityGate) cavityGate.enabled = false;
        forestClosed = cavityClosed = false;
    }

    public void CloseForest()
    {
        if (forestClosed) return;
        if (forestGate) forestGate.enabled = true;
        forestClosed = true;
        Debug.Log("Forest gate closed.");
    }

    public void CloseGoal()
    {
        if (cavityClosed) return;
        if (cavityGate) cavityGate.enabled = true;
        cavityClosed = true;
        Debug.Log("Goal gate closed.");
    }
}
