using UnityEngine;

public class Fossil : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float interactionRadius = 3f;
    [SerializeField] private LayerMask playerLayer;
    
    [Header("UI Reference")]
    [SerializeField] private FossilCleaningGame cleaningGameUI;
    
    [Header("Debug Settings")]
    [SerializeField] private bool showDebugLogs = true;
    
    [Header("Fossil Data")]
    [SerializeField] private Sprite fossilSprite;
    [SerializeField] private int dirtParticleCount = 30;
    
    private Transform playerTransform;
    private bool playerInRange = false;
    private bool isInteracted = false;
    
    void Start()
    {
        // Find the player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            if (showDebugLogs)
                Debug.Log($"[Fossil] Found player: {player.name}");
        }
        else
        {
            if (showDebugLogs)
                Debug.LogWarning("[Fossil] No player found with 'Player' tag!");
        }
        
        // Make sure we have a cleaning game reference
        if (cleaningGameUI == null)
        {
            cleaningGameUI = FindObjectOfType<FossilCleaningGame>();
            if (showDebugLogs)
            {
                if (cleaningGameUI != null)
                    Debug.Log("[Fossil] Found FossilCleaningGame in scene");
                else
                    Debug.LogWarning("[Fossil] No FossilCleaningGame found in scene!");
            }
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
        bool wasInRange = playerInRange;
        playerInRange = distance <= interactionRadius;
        
        // Debug: Log when player enters or exits range
        if (showDebugLogs)
        {
            if (playerInRange && !wasInRange)
            {
                Debug.Log($"[Fossil] Player ENTERED range (Distance: {distance:F2} / Max: {interactionRadius})");
            }
            else if (!playerInRange && wasInRange)
            {
                Debug.Log($"[Fossil] Player LEFT range (Distance: {distance:F2} / Max: {interactionRadius})");
            }
        }
    }
    
    void CheckForClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        if (showDebugLogs)
            Debug.Log($"[Fossil] Mouse clicked! Player in range: {playerInRange}");
        
        if (Physics.Raycast(ray, out hit))
        {
            if (showDebugLogs)
                Debug.Log($"[Fossil] Raycast hit: {hit.collider.gameObject.name}");
            
            if (hit.collider.gameObject == gameObject)
            {
                if (showDebugLogs)
                    Debug.Log("[Fossil] Click detected on THIS fossil - Starting minigame!");
                StartCleaningMinigame();
            }
            else
            {
                if (showDebugLogs)
                    Debug.Log($"[Fossil] Click hit different object: {hit.collider.gameObject.name}");
            }
        }
        else
        {
            if (showDebugLogs)
                Debug.Log("[Fossil] Raycast didn't hit anything");
        }
    }
    
    void StartCleaningMinigame()
    {
        if (cleaningGameUI != null)
        {
            isInteracted = true;
            
            if (showDebugLogs)
                Debug.Log($"[Fossil] Starting cleaning minigame with {dirtParticleCount} dirt particles");
            
            cleaningGameUI.StartMinigame(fossilSprite, dirtParticleCount, OnCleaningComplete);
            
            // Pause player movement if needed
            PlayerController player = playerTransform?.GetComponent<PlayerController>();
            if (player != null)
            {
                player.enabled = false;
                if (showDebugLogs)
                    Debug.Log("[Fossil] Player movement disabled");
            }
        }
        else
        {
            if (showDebugLogs)
                Debug.LogError("[Fossil] Cannot start minigame - cleaningGameUI is null!");
        }
    }
    
    void OnCleaningComplete()
    {
        // Re-enable player movement
        PlayerController player = playerTransform?.GetComponent<PlayerController>();
        if (player != null)
        {
            player.enabled = true;
            if (showDebugLogs)
                Debug.Log("[Fossil] Player movement re-enabled");
        }
        
        // You can add effects, destroy the fossil, mark as collected, etc.
        if (showDebugLogs)
            Debug.Log("[Fossil] Cleaning complete! Destroying fossil.");
        
        // Optional: Destroy the fossil or mark it as collected
        Destroy(gameObject);
    }
    
    // Visualize the interaction radius in the editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}

