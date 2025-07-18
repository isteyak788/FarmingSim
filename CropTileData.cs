// Assets/Scripts/CropTileData.cs
using UnityEngine;
using System; // Required for [Serializable]

[Serializable]
public class CropTileData
{
    // Changed default fertility to 250f, and recovery max to 500f
    public float fertility = 250f; // Base fertility, now from 0 to 500
    public CropType currentCrop;
    public int currentGrowthStage;
    public float growthProgress;
    public TileState state;

    // Fertility Recovery Parameters
    public float recoveryRatePerSecond = 1.0f; // How much fertility recovers per second
    public float maxRecoveryFertility = 500f;   // The maximum fertility it can recover to

    public CropTileData()
    {
        fertility = 250f; // Initialize default to mid-range
        state = TileState.Unprepared;
        currentGrowthStage = 0;
        growthProgress = 0f;
        recoveryRatePerSecond = 1.0f;
        maxRecoveryFertility = 500f;
    }

    /// <summary>
    /// Updates the tile's state, including fertility recovery and crop growth.
    /// This should be called by a central game loop (e.g., GameManager's Update).
    /// </summary>
    /// <param name="deltaTime">The time passed since the last update.</param>
    /// <param name="seasonSystem">Reference to the SeasonSystem for checking growing seasons.</param>
    public void UpdateTile(float deltaTime, SeasonSystem seasonSystem)
    {
        // Handle fertility recovery (if below max and not currently decaying from a modifier)
        // Note: The direct decay/increase from FertilityModifier happens separately.
        // This recovery acts as a "natural" recovery.
        if (fertility < maxRecoveryFertility)
        {
            // Clamp fertility between 0 and 500
            fertility = Mathf.Clamp(fertility + recoveryRatePerSecond * deltaTime, 0f, maxRecoveryFertility);
        }

        // --- Crop Growth Logic ---
        if (state == TileState.Sown && currentCrop != null)
        {
            bool canGrowThisSeason = true; // Assume can grow if no season system or no specific seasons defined

            if (seasonSystem != null && currentCrop.growingSeasons != null && currentCrop.growingSeasons.Count > 0)
            {
                // Check if the current season is one of the crop's growing seasons
                Season currentSeason = seasonSystem.GetCurrentSeason();
                canGrowThisSeason = currentCrop.growingSeasons.Contains(currentSeason);
            }

            if (canGrowThisSeason)
            {
                // Increase growth progress based on fertility and time
                // FertilityFactor: 0 at 0 fertility, 1 at 500 fertility.
                // This means at 0 fertility, growth is 0. At 500 fertility, growth is at max speed.
                float fertilityGrowthFactor = fertility / maxRecoveryFertility; // Use maxRecoveryFertility as the normalizer
                growthProgress += (deltaTime / currentCrop.timeToGrowInSeconds) * fertilityGrowthFactor;

                if (growthProgress >= 1f)
                {
                    growthProgress = 1f;
                    state = TileState.ReadyForHarvest; // Crop is fully grown
                    // Ensure the growth stage is the last one if fully grown
                    if (currentCrop.growthStages != null && currentCrop.growthStages.Length > 0)
                    {
                        currentGrowthStage = currentCrop.growthStages.Length - 1;
                    }
                }
                else
                {
                    // Update growth stage based on progress
                    if (currentCrop.growthStages != null && currentCrop.growthStages.Length > 0)
                    {
                        int previousStage = currentGrowthStage;
                        for (int i = currentCrop.growthStages.Length - 1; i >= 0; i--)
                        {
                            if (growthProgress >= currentCrop.growthStages[i].threshold)
                            {
                                currentGrowthStage = i;
                                break;
                            }
                        }
                        // Optional: if (currentGrowthStage != previousStage) Debug.Log("Crop stage changed!");
                    }
                }
            }
        }
    }
}

// Ensure your TileState enum is defined elsewhere, e.g., in a separate Script or in LandData.cs
// public enum TileState { Unprepared, Prepared, Sown, Growing, ReadyForHarvest, Harvested }
