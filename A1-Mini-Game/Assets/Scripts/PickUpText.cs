using UnityEngine;
using TMPro;
// attached to Text (TMP) object under Canvas. displays to user how many pickups they have in hand.
public class PickupUI : MonoBehaviour
{
    [SerializeField] PlayerInventory inventory;
    [SerializeField] TMP_Text label;

    void Start()
    {
        inventory.OnCountChanged += UpdateText;
        UpdateText(inventory.Count);
    }
    void UpdateText(int c)
    {
        label.text = $"Pickups: {c}";
    }
}
