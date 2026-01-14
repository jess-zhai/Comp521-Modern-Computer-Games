using UnityEngine;
// Attached to Platform Prefab. Destroys the platform, this will be triggered in StepTrigger.
public class PlatformDie : MonoBehaviour
{
    public void Kill(float delay = 0.25f)
    {
        Destroy(gameObject, delay);
    }
}
