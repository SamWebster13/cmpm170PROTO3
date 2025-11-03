using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 720f;
    
    private Rigidbody rb;
    private Camera mainCamera;
    private Vector3 moveDirection;
    
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.freezeRotation = true;
        }
        mainCamera = Camera.main;
    }
    
    void Update()
    {
        // Get input (WASD or Arrow keys)
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        
        // Store movement direction
        moveDirection = new Vector3(horizontal, 0f, vertical).normalized;
        
        // Rotate player to face mouse position
        RotateTowardsMouse();
    }
    
    void FixedUpdate()
    {
        // Move the player in FixedUpdate for smooth Rigidbody movement
        if (rb != null)
        {
            rb.MovePosition(rb.position + moveDirection * moveSpeed * Time.fixedDeltaTime);
        }
    }
    
    void RotateTowardsMouse()
    {
        if (mainCamera == null) return;
        
        // Create a ray from camera through mouse position
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        
        // Create a plane at the player's y position
        Plane groundPlane = new Plane(Vector3.up, transform.position);
        
        // Raycast to find where mouse points on the ground plane
        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 mouseWorldPosition = ray.GetPoint(distance);
            Vector3 directionToMouse = (mouseWorldPosition - transform.position).normalized;
            directionToMouse.y = 0f; // Keep it horizontal
            
            if (directionToMouse.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToMouse);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
    }
}

