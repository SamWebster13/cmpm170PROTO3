using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerControllerCC_TopDown : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 6f;
    public float sprintMultiplier = 1.5f;
    public float jumpHeight = 1.5f;
    public float gravity = -20f;

    [Header("Camera")]
    public Transform cam; // Main Camera (top-down orthographic or perspective)

    [Header("Facing")]
    public bool faceMoveDirection = true;
    public float turnLerp = 0.2f;

    [Header("Interaction (optional)")]
    public float interactDistance = 3f;
    public LayerMask interactMask = ~0;

    CharacterController cc;
    Vector3 velocity; // only Y is used for vertical motion

    void Awake()
    {
        cc = GetComponent<CharacterController>();

        // For top-down, you usually don't want locked cursor:
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        Move();
        // If you want click-to-interact later, you can call TryInteract() here on input.
    }

    void Move()
    {
        bool grounded = cc.isGrounded;
        if (grounded && velocity.y < 0f) velocity.y = -2f; // keep grounded

        // Input
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        Vector2 input = new Vector2(x, z);
        if (input.sqrMagnitude > 1f) input.Normalize();

        // Camera-relative planar axes (so WASD matches camera view)
        Vector3 camF = Vector3.forward;
        Vector3 camR = Vector3.right;

        if (cam != null)
        {
            camF = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            camR = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
        }

        Vector3 planar = (camF * input.y + camR * input.x);
        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1f);

        // Apply gravity
        velocity.y += gravity * Time.deltaTime;

        // Compose final motion
        Vector3 motion = planar * speed * Time.deltaTime;
        motion += velocity * Time.deltaTime; // vertical

        cc.Move(motion);

        // Optional: face the direction you’re moving (planar only)
        if (faceMoveDirection && planar.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(planar);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnLerp);
        }
    }

    // Optional mouse-based interact for later:
    // Cast a ray from camera to mouse cursor, then check distance to player.
    bool TryInteract()
    {
        if (cam == null) return false;
        if (!Input.GetKeyDown(KeyCode.E)) return false;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, interactMask))
        {
            if (Vector3.Distance(transform.position, hit.point) <= interactDistance)
            {
                // TODO: call a component on hit.collider
                // hit.collider.GetComponent<IInteractable>()?.Use();
                return true;
            }
        }
        return false;
    }
}
