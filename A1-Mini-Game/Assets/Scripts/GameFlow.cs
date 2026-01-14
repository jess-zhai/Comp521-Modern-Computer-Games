using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
// Attached to GameFlowController (empty object), switches scene when player wins/loses and quits game.
public class GameFlow : MonoBehaviour
{
    public static GameFlow I { get; private set; }

    [SerializeField] string winScene = "Win";
    [SerializeField] string loseScene = "Lose";

    bool ended = false;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
    }
    // load the win screen, wait for 5 seconds and quit.
    public void Win()
    {
        if (ended) return;
        ended = true;
        UnlockCursor();
        SceneManager.LoadScene(winScene); 
        StartCoroutine(WaitRoutine());
        Application.Quit();
    }
    // loads the lose screen, wait for 5 seconds and quit.
    public void Lose()
    {
        if (ended) return;
        ended = true;
        UnlockCursor();
        SceneManager.LoadScene(loseScene);
        StartCoroutine(WaitRoutine());
        Application.Quit();
    }
 
    void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // got from unity official tutorial
    IEnumerator WaitRoutine()
    {
        //yield on a new YieldInstruction that waits for 5 seconds.
        yield return new WaitForSeconds(5);

    }
}
