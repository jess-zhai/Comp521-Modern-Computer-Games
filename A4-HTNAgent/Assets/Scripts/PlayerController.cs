using UnityEngine;
using TMPro;
// mainly adapted from Assignment 1
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 10.0f;
    public float mouseSensitivity = 5.0f;
    public float verticalLookLimit = 80.0f;
    public Transform cameraTransform;

    [Header("Player Stats")]
    public int maxLives = 2;
    public float invisibilityDuration = 10.0f;
    public KeyCode invisibilityToggle = KeyCode.Space;

    [Header("UI References (TMP)")]
    public TMP_Text livesText;
    public TMP_Text invisibilityText;
    public TMP_Text treasureText;
    public TMP_Text invisibleStatusText;

    private CharacterController characterController;
    private float verticalRotation = 0f;

    private int currentLives;
    private bool isInvisible = false;
    private float remainingInvisibilityTime = 0f;
    private int treasuresCollected = 0;

    private bool hasWon = false;
    private bool hasLost = false;

    private Renderer playerRenderer;
    private Color originalColor;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        playerRenderer = GetComponentInChildren<Renderer>(); 

        if (playerRenderer != null)
            originalColor = playerRenderer.material.color;

        currentLives = maxLives;
        isInvisible = false;
        remainingInvisibilityTime = invisibilityDuration;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        UpdateUI();
    }

    void Update()
    {
        // if game ended, stop processing input/movement
        if (hasWon || hasLost)
            return;

        HandleMouseLook();
        HandleMovement();
        HandleInvisibility();
    }

    void HandleMouseLook()
    {
        float horizontalRotation = Input.GetAxis("Mouse X") * mouseSensitivity;
        verticalRotation -= Input.GetAxis("Mouse Y") * mouseSensitivity;

        // horizontal rotation on player body
        transform.Rotate(0, horizontalRotation, 0);

        // vertical rotation on camera
        verticalRotation = Mathf.Clamp(verticalRotation, -verticalLookLimit, verticalLookLimit);
        cameraTransform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }

    void HandleMovement()
    {
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 horizontalMove = (forward * verticalInput + right * horizontalInput).normalized * moveSpeed;
        characterController.Move(horizontalMove * Time.deltaTime);
    }

    void HandleInvisibility()
    {
        // toggle invisibility only if there's still have time left
        if (Input.GetKeyDown(invisibilityToggle) && remainingInvisibilityTime > 0f)
        {
            isInvisible = !isInvisible;
            ApplyInvisibilityEffect();
        }

        // consume invisibility time when active
        if (isInvisible)
        {
            remainingInvisibilityTime -= Time.deltaTime;
            if (remainingInvisibilityTime <= 0f)
            {
                remainingInvisibilityTime = 0f;
                isInvisible = false;
                ApplyInvisibilityEffect();
            }
        }

        UpdateUI();
    }

    void ApplyInvisibilityEffect()
    {
        if (playerRenderer != null)
        {
            if (isInvisible)
            {
                Color invisibleColor = originalColor;
                invisibleColor.a = 0.3f;
                playerRenderer.material.color = invisibleColor;
            }
            else
            {
                playerRenderer.material.color = originalColor;
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // pick up treasure
        if (other.CompareTag("Treasure"))
        {
            treasuresCollected++;
            Destroy(other.gameObject);
            Debug.Log($"Treasure collected! Total: {treasuresCollected}");
            UpdateUI();
        }

        // win if returned to spawn with treasure
        if (other.CompareTag("Finish") && treasuresCollected > 0)
        {
            WinGame();
        }
    }

    public void TakeDamage()
    {
        if (hasWon || hasLost)
            return;

        currentLives--;
        UpdateUI();

        Debug.Log($"Player took damage! Lives remaining: {currentLives}");

        if (currentLives <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log("Player died! Game Over.");
        LoseGame();
    }

    void WinGame()
    {
        if (hasWon || hasLost)
            return;

        hasWon = true;
        Debug.Log("Player reached spawn with treasure! YOU WIN.");
        UpdateUI();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (GameFlow.I != null)
        {
            GameFlow.I.Win();
        }
    }

    void LoseGame()
    {
        if (hasWon || hasLost)
            return;

        hasLost = true;
        Debug.Log("Player has no lives left. YOU LOSE.");
        UpdateUI();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (GameFlow.I != null)
        {
            GameFlow.I.Lose();
        }
    }

    void UpdateUI()
    {
        if (livesText != null)
            livesText.text = $"Lives: {currentLives} / {maxLives}";

        if (invisibilityText != null)
            invisibilityText.text = $"Invisibility: {remainingInvisibilityTime:F2} s";

        if (invisibleStatusText != null)
            invisibleStatusText.text = $"Invisible: {(isInvisible ? "YES" : "NO")}";

        if (treasureText != null)
            treasureText.text = $"Treasures: {treasuresCollected}";
    }

    public bool IsInvisible => isInvisible;
    public int TreasuresCollected => treasuresCollected;
    public bool HasWon => hasWon;
    public bool HasLost => hasLost;
}
