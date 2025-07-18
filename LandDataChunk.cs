// LandDataChunk.cs
using UnityEngine;
using System.Collections.Generic;
using UnityEditor; // For EditorUtility.SetDirty - this is an editor dependency, but used for asset dirtying

[CreateAssetMenu(fileName = "NewLandDataChunk", menuName = "Farming/Land Data Chunk")]
public class LandDataChunk : ScriptableObject
{
    [HideInInspector] // Managed by LandData, not directly editable here
    public Vector2Int chunkCoord; // The (X, Y) coordinate of this chunk in the overall chunk grid

    [Header("Chunk Internal Grid Settings (Fixed)")]
    public Vector2Int internalGridSize = new Vector2Int(16, 16); // The fixed number of cells within this chunk (e.g., 16x16 tiles)
    public float tileSize = 2f; // The world size (Unity units) of each individual cell within this chunk

    [HideInInspector] // Managed internally by the LandDataChunk
    public List<CropTileData> tilesData = new List<CropTileData>();

    // Editor-specific state: determines if this chunk is expanded or collapsed in the Scene View
    [System.NonSerialized] // Do not serialize this temporary editor state into the asset
    public bool isExpandedInEditor = false;

    /// <summary>
    /// Initializes or resizes the tilesData list based on internalGridSize.
    /// Should be called once after creating a new chunk or if internalGridSize changes.
    /// </summary>
    public void InitializeTiles()
    {
        int totalTiles = internalGridSize.x * internalGridSize.y;
        if (tilesData == null)
        {
            tilesData = new List<CropTileData>();
        }

        // Add new tiles if the size increased
        while (tilesData.Count < totalTiles)
        {
            tilesData.Add(new CropTileData());
        }

        // Remove excess tiles if the size decreased
        while (tilesData.Count > totalTiles)
        {
            tilesData.RemoveAt(tilesData.Count - 1);
        }

        // Mark the asset as dirty so changes are saved
        EditorUtility.SetDirty(this);
    }

    /// <summary>
    /// Gets the CropTileData for a given internal grid coordinate (0 to internalGridSize-1).
    /// </summary>
    public CropTileData GetTileData(int x, int y)
    {
        if (x >= 0 && x < internalGridSize.x && y >= 0 && y < internalGridSize.y)
        {
            return tilesData[y * internalGridSize.x + x];
        }
        return null;
    }

    /// <summary>
    /// Sets the CropTileData for a given internal grid coordinate.
    /// </summary>
    public void SetTileData(int x, int y, CropTileData data)
    {
        if (x >= 0 && x < internalGridSize.x && y >= 0 && y < internalGridSize.y)
        {
            tilesData[y * internalGridSize.x + x] = data;
            EditorUtility.SetDirty(this); // Mark chunk as dirty when data changes
        }
    }

    /// <summary>
    /// Converts an internal chunk grid position (0,0 to internalGridSize-1, internalGridSize-1)
    /// to a local world position relative to the chunk's bottom-left corner.
    /// </summary>
    public Vector3 GetLocalTileWorldPosition(int x, int y)
    {
        float worldX = (x * tileSize) + (tileSize / 2f); // Center of the tile
        float worldZ = (y * tileSize) + (tileSize / 2f);
        return new Vector3(worldX, 0, worldZ); // Assuming ground plane is at Y=0
    }
}
