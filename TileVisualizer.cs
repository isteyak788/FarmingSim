// Assets/Scripts/TileVisualizer.cs (Example for runtime)
using UnityEngine;

public class TileVisualizer : MonoBehaviour
{
    public Vector2Int globalTileCoord; // Set this when creating the tile GameObject
    private LandData landData; // Reference to your LandData

    private GameObject currentCropModelInstance;

    public void Initialize(LandData ld, Vector2Int globalCoord)
    {
        landData = ld;
        globalTileCoord = globalCoord;
    }

    void Update()
    {
        if (landData == null) return;

        // Get the relevant CropTileData
        Vector2Int internalChunkSize = landData.GetDefaultInternalChunkSize();
        int chunkX = globalTileCoord.x / internalChunkSize.x;
        int chunkY = globalTileCoord.y / internalChunkSize.y;
        int internalX = globalTileCoord.x % internalChunkSize.x;
        int internalY = globalTileCoord.y % internalChunkSize.y;

        LandDataChunk chunk = landData.GetChunk(chunkX, chunkY);
        if (chunk == null) return;

        CropTileData tileData = chunk.GetTileData(internalX, internalY);
        if (tileData == null) return;

        // Update crop model based on growth stage
        UpdateCropModel(tileData);
    }

    private void UpdateCropModel(CropTileData tileData)
    {
        GameObject newModel = null;
        if (tileData.currentCrop != null && tileData.currentCrop.growthStages != null && tileData.currentCrop.growthStages.Length > 0)
        {
            // Ensure currentGrowthStage is valid
            int stageIndex = Mathf.Clamp(tileData.currentGrowthStage, 0, tileData.currentCrop.growthStages.Length - 1);
            newModel = tileData.currentCrop.growthStages[stageIndex].stageModel;
        }

        // If model needs to change
        if (currentCropModelInstance == null || newModel != currentCropModelInstance.gameObject)
        {
            // Destroy old model
            if (currentCropModelInstance != null)
            {
                Destroy(currentCropModelInstance);
            }

            // Instantiate new model
            if (newModel != null)
            {
                currentCropModelInstance = Instantiate(newModel, transform); // Parent to this tile's GameObject
                currentCropModelInstance.transform.localPosition = Vector3.zero; // Center on the tile
                // Adjust model position/scale as needed for your specific models
            }
        }
    }
}
