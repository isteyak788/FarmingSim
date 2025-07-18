// GridManager.cs
using UnityEngine; // Required for MonoBehaviour, Vector2Int
using System;    // Required for DateTime

public class GridManager : MonoBehaviour
{
    // Assign your LandData ScriptableObject asset here in the Inspector.
    // This is the asset that your LandDataEditor manages.
    public LandData landData;

    public float calculationIntervalDays = 5f; // Public parameter for calculation frequency

    private DateTime lastCalculationTime;

    // Assuming you have a way to get the current in-game date/time.
    // For testing, you might just use DateTime.Now or a simple timer.
    public DateTime GetCurrentGameTime()
    {
        // Replace with your actual game time system if you have one.
        // For simple testing, DateTime.Now works.
        return DateTime.Now;
    }

    void Start()
    {
        if (landData == null)
        {
            Debug.LogError("GridManager: LandData asset reference not set! Please assign it in the Inspector.");
            enabled = false; // Disable script if critical reference is missing
            return;
        }

        // Initialize LandData if it hasn't been (e.g., if you don't use the editor to pre-init)
        // This is safe to call as InitializeChunks() handles if it's already initialized.
        landData.InitializeChunks();

        lastCalculationTime = GetCurrentGameTime(); // Initialize the timer

        // Immediately trigger a map update on start to show the initial state
        TriggerMapUpdate();
    }

    void OnEnable()
    {
        // Subscribe to the LandData's OnLandDataChanged event.
        // This event is defined on the LandData ScriptableObject itself,
        // and is invoked by the LandDataEditor when changes are made.
        if (landData != null)
        {
            landData.OnLandDataChanged += TriggerMapUpdate;
        }
    }

    void OnDisable()
    {
        // Always unsubscribe from events to prevent memory leaks,
        // especially when the GameObject is disabled or destroyed.
        if (landData != null)
        {
            landData.OnLandDataChanged -= TriggerMapUpdate;
        }
    }

    void Update()
    {
        // Timer-based update for the map visualization
        if ((GetCurrentGameTime() - lastCalculationTime).TotalDays >= calculationIntervalDays)
        {
            Debug.Log($"GridManager: {calculationIntervalDays} days passed. Triggering map update.");
            TriggerMapUpdate();
            lastCalculationTime = GetCurrentGameTime(); // Reset timer
        }
    }

    // This method is called both by the internal timer and by the OnLandDataChanged event
    public void TriggerMapUpdate()
    {
        Debug.Log("GridManager: Land data updated or time interval passed. Handle your logic here.");
        // In this trimmed version, you would add whatever logic needs to happen
        // when the land data changes or the timer interval is met,
        // now that MapModeVisualizer is removed.
    }
}
