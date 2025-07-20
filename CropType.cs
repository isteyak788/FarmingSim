// Assets/Scripts/CropType.cs
using UnityEngine;
using System.Collections.Generic; // For List<Season>

[CreateAssetMenu(fileName = "NewCropType", menuName = "Farming/Crop Type")]
public class CropType : ScriptableObject
{
    public string cropName;
    [Tooltip("Total time from Sown to ReadyForHarvest in game days.")]
    public float timeToGrowInDays = 2f; // Total time from Sown to ReadyForHarvest (e.g., 2 game days for testing)
    public float fertilityImpact = 0f; // Initial impact when planted (e.g., -5 for heavy feeders)

    [Header("Pre-Growth Stages")]
    [Tooltip("3D models to display before any main growth stages begin.")]
    public GameObject[] preGrowthModels; // Array for models before main growth

    [Header("Growth Stages")]
    public GrowthStage[] growthStages; // Array to hold different growth stages

    [Header("Post-Growth Stages")]
    [Tooltip("3D models to display after the final main growth stage, e.g., withered or harvested states.")]
    public GameObject[] postGrowthModels; // Array for models after main growth

    [Header("Season Requirements")]
    public List<Season> growingSeasons; // List of seasons this crop can grow in. Empty list = grows all year.
}

// Sub-class for Growth Stages (can be nested in CropType or separate)
[System.Serializable]
public class GrowthStage
{
    [Tooltip("Progress threshold (0.0 to 1.0) when this stage begins.")]
    [Range(0f, 1f)]
    public float threshold;
    [Tooltip("Minimum number of in-game days this stage must last.")]
    public float minDaysForStage; // New field for minimum duration in days
    [Tooltip("3D model to display for this growth stage.")]
    public GameObject stageModel;
    // Add other properties like yield multiplier, visual effects, etc.
}
