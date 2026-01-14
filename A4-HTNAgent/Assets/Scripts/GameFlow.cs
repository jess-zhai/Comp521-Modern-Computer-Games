using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

// Reused from Assignment 1
public class GameFlow : MonoBehaviour
{
    public static GameFlow I { get; private set; }

    [SerializeField] string winScene = "Win";
    [SerializeField] string loseScene = "Lose";

    bool ended = false;

    void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Win()
    {
        if (ended) return;
        ended = true;

        UnlockCursor();
        StartCoroutine(LoadAndQuitAfterDelay(winScene));
    }

    public void Lose()
    {
        if (ended) return;
        ended = true;

        UnlockCursor();
        StartCoroutine(LoadAndQuitAfterDelay(loseScene));
    }

    void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    IEnumerator LoadAndQuitAfterDelay(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
        yield return new WaitForSeconds(5f);
        Application.Quit();
    }
}
