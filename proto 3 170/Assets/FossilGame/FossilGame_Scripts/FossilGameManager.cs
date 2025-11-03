using UnityEngine;

public class FossilGameManager : MonoBehaviour
{
    [Header("Fossil Configuration")]
    [SerializeField] private FossilType[] fossilTypes;
    [SerializeField] private int defaultDirtCount = 30;
    
    [Header("UI Reference")]
    [SerializeField] private FossilCleaningGame cleaningUI;
    
    private static FossilGameManager instance;
    
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public static FossilGameManager Instance => instance;
    
    public FossilType GetRandomFossilType()
    {
        if (fossilTypes == null || fossilTypes.Length == 0)
        {
            Debug.LogWarning("FossilGameManager: No fossil types configured! Add them in the Inspector.");
            return CreateDefaultFossilType();
        }
        
        // Debug: Show detailed info about each type
        Debug.Log($"=== FossilGameManager: Checking {fossilTypes.Length} fossil type(s) ===");
        for (int i = 0; i < fossilTypes.Length; i++)
        {
            if (fossilTypes[i] == null)
            {
                Debug.LogError($"Type [{i}]: IS NULL - Did you forget to fill in this element?");
            }
            else
            {
                string spriteName = fossilTypes[i].fossilSprite != null ? fossilTypes[i].fossilSprite.name : "MISSING";
                Debug.Log($"Type [{i}]: Name='{fossilTypes[i].fossilName}' | Sprite={spriteName} | Weight={fossilTypes[i].spawnWeight}");
            }
        }
        
        // Calculate total weight
        int totalWeight = 0;
        foreach (var type in fossilTypes)
        {
            if (type != null && type.fossilSprite != null)
            {
                totalWeight += type.spawnWeight;
            }
        }
        
        if (totalWeight == 0)
        {
            Debug.LogError("FossilGameManager: All fossil types have missing sprites or zero weights!");
            Debug.LogError("Check the log above - if sprites show 'MISSING', you need to assign them.");
            Debug.LogError("If types show 'IS NULL', reduce the Fossil Types array Size or fill in all elements.");
            return CreateDefaultFossilType();
        }
        
        // Weighted random selection
        int randomValue = Random.Range(0, totalWeight);
        int currentWeight = 0;
        
        foreach (var type in fossilTypes)
        {
            if (type != null && type.fossilSprite != null)
            {
                currentWeight += type.spawnWeight;
                if (randomValue < currentWeight)
                {
                    Debug.Log($"Selected fossil type: {type.fossilName}");
                    return type;
                }
            }
        }
        
        return CreateDefaultFossilType();
    }
    
    public void StartCleaningGame(FossilType fossilType, System.Action onComplete)
    {
        if (cleaningUI == null)
        {
            Debug.LogWarning("FossilGameManager: No cleaning UI assigned!");
            return;
        }
        
        if (fossilType == null)
        {
            Debug.LogError("FossilGameManager: fossilType is NULL!");
            return;
        }
        
        if (fossilType.fossilSprite == null)
        {
            Debug.LogError($"FossilGameManager: Fossil type '{fossilType.fossilName}' has no sprite assigned!");
            Debug.LogError("Fix: Select FossilGameManager in scene > Expand Fossil Types > Assign sprites to each element");
            return;
        }
        
        Debug.Log($"Starting cleaning game with: {fossilType.fossilName} (sprite: {fossilType.fossilSprite.name})");
        
        int dirtCount = fossilType.dirtParticleCount > 0 ? fossilType.dirtParticleCount : defaultDirtCount;
        cleaningUI.StartMinigame(fossilType.fossilSprite, dirtCount, onComplete);
    }
    
    public int GetDefaultDirtCount()
    {
        return defaultDirtCount;
    }
    
    private FossilType CreateDefaultFossilType()
    {
        // Create a simple default if nothing is configured
        FossilType defaultType = new FossilType();
        defaultType.fossilName = "Default Fossil";
        defaultType.dirtParticleCount = 0;
        defaultType.spawnWeight = 1;
        return defaultType;
    }
}

