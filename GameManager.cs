// Assets/Scripts/GameManager.cs (Example)
using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public LandData landData;
    public float gameTimeScale = 1.0f; // How fast game time passes relative to real time
    public SeasonSystem seasonSystem; // Reference to your SeasonSystem

    private List<FertilityModifier> activeFertilityModifiers = new List<FertilityModifier>();

    void Start()
    {
        if (landData == null)
        {
            Debug.LogError("LandData not assigned to GameManager!");
            return;
        }

        // Initialize all existing FertilityModifiers in the scene
        FertilityModifier[] existingModifiers = FindObjectsOfType<FertilityModifier>();
        foreach (FertilityModifier modifier in existingModifiers)
        {
            modifier.Initialize(landData);
            activeFertilityModifiers.Add(modifier);
        }

        // Initialize SeasonSystem
        if (seasonSystem == null)
        {
            seasonSystem = FindObjectOfType<SeasonSystem>(); // Or create one
            if (seasonSystem == null) Debug.LogError("SeasonSystem not found in scene!");
        }

        // Initialize LandData if it wasn't already from editor
        landData.InitializeChunks();
    }

    void Update()
    {
        float deltaTime = Time.deltaTime * gameTimeScale;

        // Update all tiles for growth and recovery
        UpdateAllTiles(deltaTime);

        // Update season system
        if (seasonSystem != null)
        {
            seasonSystem.UpdateSeason(deltaTime);
        }
    }

    void UpdateAllTiles(float deltaTime)
    {
        // Iterate through all chunks and tiles
        for (int chunkY = 0; chunkY < landData.chunkGridSize.y; chunkY++)
        {
            for (int chunkX = 0; chunkX < landData.chunkGridSize.x; chunkX++)
            {
                LandDataChunk chunk = landData.GetChunk(chunkX, chunkY);
                if (chunk != null)
                {
                    foreach (var tileData in chunk.tilesData) // Assuming tilesData is a List or array
                    {
                        if (tileData != null)
                        {
                            tileData.UpdateTile(deltaTime, seasonSystem); // Pass seasonSystem
                        }
                    }
                }
            }
        }
    }

    // You might want to add methods to add/remove FertilityModifiers dynamically
    public void AddFertilityModifier(FertilityModifier modifier)
    {
        if (!activeFertilityModifiers.Contains(modifier))
        {
            modifier.Initialize(landData);
            activeFertilityModifiers.Add(modifier);
        }
    }

    public void RemoveFertilityModifier(FertilityModifier modifier)
    {
        activeFertilityModifiers.Remove(modifier);
    }
}
