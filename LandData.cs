// LandData.cs
using UnityEngine;
using System.Collections.Generic;
using UnityEditor; // For AssetDatabase.AddObjectToAsset etc. (Only needed in Editor mode, but harmless in runtime)
using System; // Required for System.Action event delegate
using System.Linq; // Required for LINQ methods if used in other parts of LandData, though not directly in the new code


[CreateAssetMenu(fileName = "NewLandManagerData", menuName = "Farming/Land Manager Data")]
public class LandData : ScriptableObject
{
    [Header("Overall Map Settings (in Chunks)")]
    [Tooltip("The dimensions of the overall map, in number of chunks.")]
    public Vector2Int chunkGridSize = new Vector2Int(4, 4); // E.g., 4x4 chunks

    [Tooltip("The world size (Unity units) of each chunk (e.g., 32x32 meters).")]
    public float chunkSizeUnityUnits = 32f; // Should ideally be internalGridSize.x * tileSize from LandDataChunk

    [Tooltip("World position of the bottom-left corner of the entire chunk grid.")]
    public Vector3 overallGridOrigin = Vector3.zero;

    [Header("Chunk Data References")]
    // Using a 1D list to store 2D chunk grid for serialization ease
    // [SerializeField] makes it visible in Inspector and serializable, but keeps it private for code access
    [SerializeField]
    private List<LandDataChunk> chunks = new List<LandDataChunk>();

    // NEW: Event to notify listeners when LandData changes
    public event Action OnLandDataChanged;

    /// <summary>
    /// Initializes or resizes the chunks list and initializes each chunk.
    /// This should be called whenever chunkGridSize changes or on startup.
    /// </summary>
    public void InitializeChunks()
    {
        int totalChunks = chunkGridSize.x * chunkGridSize.y;

        // Ensure the list size matches the required number of chunks
        while (chunks.Count < totalChunks)
        {
            // Create new chunks if needed
            LandDataChunk newChunk = ScriptableObject.CreateInstance<LandDataChunk>();
            // Assign a temporary name for editor visibility
            newChunk.name = $"Chunk_{chunks.Count % chunkGridSize.x}_{chunks.Count / chunkGridSize.x}";
            chunks.Add(newChunk);

#if UNITY_EDITOR
            // Add the new chunk as a sub-asset to the main LandData asset
            // This ensures it gets saved and deleted with the parent LandData asset
            AssetDatabase.AddObjectToAsset(newChunk, this);
#endif
        }

        while (chunks.Count > totalChunks)
        {
            // Remove excess chunks
            LandDataChunk chunkToRemove = chunks[chunks.Count - 1];
            chunks.RemoveAt(chunks.Count - 1);
#if UNITY_EDITOR
            // Destroy the sub-asset
            AssetDatabase.RemoveObjectFromAsset(chunkToRemove);
            DestroyImmediate(chunkToRemove); // Use DestroyImmediate in editor scripts
#endif
        }

        // Initialize each chunk with its coordinates and tile data
        for (int y = 0; y < chunkGridSize.y; y++)
        {
            for (int x = 0; x < chunkGridSize.x; x++)
            {
                int index = y * chunkGridSize.x + x;
                LandDataChunk chunk = chunks[index];
                if (chunk != null)
                {
                    chunk.chunkCoord = new Vector2Int(x, y);
                    chunk.InitializeTiles(); // Ensure tile data within the chunk is initialized
                    chunk.name = $"Chunk_{x}_{y}"; // Update name in case of reinitialization
                }
            }
        }

#if UNITY_EDITOR
        // Mark the asset dirty so changes are saved
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets(); // Save the asset to disk
#endif
    }

    /// <summary>
    /// Gets a LandDataChunk at specified chunk coordinates.
    /// </summary>
    public LandDataChunk GetChunk(int x, int y)
    {
        if (x >= 0 && x < chunkGridSize.x && y >= 0 && y < chunkGridSize.y)
        {
            return chunks[y * chunkGridSize.x + x];
        }
        return null;
    }

