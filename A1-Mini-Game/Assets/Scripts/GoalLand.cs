using UnityEngine;
// Attached to GoalLand objects, allows the cube with collider to destroy itself. Called in GoalLandTrigger.
public class GoalLand : MonoBehaviour
{
    public void Drop(float delay = 0.25f)
    {
        Destroy(gameObject, delay);
    }
}