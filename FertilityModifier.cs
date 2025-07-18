// Assets/Scripts/FertilityModifier.cs
using UnityEngine;
using System.Collections.Generic;

// Ensure this component can be added to GameObjects
public class FertilityModifier : MonoBehaviour
{
    [Tooltip("Amount of fertility changed per second (e.g., -1.0 for decay, 5.0 for increase).")]
    public float fertilityChangePerSecond = -1.0f;

    [Tooltip("The maximum range (in Unity units) this modifier affects tiles.")]
    public float effectRadius = 1.5f; // Roughly 1 tile radius if tile size is 1 Unity unit.

    private LandData landData; // Reference to your LandData asset
    private bool initialized = false;

    // --- You will need to set this reference from your GameManager or a setup script ---
    // Example: FindObjectOfType<FertilityModifier>().Initialize(landDataInstance);
    public void Initialize(LandData ld)
    {
        if (ld == null)
        {
            Debug.LogError("FertilityModifier: LandData reference is null during initialization.", this);
            return;
        }
        landData = ld;
        initialized = true;
    }

    void OnEnable()
    {
        // Re-initialize if already started and re-enabled
        // This is a safety check if you disable/enable objects at runtime
        if (landData != null && !initialized)
        {
            Initialize(landData);
        }
    }

    void Update()
    {
        // For demonstration, we'll use Time.deltaTime.
        // In your full game, this should be tied to your custom in-game time system.
        if (initialized && landData != null)
        {
            ApplyFertilityChange(Time.deltaTime);
        }
    }

    private void ApplyFertilityChange(float deltaTime)
    {
        // Get all tiles within the effect radius
        List<Vector2Int> affectedGlobalTiles = GetGlobalTilesInRadius(transform.position, effectRadius);

        foreach (Vector2Int globalTileCoord in affectedGlobalTiles)
        {
            // Convert global tile to chunk and internal tile coordinates
            Vector2Int internalChunkSize = landData.GetDefaultInternalChunkSize(); // Assumes this method exists
            int chunkX = globalTileCoord.x / internalChunkSize.x;
            int chunkY = globalTileCoord.y / internalChunkSize.y;

            // Check if chunk coordinates are within bounds
            if (chunkX >= 0 && chunkX < landData.chunkGridSize.x &&
                chunkY >= 0 && chunkY < landData.chunkGridSize.y)
            {
                LandDataChunk targetChunk = landData.GetChunk(chunkX, chunkY);
                if (targetChunk != null)
                {
                    Vector2Int internalTileCoord = new Vector2Int(
                        globalTileCoord.x % internalChunkSize.x,
                        globalTileCoord.y % internalChunkSize.y
                    );

                    // Check if internal tile coordinates are within the chunk
                    if (internalTileCoord.x >= 0 && internalTileCoord.x < targetChunk.internalGridSize.x &&
                        internalTileCoord.y >= 0 && internalTileCoord.y < targetChunk.internalGridSize.y)
                    {
                        CropTileData tile = targetChunk.GetTileData(internalTileCoord.x, internalTileCoord.y);
                        if (tile != null)
                        {
                            float actualChange = fertilityChangePerSecond * deltaTime;
                            // Clamp fertility between 0 and 500
                            tile.fertility = Mathf.Clamp(tile.fertility + actualChange, 0f, 500f);

                            #if UNITY_EDITOR
                            // Mark chunk dirty in editor if changes happen in Play Mode
                            // This is typically only for editor-time modifications or if you are
                            // saving play-mode changes to assets. For true runtime saving, you'd use your own system.
                            // EditorUtility.SetDirty(targetChunk);
                            #endif
                        }
                    }
                }
            }
        }
    }

    // Helper to get all global tiles within a world radius
    private List<Vector2Int> GetGlobalTilesInRadius(Vector3 worldCenter, float radius)
    {
        List<Vector2Int> tiles = new List<Vector2Int>();
        // Get the default tile size from LandData to correctly determine affected tiles
        float tileSize = landData.GetDefaultInternalTileSize();

        // Calculate a bounding box for tiles to check
        Vector3 minBound = worldCenter - new Vector3(radius, 0, radius);
        Vector3 maxBound = worldCenter + new Vector3(radius, 0, radius);

        // Convert world bounds to global tile coordinates
        Vector2Int minTile = landData.WorldToGlobalTileCoord(minBound);
        Vector2Int maxTile = landData.WorldToGlobalTileCoord(maxBound);

        // Ensure min <= max for correct iteration order
        int startX = Mathf.Min(minTile.x, maxTile.x);
        int endX = Mathf.Max(minTile.x, maxTile.x);
        int startY = Mathf.Min(minTile.y, maxTile.y);
        int endY = Mathf.Max(minTile.y, maxTile.y);


        // Iterate through all potential tiles in the bounding box
        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                Vector2Int currentGlobalTile = new Vector2Int(x, y);
                // Get the world position of the center of this tile
                Vector3 tileWorldCenter = landData.GlobalTileCoordToWorld(currentGlobalTile);

                // Check if the tile's center is within the circular radius
                // Use a 2D distance (XZ plane)
                float distance = Vector2.Distance(new Vector2(worldCenter.x, worldCenter.z), new Vector2(tileWorldCenter.x, tileWorldCenter.z));
                if (distance <= radius + (tileSize / 2f)) // Add half a tile size to include tiles whose edges touch the radius
                {
                    tiles.Add(currentGlobalTile);
                }
            }
        }
        return tiles;
    }

    void OnDrawGizmosSelected()
    {
        if (landData == null) return; // Need LandData to draw correctly

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // Orange, semi-transparent
        Gizmos.DrawSphere(transform.position, effectRadius);

        // Draw individual affected tiles in editor
        if (Application.isEditor && !Application.isPlaying) // Only in editor, not play mode to avoid spam
        {
            List<Vector2Int> affectedTiles = GetGlobalTilesInRadius(transform.position, effectRadius);
            float tileSize = landData.GetDefaultInternalTileSize();
            foreach (Vector2Int tileCoord in affectedTiles)
            {
                Vector3 tileCenterWorld = landData.GlobalTileCoordToWorld(tileCoord);
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f); // More opaque
                Gizmos.DrawCube(tileCenterWorld, new Vector3(tileSize * 0.9f, 0.1f, tileSize * 0.9f));
            }
        }
    }
}