    /// <summary>
    /// Sets a LandDataChunk at specified chunk coordinates.
    /// </summary>
    public void SetChunk(int x, int y, LandDataChunk chunk)
    {
        if (x >= 0 && x < chunkGridSize.x && y >= 0 && y < chunkGridSize.y)
        {
            chunks[y * chunkGridSize.x + x] = chunk;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this); // Mark dirty when chunk reference is changed
#endif
        }
    }

    /// <summary>
    /// Gets the default internal grid size for chunks.
    /// Assumes all chunks have the same internal grid size.
    /// </summary>
    public Vector2Int GetDefaultInternalChunkSize()
    {
        if (chunks.Count > 0 && chunks[0] != null)
        {
            return chunks[0].internalGridSize;
        }
        // Fallback or initial default
        return new Vector2Int(16, 16);
    }

    /// <summary>
    /// Gets the default tile size in Unity units.
    /// Assumes all chunks have the same tile size.
    /// </summary>
    public float GetDefaultInternalTileSize()
    {
        if (chunks.Count > 0 && chunks[0] != null)
        {
            return chunks[0].tileSize;
        }
        // Fallback or initial default
        return 2f;
    }

    /// <summary>
    /// Converts global tile coordinates to world position (center of the tile).
    /// </summary>
    public Vector3 GlobalTileCoordToWorld(Vector2Int globalTileCoord)
    {
        float tileSize = GetDefaultInternalTileSize();
        // Calculate the world position of the bottom-left corner of the tile
        float worldX = overallGridOrigin.x + (globalTileCoord.x * tileSize) + (tileSize / 2f);
        float worldZ = overallGridOrigin.z + (globalTileCoord.y * tileSize) + (tileSize / 2f); // Assuming Y is Z-axis in world

        return new Vector3(worldX, overallGridOrigin.y, worldZ);
    }

    /// <summary>
    /// Converts a world position to global tile coordinates.
    /// </summary>
    public Vector2Int WorldToGlobalTileCoord(Vector3 worldPos)
    {
        float tileSize = GetDefaultInternalTileSize();
        // Adjust worldPos relative to the grid origin
        Vector3 relativePos = worldPos - overallGridOrigin;

        // Calculate tile coordinates by dividing by tile size and flooring
        int tileX = Mathf.FloorToInt(relativePos.x / tileSize);
        int tileY = Mathf.FloorToInt(relativePos.z / tileSize); // Assuming Z is forward/back

        return new Vector2Int(tileX, tileY);
    }

    /// <summary>
    /// Gathers fertility data for all tiles across all chunks for visualization.
    /// </summary>
    public Dictionary<Vector2Int, int> GetAllTileFertility()
    {
        Dictionary<Vector2Int, int> allTileFertility = new Dictionary<Vector2Int, int>();
        Vector2Int internalChunkSize = GetDefaultInternalChunkSize();

        for (int chunkY = 0; chunkY < chunkGridSize.y; chunkY++)
        {
            for (int chunkX = 0; chunkX < chunkGridSize.x; chunkX++)
            {
                LandDataChunk chunk = GetChunk(chunkX, chunkY);
                if (chunk == null) continue;

                // Iterate through internal tiles of the chunk
                for (int internalY = 0; internalY < chunk.internalGridSize.y; internalY++)
                {
                    for (int internalX = 0; internalX < chunk.internalGridSize.x; internalX++)
                    {
                        CropTileData tileData = chunk.GetTileData(internalX, internalY);
                        if (tileData != null)
                        {
                            // Calculate global tile coordinates
                            int globalX = (chunkX * internalChunkSize.x) + internalX;
                            int globalY = (chunkY * internalChunkSize.y) + internalY;
                            Vector2Int globalCoords = new Vector2Int(globalX, globalY);

                            // Store fertility (cast float to int, assuming 0-500 range, int is fine for color grouping)
                            // Mathf.RoundToInt is used for consistency with previous discussion
                            allTileFertility[globalCoords] = Mathf.RoundToInt(tileData.fertility);
                        }
                    }
                }
            }
        }
        return allTileFertility;
    }

    // NEW ADDITION: Method to invoke the event (call this whenever data changes)
    // This is a helper for your editor or other runtime systems to notify listeners.
    public void NotifyLandDataChanged()
    {
        OnLandDataChanged?.Invoke();
    }

    /// <summary>
    /// Converts a world position to chunk coordinates.
    /// </summary>
    public Vector2Int WorldToChunkCoord(Vector3 worldPos)
    {
        // Calculate position relative to the overall grid origin
        Vector3 relativePos = worldPos - overallGridOrigin;

        // Calculate chunk coordinates
        int chunkX = Mathf.FloorToInt(relativePos.x / chunkSizeUnityUnits);
        int chunkY = Mathf.FloorToInt(relativePos.z / chunkSizeUnityUnits); // Assuming Z is forward/back

        return new Vector2Int(chunkX, chunkY);
    }

    /// <summary>
    /// Calculates the world origin (bottom-left corner) of a chunk at given chunk coordinates.
    /// </summary>
    public Vector3 ChunkCoordToWorldOrigin(int chunkX, int chunkY)
    {
        float worldX = overallGridOrigin.x + (chunkX * chunkSizeUnityUnits);
        float worldZ = overallGridOrigin.z + (chunkY * chunkSizeUnityUnits);
        return new Vector3(worldX, overallGridOrigin.y, worldZ);
    }
}
