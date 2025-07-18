// EnvironmentalInfluenceType.cs
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewEnvironmentalInfluence", menuName = "Farming/Environmental Influence Type")]
public class FertilityImpacts : ScriptableObject
{
    [Tooltip("Tags of GameObjects that will exert this influence.")]
    public List<string> affectingTags = new List<string>();

    [Tooltip("The amount of fertility to add or subtract from tiles.")]
    public float fertilityChange = 10f;

    [Tooltip("The radius (in tiles) where the full fertilityChange is applied.")]
    public int directSpreadRadius = 1;

    [Tooltip("The additional radius (in tiles) beyond the direct spread where the effect blends out.")]
    public int blendRadius = 1; // E.g., if spread is 1 and blend is 1, effect goes up to 2 tiles, blending in the second.

    [Tooltip("Determines if this influence adds or subtracts fertility.")]
    public InfluenceDirection influenceDirection = InfluenceDirection.Positive;

    public enum InfluenceDirection
    {
        Positive,
        Negative
    }

    [Tooltip("How frequently (in game days) this influence should be reapplied to tiles.")]
    public float applicationFrequencyDays = 1.0f; // Every day, for example

    [Tooltip("Priorities for different influence types (higher means applied last and can override).")]
    public int priority = 0; // Lower numbers applied first, higher applied later, potentially overriding.

    // Helper for editor to see total radius
    public int TotalRadius => directSpreadRadius + blendRadius;
}
