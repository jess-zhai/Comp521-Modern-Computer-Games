using UnityEngine;
// Attached to Player object. Keeps track of the number of Pickups the player has.
public class PlayerInventory : MonoBehaviour
{
    [SerializeField] int startCount = 0;
    public int Count { get; private set; }
    public bool ProjectileInFlight { get; set; } = false; // only 1 projectile could fly at a time

    public System.Action<int> OnCountChanged; // hook UI

    void Awake() { Count = startCount; }

    public bool HasAny() => Count > 0;

    // will be called in Pickups script, adds n (default to 1) pickups to inventory
    public void Add(int n = 1)
    {
        Count += n;
        OnCountChanged?.Invoke(Count);
    }

    // will be called in Projectiles script, consumes 1 if have pickups in hand.
    public bool ConsumeOne()
    {
        if (Count <= 0) return false;
        Count--;
        OnCountChanged?.Invoke(Count);
        return true;
    }
}
