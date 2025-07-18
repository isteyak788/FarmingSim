// Assets/Scripts/SeasonSystem.cs
using UnityEngine; // This line should be at the very top
using System.Collections.Generic; // This line should be at the very top

[CreateAssetMenu(fileName = "NewSeasonSystem", menuName = "Farming/Season System")]
public class SeasonSystem : ScriptableObject
{
    public List<Season> seasons = new List<Season>();
    public float daysPerSeason = 30f; // How many game days each season lasts

    private int currentSeasonIndex = 0;
    private float currentSeasonProgressDays = 0f;

    public Season GetCurrentSeason()
    {
        if (seasons == null || seasons.Count == 0) return null;
        return seasons[currentSeasonIndex];
    }

    public void UpdateSeason(float deltaTime) // deltaTime comes from GameManager
    {
        // Convert deltaTime (seconds) to days. Assuming 1 real second = X game seconds.
        // You'll need to define your "game day" conversion (e.g., 1 game day = 600 real seconds).
        // For now, let's assume deltaTime is already scaled by your gameTimeScale in GameManager
        // and represents a fraction of a day or seconds that need to be converted to days.
        // Example: If 1 real second = 1 game minute, then 60 real seconds = 1 game hour, 1440 real seconds = 1 game day.
        // So, if deltaTime is in real seconds: deltaTime / (seconds_per_game_day)
        // For simplicity, if deltaTime is already game-scaled seconds, then:
        // currentSeasonProgressDays += (deltaTime / total_game_seconds_in_a_day)
        // Let's assume your GameManager already scales deltaTime to represent "game seconds".
        // If your 'daysPerSeason' is in "game days", and your deltaTime is in "game seconds",
        // then you need to convert game seconds to game days.
        // Let's assume 1 game day = 86400 game seconds (standard real-world seconds in a day).
        // You can adjust this '86400f' based on how many "game seconds" make up one "game day" in your system.
        currentSeasonProgressDays += (deltaTime / 86400f); // Adjust 86400f to your game's seconds-per-day equivalent

        if (currentSeasonProgressDays >= daysPerSeason)
        {
            currentSeasonProgressDays -= daysPerSeason; // Subtract, don't reset to 0, to carry over remainder
            currentSeasonIndex = (currentSeasonIndex + 1) % seasons.Count; // Move to next season, loop back
            Debug.Log($"New Season: {GetCurrentSeason().seasonName}");
            // Trigger events for new season if needed (e.g., UI update)
        }
    }

    public float GetSeasonProgressNormalized()
    {
        return currentSeasonProgressDays / daysPerSeason;
    }

    // Call this to set up initial season or reset
    public void ResetSeasonSystem()
    {
        currentSeasonIndex = 0;
        currentSeasonProgressDays = 0f;
    }
}
