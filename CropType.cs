// Assets/Scripts/CropType.cs (modify existing or create new)
using UnityEngine;
using System.Collections.Generic; // For List<Season>

[CreateAssetMenu(fileName = "NewCropType", menuName = "Farming/Crop Type")]
public class CropType : ScriptableObject
{
    public string cropName;
    public float timeToGrowInSeconds = 120f; // Total time from Sown to ReadyForHarvest (e.g., 2 minutes for testing)
    public float fertilityImpact = 0f; // Initial impact when planted (e.g., -5 for heavy feeders)

    [Header("Growth Stages")]
    public GrowthStage[] growthStages; // Array to hold different growth stages

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
    [Tooltip("3D model to display for this growth stage.")]
    public GameObject stageModel;
    // Add other properties like yield multiplier, visual effects, etc.
}
