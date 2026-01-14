using UnityEngine;
// Attached to GoalLandTrigger object, child in GoalLand prefab. the trigger that
// keeps track of which GoalLand tile we were on, and destroys that tile. 
public class GoalLandTrigger : MonoBehaviour
{
    public float dropDelay = 0.25f;

    static GoalLand current;  // last tile we stood on
    GoalLand me;

    void Awake()
    {
        me = GetComponentInParent<GoalLand>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (current && current != me)
        {
            current.Drop(dropDelay);    // destroy the tile we just left
        }
        current = me;
    }
}
