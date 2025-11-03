using UnityEngine;

public class Fossil : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float interactionRadius = 3f;
    
    private FossilType currentFossilType;
    private Transform playerTransform;
    private bool playerInRange = false;
    private bool isInteracted = false;
    
    void Start()
    {
        // Get a random fossil type from the manager
        if (FossilGameManager.Instance != null)
        {
            currentFossilType = FossilGameManager.Instance.GetRandomFossilType();
        }
        
        // Find the player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }
    
    void Update()
    {
        if (isInteracted) return;
        
        // Check if player is in range
        CheckPlayerInRange();
        
        // Check for mouse click when player is in range
        if (playerInRange && Input.GetMouseButtonDown(0))
        {
            CheckForClick();
        }
    }
    
    void CheckPlayerInRange()
    {
        if (playerTransform == null) return;
        
        float distance = Vector3.Distance(transform.position, playerTransform.position);
        playerInRange = distance <= interactionRadius;
    }
    
    void CheckForClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit) && hit.collider.gameObject == gameObject)
        {
            StartCleaningMinigame();
        }
    }
    
    void StartCleaningMinigame()
    {
        if (FossilGameManager.Instance == null) return;
        
        isInteracted = true;
        
        // Disable player movement
        PlayerController player = playerTransform?.GetComponent<PlayerController>();
        if (player != null)
        {
            player.enabled = false;
        }
        
        // Start the cleaning game through the manager
        FossilGameManager.Instance.StartCleaningGame(currentFossilType, OnCleaningComplete);
    }
    
    void OnCleaningComplete()
    {
        // Re-enable player movement
        PlayerController player = playerTransform?.GetComponent<PlayerController>();
        if (player != null)
        {
            player.enabled = true;
        }
        
        // Destroy the fossil
        Destroy(gameObject);
    }
    
    // Visualize the interaction radius in the editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
