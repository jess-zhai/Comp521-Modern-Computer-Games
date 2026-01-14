using UnityEngine;
// Attached to Player object. the movement controller of the player and camera.
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 10.0f; // move speed of player
    public float mouseSensitivity = 5.0f; // sensitivity of rotating
    public float verticalLookLimit = 80.0f; // limit the vertical look angle
    public Transform cameraTransform; // the camera

    public float gravity = -10f;

    private CharacterController characterController;
    private float verticalRotation = 0f; // vertical rotation of camera
    private Vector3 moveDirection = Vector3.zero; // initialize move direction to be 0 vector

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        // Lock and hide cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // process look and move every frame
        HandleMouseLook();
        HandleMovement();
    }

    void HandleMouseLook()
    {
        // get mouse input
        float horizontalRotation = Input.GetAxis("Mouse X") * mouseSensitivity;
        verticalRotation -= Input.GetAxis("Mouse Y") * mouseSensitivity;

        // horizontal movement of mouse: character rotates sideways
        transform.Rotate(0, horizontalRotation, 0);
        // vertical movement of mouse: camera rotates up and down
        verticalRotation = Mathf.Clamp(verticalRotation, -verticalLookLimit, verticalLookLimit); // don't want camera to flip upside down
        cameraTransform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }

    void HandleMovement()
    {
        // get input
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        // get components of direction of movement
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        // make sure they're not affected by previous frame and also the slope
        forward.y = 0;
        right.y = 0;
        forward.Normalize();
        right.Normalize();

        // combine components of horizontal direction of movement
        Vector3 horizontalMove = (forward * verticalInput + right * horizontalInput).normalized * moveSpeed;

        // set horizontal components
        moveDirection.x = horizontalMove.x;
        moveDirection.z = horizontalMove.z;

        // handle gravity
        if (characterController.isGrounded && moveDirection.y < 0)
        {
            moveDirection.y = -1f;
        }
        else
        {
            moveDirection.y += gravity * Time.deltaTime;
        }

        // move character
        characterController.Move(moveDirection * Time.deltaTime);

    }

}