using UnityEngine;
using System.Collections;
// Attached to KillArea, trigger Game End (lose) when player falls to it
[RequireComponent(typeof(Collider))]
public class AreaKillCollide : MonoBehaviour
{
    void Reset() { GetComponent<Collider>().isTrigger = true; }

    // if it detects the player, it triggers the gameover. 
    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        GameFlow.I.Lose();
        //Debug.Log("Game Over: Fell into cavity");
    }

}
