using UnityEngine;

[System.Serializable]
public class FossilType
{
    [Header("Fossil Info")]
    public string fossilName = "Unknown Fossil";
    public Sprite fossilSprite;
    
    [Header("Cleaning Settings")]
    [Tooltip("Number of dirt particles to clean. Leave at 0 to use default.")]
    public int dirtParticleCount = 0; // 0 means use default
    
    [Header("Rarity")]
    [Tooltip("Higher weight = more common. Leave at 1 for equal chances.")]
    [Range(1, 100)]
    public int spawnWeight = 1;
}

