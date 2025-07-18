// Assets/Scripts/Season.cs
using UnityEngine; // This line MUST be at the very top of the file

[CreateAssetMenu(fileName = "NewSeason", menuName = "Farming/Season")]
public class Season : ScriptableObject
{
    public string seasonName;
    // You could add other properties like temperature ranges, rainfall likelihood, etc.
}
