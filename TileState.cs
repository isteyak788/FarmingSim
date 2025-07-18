// TileState.cs

public enum TileState
{
    Unprepared,      // Initial state, no work done
    Prepared,        // Soil has been tilled/prepared
    Sown,            // Seeds have been planted
    Growing,         // Crop is actively growing (though we track progress with growthProgress)
    ReadyForHarvest, // Crop is fully grown and can be harvested
    Harvested,       // Crop has been harvested (ready for next cycle or removal)
    // Add any other states you need
}
