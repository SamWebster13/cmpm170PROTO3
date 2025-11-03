using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;

public class FossilCleaningGame : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject cleaningPanel;
    [SerializeField] private Image fossilImage;
    [SerializeField] private Transform dirtContainer;
    
    [Header("Dirt Particle Settings")]
    [SerializeField] private GameObject dirtCirclePrefab;
    [SerializeField] private Sprite dirtSprite; // Unity's default circle sprite
    [SerializeField] private float dirtCircleSize = 50f;
    [SerializeField] private float minSizeMultiplier = 0.5f; // Minimum size variation
    [SerializeField] private float maxSizeMultiplier = 1.5f; // Maximum size variation
    [SerializeField] private Color dirtColor = new Color(0.4f, 0.25f, 0.1f); // Brown color
    [SerializeField] private float colorVariation = 0.1f; // How much the color can vary (0-1)
    
    private List<DirtCircle> activeDirtCircles = new List<DirtCircle>();
    private Action onCompleteCallback;
    private int totalDirtCount = 0;
    private int removedDirtCount = 0;
    
    void Start()
    {
        // Make sure the panel is hidden at start
        if (cleaningPanel != null)
        {
            cleaningPanel.SetActive(false);
        }
    }
    
    public void StartMinigame(Sprite fossilSprite, int dirtCount, Action onComplete)
    {
        // Store callback
        onCompleteCallback = onComplete;
        totalDirtCount = dirtCount;
        removedDirtCount = 0;
        
        // Show the UI panel
        if (cleaningPanel != null)
        {
            cleaningPanel.SetActive(true);
        }
        
        // Set the fossil image
        if (fossilImage != null && fossilSprite != null)
        {
            fossilImage.sprite = fossilSprite;
            fossilImage.preserveAspect = true;
        }
        
        // Create dirt particles
        CreateDirtParticles(dirtCount);
    }
    
    void CreateDirtParticles(int count)
    {
        // Clear any existing dirt
        ClearDirt();
        
        if (fossilImage == null || dirtContainer == null) return;
        
        RectTransform fossilRect = fossilImage.GetComponent<RectTransform>();
        
        for (int i = 0; i < count; i++)
        {
            GameObject dirtObj;
            
            // Use prefab if available, otherwise create dynamically
            if (dirtCirclePrefab != null)
            {
                dirtObj = Instantiate(dirtCirclePrefab, dirtContainer);
            }
            else
            {
                dirtObj = CreateDirtCircleObject();
            }
            
            // Position randomly over the fossil image
            RectTransform dirtRect = dirtObj.GetComponent<RectTransform>();
            if (dirtRect != null && fossilRect != null)
            {
                float randomX = UnityEngine.Random.Range(-fossilRect.rect.width * 0.4f, fossilRect.rect.width * 0.4f);
                float randomY = UnityEngine.Random.Range(-fossilRect.rect.height * 0.4f, fossilRect.rect.height * 0.4f);
                dirtRect.anchoredPosition = new Vector2(randomX, randomY);
                
                // Random size variation
                float randomScale = UnityEngine.Random.Range(minSizeMultiplier, maxSizeMultiplier);
                dirtRect.localScale = Vector3.one * randomScale;
            }
            
            // Apply random color variation if using prefab
            if (dirtCirclePrefab != null)
            {
                Image image = dirtObj.GetComponent<Image>();
                if (image != null)
                {
                    Color variedColor = new Color(
                        image.color.r + UnityEngine.Random.Range(-colorVariation, colorVariation),
                        image.color.g + UnityEngine.Random.Range(-colorVariation, colorVariation),
                        image.color.b + UnityEngine.Random.Range(-colorVariation, colorVariation),
                        image.color.a
                    );
                    image.color = variedColor;
                }
            }
            
            // Add DirtCircle component
            DirtCircle dirtCircle = dirtObj.GetComponent<DirtCircle>();
            if (dirtCircle == null)
            {
                dirtCircle = dirtObj.AddComponent<DirtCircle>();
            }
            
            dirtCircle.Initialize(this);
            activeDirtCircles.Add(dirtCircle);
        }
    }
    
    GameObject CreateDirtCircleObject()
    {
        GameObject dirtObj = new GameObject("DirtCircle");
        dirtObj.transform.SetParent(dirtContainer, false);
        
        // Add Image component
        Image image = dirtObj.AddComponent<Image>();
        
        // Use Unity's default circle sprite if available, otherwise create a filled circle
        if (dirtSprite != null)
        {
            image.sprite = dirtSprite;
        }
        else
        {
            // Use Unity's built-in circle sprite
            image.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Filled;
        }
        
        // Apply color with slight variation
        Color variedColor = new Color(
            dirtColor.r + UnityEngine.Random.Range(-colorVariation, colorVariation),
            dirtColor.g + UnityEngine.Random.Range(-colorVariation, colorVariation),
            dirtColor.b + UnityEngine.Random.Range(-colorVariation, colorVariation),
            dirtColor.a
        );
        image.color = variedColor;
        
        // Set size
        RectTransform rect = dirtObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(dirtCircleSize, dirtCircleSize);
        
        return dirtObj;
    }
    
    void ClearDirt()
    {
        foreach (DirtCircle dirt in activeDirtCircles)
        {
            if (dirt != null)
            {
                Destroy(dirt.gameObject);
            }
        }
        activeDirtCircles.Clear();
    }
    
    public void OnDirtRemoved(DirtCircle dirt)
    {
        activeDirtCircles.Remove(dirt);
        removedDirtCount++;
        
        // Check if all dirt is removed
        if (activeDirtCircles.Count == 0)
        {
            CompleteMinigame();
        }
    }
    
    void CompleteMinigame()
    {
        Debug.Log("Fossil cleaning complete!");
        
        // Hide the panel after a short delay
        Invoke("HidePanel", 1f);
    }
    
    void HidePanel()
    {
        if (cleaningPanel != null)
        {
            cleaningPanel.SetActive(false);
        }
        
        // Call the completion callback
        onCompleteCallback?.Invoke();
    }
    
    void Update()
    {
        // Optional: Press Escape to cancel
        if (cleaningPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            CancelMinigame();
        }
    }
    
    void CancelMinigame()
    {
        ClearDirt();
        
        if (cleaningPanel != null)
        {
            cleaningPanel.SetActive(false);
        }
        
        // Still call callback to re-enable player
        onCompleteCallback?.Invoke();
    }
}

