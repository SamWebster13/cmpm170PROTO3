using UnityEngine;

public class FossilSpawner : MonoBehaviour
{
    [Header("Spawning Settings")]
    [SerializeField] private GameObject fossilPrefab;
    [SerializeField] private int numberOfFossils = 5;
    [SerializeField] private Vector2 spawnAreaSize = new Vector2(50f, 50f);
    [SerializeField] private float spawnHeight = 0.5f;
    [SerializeField] private LayerMask groundLayer;
    
    [Header("Spawn on Start")]
    [SerializeField] private bool spawnOnStart = true;
    
    void Start()
    {
        if (spawnOnStart)
        {
            SpawnFossils();
        }
    }
    
    public void SpawnFossils()
    {
        if (fossilPrefab == null)
        {
            Debug.LogWarning("Fossil prefab is not assigned!");
            return;
        }
        
        for (int i = 0; i < numberOfFossils; i++)
        {
            SpawnFossil();
        }
    }
    
    void SpawnFossil()
    {
        // Generate random position within spawn area
        float randomX = Random.Range(-spawnAreaSize.x / 2f, spawnAreaSize.x / 2f);
        float randomZ = Random.Range(-spawnAreaSize.y / 2f, spawnAreaSize.y / 2f);
        
        Vector3 spawnPosition = transform.position + new Vector3(randomX, spawnHeight, randomZ);
        
        // Raycast down to find ground
        RaycastHit hit;
        if (Physics.Raycast(spawnPosition + Vector3.up * 10f, Vector3.down, out hit, 100f, groundLayer))
        {
            spawnPosition = hit.point + Vector3.up * spawnHeight;
        }
        
        // Spawn the fossil
        GameObject fossil = Instantiate(fossilPrefab, spawnPosition, Quaternion.identity);
        
        // Random rotation for variety
        fossil.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
    }
    
    // Visualize spawn area in editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, new Vector3(spawnAreaSize.x, 0.1f, spawnAreaSize.y));
    }
}

