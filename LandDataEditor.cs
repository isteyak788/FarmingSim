// Assets/Scripts/Editor/LandDataEditor.cs
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq; // Required for .FirstOrDefault() and .OrderBy()

[CustomEditor(typeof(LandData))]
public class LandDataEditor : Editor
{
    private LandData landData;

    // Current interaction context
    private LandDataChunk hoveredChunk;
    private Vector2Int hoveredChunkCoord; // Coordinates of the chunk being hovered over
    private CropTileData hoveredTileData; // If a tile within an expanded chunk is hovered
    private Vector2Int hoveredInternalTileCoord; // Internal coordinates within the hovered chunk

    // Editor Tools State
    private bool editModeEnabled = false;
    private EditorTool currentEditorTool = EditorTool.SquareBrush; // Default to SquareBrush
    private BrushMode currentBrushMode = BrushMode.Fertility;
    private float brushFertilityValue = 50f; // Default brush value to mid-range (0-100)
    private CropType brushCropType;
    private int brushSize = 1; // For SquareBrush, this is side length; for CircleBrush, this is radius

    // For Box Selection
    private bool isDraggingBox = false;
    private Vector2Int boxSelectStartGlobalCoord; // Global tile coordinate
    private Vector2Int boxSelectEndGlobalCoord;   // Global tile coordinate

    // For Lasso Selection
    private bool isLassoing = false;
    private List<Vector2Int> lassoPointsGlobal = new List<Vector2Int>(); // Global tile coordinates for lasso points

    // Overlay settings
    private bool showFertilityOverlay = false;
    private Gradient fertilityColorGradient;
    private bool showEnvironmentalInfluenceViz = false; // New toggle for visualizing environmental influences

    // EditorPrefs keys for persistence
    private const string EDITOR_PREFS_EDIT_MODE_ENABLED = "LandDataEditor.EditModeEnabled";
    private const string EDITOR_PREFS_CURRENT_TOOL = "LandDataEditor.CurrentTool";
    private const string EDITOR_PREFS_BRUSH_MODE = "LandDataEditor.BrushMode";
    private const string EDITOR_PREFS_BRUSH_FERTILITY_VALUE = "LandDataEditor.BrushFertilityValue";
    private const string EDITOR_PREFS_BRUSH_SIZE = "LandDataEditor.BrushSize";
    private const string EDITOR_PREFS_SHOW_FERTILITY_OVERLAY = "LandDataEditor.ShowFertilityOverlay";
    private const string EDITOR_PREFS_SHOW_ENVIRONMENTAL_INFLUENCE_VIZ = "LandDataEditor.ShowEnvironmentalInfluenceViz";


    // Enum for different interaction tools
    private enum EditorTool { SquareBrush, CircleBrush, BoxSelect, LassoSelect } // Renamed Brush to SquareBrush, added CircleBrush
    // Enum for different brush modes (what the brush applies)
    private enum BrushMode { Fertility, CropType }

    private void OnEnable()
    {
        landData = (LandData)target;
        landData.InitializeChunks(); // Call this unconditionally as it handles re-initialization safely.

        // Load persisted editor state
        editModeEnabled = EditorPrefs.GetBool(EDITOR_PREFS_EDIT_MODE_ENABLED, false);
        currentEditorTool = (EditorTool)EditorPrefs.GetInt(EDITOR_PREFS_CURRENT_TOOL, (int)EditorTool.SquareBrush);
        currentBrushMode = (BrushMode)EditorPrefs.GetInt(EDITOR_PREFS_BRUSH_MODE, (int)BrushMode.Fertility);
        brushFertilityValue = EditorPrefs.GetFloat(EDITOR_PREFS_BRUSH_FERTILITY_VALUE, 50f); // Default to 50 for 0-100 range
        brushSize = EditorPrefs.GetInt(EDITOR_PREFS_BRUSH_SIZE, 1);
        showFertilityOverlay = EditorPrefs.GetBool(EDITOR_PREFS_SHOW_FERTILITY_OVERLAY, false);
        showEnvironmentalInfluenceViz = EditorPrefs.GetBool(EDITOR_PREFS_SHOW_ENVIRONMENTAL_INFLUENCE_VIZ, false);


        // Initialize the Gradient
        if (fertilityColorGradient == null)
        {
            fertilityColorGradient = new Gradient();
            // Default gradient from red (low) to green (high) for 0-100 range
            GradientColorKey[] colorKeys = new GradientColorKey[2];
            colorKeys[0] = new GradientColorKey(Color.red, 0f);    // 0 fertility is red
            colorKeys[1] = new GradientColorKey(Color.green, 1f);  // 100 fertility is green (normalized to 1.0)

            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
            alphaKeys[0] = new GradientAlphaKey(0.5f, 0f); // 50% opacity
            alphaKeys[1] = new GradientAlphaKey(0.5f, 1f); // 50% opacity
            fertilityColorGradient.SetKeys(colorKeys, alphaKeys);
        }

        // Subscribe to SceneView rendering events
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        // Unsubscribe to prevent memory leaks
        SceneView.duringSceneGui -= OnSceneGUI;

        // Save editor state when disabled
        EditorPrefs.SetBool(EDITOR_PREFS_EDIT_MODE_ENABLED, editModeEnabled);
        EditorPrefs.SetInt(EDITOR_PREFS_CURRENT_TOOL, (int)currentEditorTool);
        EditorPrefs.SetInt(EDITOR_PREFS_BRUSH_MODE, (int)currentBrushMode);
        EditorPrefs.SetFloat(EDITOR_PREFS_BRUSH_FERTILITY_VALUE, brushFertilityValue);
        EditorPrefs.SetInt(EDITOR_PREFS_BRUSH_SIZE, brushSize);
        EditorPrefs.SetBool(EDITOR_PREFS_SHOW_FERTILITY_OVERLAY, showFertilityOverlay);
        EditorPrefs.SetBool(EDITOR_PREFS_SHOW_ENVIRONMENTAL_INFLUENCE_VIZ, showEnvironmentalInfluenceViz);
    }

    // Custom Inspector GUI for LandData asset
    public override void OnInspectorGUI()
    {
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector(); // Draws public fields from LandData (chunkGridSize, chunkSizeUnityUnits, etc.)

        if (EditorGUI.EndChangeCheck())
        {
            landData.InitializeChunks();
            landData.NotifyLandDataChanged(); // Notify after structural changes
        }

        if (GUILayout.Button("Force Reinitialize All Chunks"))
        {
            landData.InitializeChunks();
            landData.NotifyLandDataChanged(); // Notify after force reinit
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Editor Interaction Tools", EditorStyles.boldLabel);

        // Toggle button to enable/disable Scene View interaction
        bool newEditModeEnabled = GUILayout.Toggle(editModeEnabled, "Enable Editor Tools (Scene Interaction)", "Button");
        if (newEditModeEnabled != editModeEnabled)
        {
            editModeEnabled = newEditModeEnabled;
            EditorPrefs.SetBool(EDITOR_PREFS_EDIT_MODE_ENABLED, editModeEnabled); // Persist change immediately
        }

        if (editModeEnabled)
        {
            EditorGUILayout.HelpBox("Select a tool below to interact with the grid in the Scene view.\n" +
                                    "Ctrl + Left-click a chunk to expand it into a detailed grid.\n" +
                                    "Ctrl + Right-click an expanded chunk to collapse it.", MessageType.Info);

            EditorTool newEditorTool = (EditorTool)EditorGUILayout.EnumPopup("Active Tool", currentEditorTool);
            if (newEditorTool != currentEditorTool)
            {
                currentEditorTool = newEditorTool;
                EditorPrefs.SetInt(EDITOR_PREFS_CURRENT_TOOL, (int)currentEditorTool); // Persist change
            }


            // --- Tool-specific UI ---
            if (currentEditorTool == EditorTool.SquareBrush) // Renamed from EditorTool.Brush
            {
                EditorGUILayout.HelpBox("Click and drag to apply square brush. Brush size determines area.", MessageType.None);
                BrushMode newBrushMode = (BrushMode)EditorGUILayout.EnumPopup("Brush Mode", currentBrushMode);
                if (newBrushMode != currentBrushMode)
                {
                    currentBrushMode = newBrushMode;
                    EditorPrefs.SetInt(EDITOR_PREFS_BRUSH_MODE, (int)currentBrushMode); // Persist change
                }

                int newBrushSize = EditorGUILayout.IntSlider("Brush Size", brushSize, 1, 5); // Brush size (in tiles)
                if (newBrushSize != brushSize)
                {
                    brushSize = newBrushSize;
                    EditorPrefs.SetInt(EDITOR_PREFS_BRUSH_SIZE, brushSize); // Persist change
                }


                if (currentBrushMode == BrushMode.Fertility)
                {
                    // Changed slider range to 0-100
                    float newBrushFertilityValue = EditorGUILayout.Slider("Brush Fertility", brushFertilityValue, 0f, 100f);
                    if (newBrushFertilityValue != brushFertilityValue)
                    {
                        brushFertilityValue = newBrushFertilityValue;
                        EditorPrefs.SetFloat(EDITOR_PREFS_BRUSH_FERTILITY_VALUE, brushFertilityValue); // Persist change
                    }
                }
                else if (currentBrushMode == BrushMode.CropType)
                {
                    brushCropType = (CropType)EditorGUILayout.ObjectField("Brush Crop Type", brushCropType, typeof(CropType), false);
                }
            }
            else if (currentEditorTool == EditorTool.CircleBrush) // New CircleBrush UI
            {
                EditorGUILayout.HelpBox("Click and drag to apply circular brush. Brush radius determines area.", MessageType.None);
                BrushMode newBrushMode = (BrushMode)EditorGUILayout.EnumPopup("Brush Mode", currentBrushMode);
                if (newBrushMode != currentBrushMode)
                {
                    currentBrushMode = newBrushMode;
                    EditorPrefs.SetInt(EDITOR_PREFS_BRUSH_MODE, (int)currentBrushMode); // Persist change
                }

                int newBrushSize = EditorGUILayout.IntSlider("Brush Radius", brushSize, 1, 5); // Brush size (in tiles)
                if (newBrushSize != brushSize)
                {
                    brushSize = newBrushSize;
                    EditorPrefs.SetInt(EDITOR_PREFS_BRUSH_SIZE, brushSize); // Persist change
                }

                if (currentBrushMode == BrushMode.Fertility)
                {
                    float newBrushFertilityValue = EditorGUILayout.Slider("Brush Fertility", brushFertilityValue, 0f, 100f);
                    if (newBrushFertilityValue != brushFertilityValue)
                    {
                        brushFertilityValue = newBrushFertilityValue;
                        EditorPrefs.SetFloat(EDITOR_PREFS_BRUSH_FERTILITY_VALUE, brushFertilityValue); // Persist change
                    }
                }
                else if (currentBrushMode == BrushMode.CropType)
                {
                    brushCropType = (CropType)EditorGUILayout.ObjectField("Brush Crop Type", brushCropType, typeof(CropType), false);
                }
            }
            else if (currentEditorTool == EditorTool.BoxSelect)
            {
                EditorGUILayout.HelpBox("Click and drag to select a box area. Release to apply brush to the area.", MessageType.None);
                BrushMode newBrushMode = (BrushMode)EditorGUILayout.EnumPopup("Brush Mode", currentBrushMode);
                if (newBrushMode != currentBrushMode)
                {
                    currentBrushMode = newBrushMode;
                    EditorPrefs.SetInt(EDITOR_PREFS_BRUSH_MODE, (int)currentBrushMode); // Persist change
                }

                if (currentBrushMode == BrushMode.Fertility)
                {
                    // Changed slider range to 0-100
                    float newBrushFertilityValue = EditorGUILayout.Slider("Brush Fertility", brushFertilityValue, 0f, 100f);
                    if (newBrushFertilityValue != brushFertilityValue)
                    {
                        brushFertilityValue = newBrushFertilityValue;
                        EditorPrefs.SetFloat(EDITOR_PREFS_BRUSH_FERTILITY_VALUE, brushFertilityValue); // Persist change
                    }
                }
                else if (currentBrushMode == BrushMode.CropType)
                {
                    brushCropType = (CropType)EditorGUILayout.ObjectField("Brush Crop Type", brushCropType, typeof(CropType), false);
                }
            }
            else if (currentEditorTool == EditorTool.LassoSelect)
            {
                EditorGUILayout.HelpBox("Click points to define a lasso path. Right-click or press Enter to close and apply.", MessageType.Info);
                BrushMode newBrushMode = (BrushMode)EditorGUILayout.EnumPopup("Brush Mode", currentBrushMode);
                if (newBrushMode != currentBrushMode)
                {
                    currentBrushMode = newBrushMode;
                    EditorPrefs.SetInt(EDITOR_PREFS_BRUSH_MODE, (int)currentBrushMode); // Persist change
                }

                if (currentBrushMode == BrushMode.Fertility)
                {
                    float newBrushFertilityValue = EditorGUILayout.Slider("Brush Fertility", brushFertilityValue, 0f, 100f);
                    if (newBrushFertilityValue != brushFertilityValue)
                    {
                        brushFertilityValue = newBrushFertilityValue;
                        EditorPrefs.SetFloat(EDITOR_PREFS_BRUSH_FERTILITY_VALUE, brushFertilityValue); // Persist change
                    }
                }
                else if (currentBrushMode == BrushMode.CropType)
                {
                    brushCropType = (CropType)EditorGUILayout.ObjectField("Brush Crop Type", brushCropType, typeof(CropType), false);
                }

                if (isLassoing)
                {
                    EditorGUILayout.LabelField($"Lasso Points: {lassoPointsGlobal.Count}");
                    if (GUILayout.Button("Clear Lasso Points"))
                    {
                        lassoPointsGlobal.Clear();
                        isLassoing = false;
                    }
                }
            }

            if (GUILayout.Button("Save All Land Data Changes"))
            {
                EditorUtility.SetDirty(landData); // Mark the main asset dirty
                AssetDatabase.SaveAssets(); // Save all changes including embedded chunks
                Debug.Log("All Land Data Saved!");
                landData.NotifyLandDataChanged(); // Notify after explicit save
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Enable Editor Tools above to interact with the grid.", MessageType.Info);
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Overlay Options", EditorStyles.boldLabel);

        // Using GradientField for fertility colors
        bool newShowFertilityOverlay = GUILayout.Toggle(showFertilityOverlay, "Show Fertility Overlay (Editor Only)");
        if (newShowFertilityOverlay != showFertilityOverlay)
        {
            showFertilityOverlay = newShowFertilityOverlay;
            EditorPrefs.SetBool(EDITOR_PREFS_SHOW_FERTILITY_OVERLAY, showFertilityOverlay); // Persist change
        }

        if (showFertilityOverlay)
        {
            fertilityColorGradient = EditorGUILayout.GradientField("Fertility Gradient", fertilityColorGradient);
        }

        // Environmental Influence Visualization Toggle
        bool newShowEnvironmentalInfluenceViz = GUILayout.Toggle(showEnvironmentalInfluenceViz, "Show Environmental Influence Viz");
        if (newShowEnvironmentalInfluenceViz != showEnvironmentalInfluenceViz)
        {
            showEnvironmentalInfluenceViz = newShowEnvironmentalInfluenceViz;
            EditorPrefs.SetBool(EDITOR_PREFS_SHOW_ENVIRONMENTAL_INFLUENCE_VIZ, showEnvironmentalInfluenceViz); // Persist change
        }

        // NEW: Button to apply environmental influences
        EditorGUILayout.Space(5);
        if (GUILayout.Button("Apply Environmental Influences (Editor Only)"))
        {
            ApplyAllEnvironmentalInfluences();
            EditorUtility.SetDirty(landData); // Mark the landData dirty after applying influences
            AssetDatabase.SaveAssets(); // Save changes
            Debug.Log("Environmental Influences Applied!");
            landData.NotifyLandDataChanged(); // Notify after applying influences
        }

        // It's good practice to mark the target dirty if any GUI elements change its state,
        // even if not directly serializing the GUI state. This ensures editor changes persist.
        if (GUI.changed)
        {
            EditorUtility.SetDirty(landData);
        }
    }

    // Called for custom Scene View drawing and interaction
    private void OnSceneGUI(SceneView sceneView)
    {
        // Only draw if the LandData asset is currently selected in the Project window
        if (landData == null || Selection.activeObject != landData)
        {
            return;
        }

        Event current = Event.current; // Get the current event (mouse, keyboard, etc.)
        Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition); // Ray from mouse position into world
        // Assume terrain is on a plane at landData.overallGridOrigin.y
        Plane groundPlane = new Plane(Vector3.up, landData.overallGridOrigin.y);
        Vector3 hitPoint = Vector3.zero;
        bool mouseOverGround = false;

        if (groundPlane.Raycast(ray, out float distance))
        {
            hitPoint = ray.GetPoint(distance);
            mouseOverGround = true;

            // Determine which chunk the mouse is over
            hoveredChunkCoord = landData.WorldToChunkCoord(hitPoint);

            // Clamp chunk coordinates to ensure they are within the defined grid
            hoveredChunkCoord.x = Mathf.Clamp(hoveredChunkCoord.x, 0, landData.chunkGridSize.x - 1);
            hoveredChunkCoord.y = Mathf.Clamp(hoveredChunkCoord.y, 0, landData.chunkGridSize.y - 1);

            hoveredChunk = landData.GetChunk(hoveredChunkCoord.x, hoveredChunkCoord.y);

            // If a chunk is hovered and expanded, determine which internal tile is hovered
            if (hoveredChunk != null && hoveredChunk.isExpandedInEditor)
            {
                Vector3 chunkOrigin = landData.ChunkCoordToWorldOrigin(hoveredChunkCoord.x, hoveredChunkCoord.y);
                Vector3 localHitPointInChunk = hitPoint - chunkOrigin; // Position relative to chunk's origin

                hoveredInternalTileCoord.x = Mathf.FloorToInt(localHitPointInChunk.x / hoveredChunk.tileSize);
                hoveredInternalTileCoord.y = Mathf.FloorToInt(localHitPointInChunk.z / hoveredChunk.tileSize);

                // Clamp internal tile coordinates
                hoveredInternalTileCoord.x = Mathf.Clamp(hoveredInternalTileCoord.x, 0, hoveredChunk.internalGridSize.x - 1);
                hoveredInternalTileCoord.y = Mathf.Clamp(hoveredInternalTileCoord.y, 0, hoveredChunk.internalGridSize.y - 1);

                hoveredTileData = hoveredChunk.GetTileData(hoveredInternalTileCoord.x, hoveredInternalTileCoord.y);
            }
            else
            {
                hoveredTileData = null; // No internal tile hovered if chunk is collapsed or not hovered
            }
        }
        else
        {
            hoveredChunk = null;
            hoveredTileData = null;
        }

        // Prevent Unity's default selection behavior when our tools are active
        if (editModeEnabled)
        {
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            // --- Chunk Expansion/Collapse Logic (Ctrl + Click) ---
            if (mouseOverGround && hoveredChunk != null)
            {
                // Ctrl + Left Click to expand a chunk
                if (current.type == EventType.MouseDown && current.button == 0 && current.control)
                {
                    hoveredChunk.isExpandedInEditor = true;
                    current.Use(); // Consume the event
                }
                // Ctrl + Right Click to collapse a chunk
                else if (current.type == EventType.MouseDown && current.button == 1 && current.control)
                {
                    hoveredChunk.isExpandedInEditor = false;
                    current.Use(); // Consume the event
                }
                // If the chunk is expanded, pass interaction to tool handlers
                else if (hoveredChunk.isExpandedInEditor)
                {
                    if (currentEditorTool == EditorTool.SquareBrush) // Updated name
                    {
                        HandleSquareBrushTool(current, mouseOverGround && hoveredTileData != null);
                    }
                    else if (currentEditorTool == EditorTool.CircleBrush) // New CircleBrush handler
                    {
                        HandleCircleBrushTool(current, mouseOverGround && hoveredTileData != null);
                    }
                    else if (currentEditorTool == EditorTool.BoxSelect)
                    {
                        HandleBoxSelectTool(current, mouseOverGround && hoveredTileData != null);
                    }
                    else if (currentEditorTool == EditorTool.LassoSelect)
                    {
                        HandleLassoSelectTool(current, mouseOverGround && hoveredTileData != null);
                    }
                }
            }
        }
        else
        {
            // Allow normal Unity selection if our custom tools are disabled
            HandleUtility.Repaint(); // Ensure SceneView repaints to update selection
        }

        // --- Drawing ---
        DrawChunkGrid();
        DrawToolSpecificVisuals(); // Draws brush/selection previews and hovered tile info
        if (showFertilityOverlay)
        {
            DrawFertilityOverlay();
        }
        // Draw Environmental Influence Visualization
        if (showEnvironmentalInfluenceViz)
        {
            DrawEnvironmentalInfluenceVisuals();
        }

        // Request repaint for continuous drawing updates
        SceneView.RepaintAll();
    }

    // --- Tool Handling Methods ---

    private void HandleSquareBrushTool(Event current, bool mouseOverTile) // Renamed from HandleBrushTool
    {
        if (!mouseOverTile) return; // Must be over an active tile in an expanded chunk

        if (current.type == EventType.MouseDown && current.button == 0)
        {
            // Apply brush on initial click
            ApplySquareBrushToArea(hoveredChunk, hoveredInternalTileCoord.x, hoveredInternalTileCoord.y, brushSize);
            landData.NotifyLandDataChanged(); // Notify after changes
            current.Use();
        }
        else if (current.type == EventType.MouseDrag && current.button == 0)
        {
            // Apply brush while dragging
            ApplySquareBrushToArea(hoveredChunk, hoveredInternalTileCoord.x, hoveredInternalTileCoord.y, brushSize);
            landData.NotifyLandDataChanged(); // Notify after changes
            current.Use();
        }
    }

    private void HandleCircleBrushTool(Event current, bool mouseOverTile) // New CircleBrush handler
    {
        if (!mouseOverTile) return; // Must be over an active tile in an expanded chunk

        if (current.type == EventType.MouseDown && current.button == 0)
        {
            // Apply brush on initial click
            ApplyCircleBrushToArea(hoveredChunk, hoveredInternalTileCoord.x, hoveredInternalTileCoord.y, brushSize);
            landData.NotifyLandDataChanged(); // Notify after changes
            current.Use();
        }
        else if (current.type == EventType.MouseDrag && current.button == 0)
        {
            // Apply brush while dragging
            ApplyCircleBrushToArea(hoveredChunk, hoveredInternalTileCoord.x, hoveredInternalTileCoord.y, brushSize);
            landData.NotifyLandDataChanged(); // Notify after changes
            current.Use();
        }
    }

    private void HandleBoxSelectTool(Event current, bool mouseOverTile)
    {
        if (!mouseOverTile) return;

        // Get the global tile coordinate of the mouse position
        Vector2Int globalCoord = landData.WorldToGlobalTileCoord(
            landData.ChunkCoordToWorldOrigin(hoveredChunkCoord.x, hoveredChunkCoord.y) +
            hoveredChunk.GetLocalTileWorldPosition(hoveredInternalTileCoord.x, hoveredInternalTileCoord.y)
        );

        if (current.type == EventType.MouseDown && current.button == 0)
        {
            isDraggingBox = true;
            boxSelectStartGlobalCoord = globalCoord;
            boxSelectEndGlobalCoord = globalCoord;
            current.Use();
        }
        else if (current.type == EventType.MouseDrag && current.button == 0 && isDraggingBox)
        {
            boxSelectEndGlobalCoord = globalCoord;
            current.Use();
        }
        else if (current.type == EventType.MouseUp && current.button == 0 && isDraggingBox)
        {
            isDraggingBox = false;
            ApplyBrushToBox(boxSelectStartGlobalCoord, boxSelectEndGlobalCoord);
            landData.NotifyLandDataChanged(); // Notify after changes
            current.Use();
        }
    }

    private void HandleLassoSelectTool(Event current, bool mouseOverTile)
    {
        if (!mouseOverTile) return;

        Vector2Int globalCoord = landData.WorldToGlobalTileCoord(
            landData.ChunkCoordToWorldOrigin(hoveredChunkCoord.x, hoveredChunkCoord.y) +
            hoveredChunk.GetLocalTileWorldPosition(hoveredInternalTileCoord.x, hoveredInternalTileCoord.y)
        );

        if (current.type == EventType.MouseDown && current.button == 0) // Left click to add points
        {
            if (!isLassoing)
            {
                lassoPointsGlobal.Clear();
                isLassoing = true;
            }
            if (!lassoPointsGlobal.Contains(globalCoord)) // Avoid duplicate points if clicking same tile
            {
                lassoPointsGlobal.Add(globalCoord);
            }
            current.Use();
        }
        else if (isLassoing && (current.type == EventType.MouseDown && current.button == 1 || // Right click to finish
                                current.type == EventType.KeyDown && current.keyCode == KeyCode.Return)) // Enter key to finish
        {
            if (lassoPointsGlobal.Count >= 3) // Need at least 3 points to form a polygon
            {
                ApplyBrushToLasso(lassoPointsGlobal);
                Debug.Log("Lasso selection applied.");
            }
            else
            {
                Debug.LogWarning("Lasso selection requires at least 3 points. Clearing points.");
            }
            lassoPointsGlobal.Clear(); // Clear points after applying/finishing
            isLassoing = false;
            landData.NotifyLandDataChanged(); // Notify after changes
            current.Use();
        }
    }

    // --- Drawing Methods ---

    private void DrawChunkGrid()
    {
        Handles.color = new Color(0.7f, 0.7f, 0.7f, 0.5f); // Lighter grey for chunk lines
        Vector3 origin = landData.overallGridOrigin;
        float chunkSize = landData.chunkSizeUnityUnits;

        // Draw horizontal lines for chunks
        for (int y = 0; y <= landData.chunkGridSize.y; y++)
        {
            Vector3 start = new Vector3(origin.x, origin.y, origin.z + y * chunkSize);
            Vector3 end = new Vector3(origin.x + landData.chunkGridSize.x * chunkSize, origin.y, origin.z + y * chunkSize);
            Handles.DrawLine(start, end);
        }

        // Draw vertical lines for chunks
        for (int x = 0; x <= landData.chunkGridSize.x; x++)
        {
            Vector3 start = new Vector3(origin.x + x * chunkSize, origin.y, origin.z);
            Vector3 end = new Vector3(origin.x + x * chunkSize, origin.y, origin.z + landData.chunkGridSize.y * chunkSize);
            Handles.DrawLine(start, end);
        }

        // Draw individual chunk cells (collapsed or expanded)
        for (int y = 0; y < landData.chunkGridSize.y; y++)
        {
            for (int x = 0; x < landData.chunkGridSize.x; x++)
            {
                LandDataChunk chunk = landData.GetChunk(x, y);
                if (chunk == null) continue;

                Vector3 chunkOrigin = landData.ChunkCoordToWorldOrigin(x, y);
                Vector3 chunkCenter = chunkOrigin + new Vector3(landData.chunkSizeUnityUnits / 2f, 0, landData.chunkSizeUnityUnits / 2f);

                if (chunk.isExpandedInEditor)
                {
                    // Draw internal tile grid if expanded
                    DrawInternalGrid(chunk, chunkOrigin);
                }
                else
                {
                    // Draw single chunk cell representation if collapsed
                    Handles.color = new Color(0.2f, 0.5f, 0.8f, 0.2f); // Collapsed chunk fill color
                    // Use DrawSolidRectangleWithOutline for a clearer visual
                    Handles.DrawSolidRectangleWithOutline(GetChunkRectVertices(x, y), Handles.color, Color.cyan); // Blue outline
                    Handles.Label(chunkCenter + Vector3.up * 0.1f, $"Chunk {x},{y}\n(Collapsed)", EditorStyles.miniLabel);
                }

                // Highlight hovered chunk (regardless of expansion state)
                if (hoveredChunk == chunk)
                {
                    Handles.color = new Color(0.5f, 1f, 0f, 0.3f); // Greenish yellow highlight
                    Handles.DrawSolidRectangleWithOutline(GetChunkRectVertices(x, y), Handles.color, Color.green);
                    Handles.Label(chunkCenter + Vector3.up * 0.2f, $"HOVERED CHUNK", EditorStyles.boldLabel);
                }
            }
        }
    }

    private void DrawInternalGrid(LandDataChunk chunk, Vector3 chunkWorldOrigin)
    {
        Handles.color = Color.grey;
        float tileSize = chunk.tileSize;
        Vector2Int internalGridSize = chunk.internalGridSize;

        // Draw internal horizontal lines
        for (int y = 0; y <= internalGridSize.y; y++)
        {
            Vector3 start = new Vector3(chunkWorldOrigin.x, chunkWorldOrigin.y, chunkWorldOrigin.z + y * tileSize);
            Vector3 end = new Vector3(chunkWorldOrigin.x + internalGridSize.x * tileSize, chunkWorldOrigin.y, chunkWorldOrigin.z + y * tileSize);
            Handles.DrawLine(start, end);
        }

        // Draw internal vertical lines
        for (int x = 0; x <= internalGridSize.x; x++)
        {
            Vector3 start = new Vector3(chunkWorldOrigin.x + x * tileSize, chunkWorldOrigin.y, chunkWorldOrigin.z);
            Vector3 end = new Vector3(chunkWorldOrigin.x + x * tileSize, chunkWorldOrigin.y, chunkWorldOrigin.z + internalGridSize.y * tileSize);
            Handles.DrawLine(start, end);
        }
    }

    private void DrawToolSpecificVisuals()
    {
        // Draw info for the currently hovered internal tile (if a chunk is expanded)
        if (hoveredTileData != null && hoveredChunk != null && hoveredChunk.isExpandedInEditor)
        {
            GUIStyle textStyle = new GUIStyle();
            textStyle.normal.textColor = Color.white;
            textStyle.alignment = TextAnchor.MiddleCenter;
            textStyle.fontSize = 12;

            // Get the world position of the center of the hovered tile
            Vector3 tileWorldPos = landData.ChunkCoordToWorldOrigin(hoveredChunkCoord.x, hoveredChunkCoord.y) +
                                   hoveredChunk.GetLocalTileWorldPosition(hoveredInternalTileCoord.x, hoveredInternalTileCoord.y);

            string infoText = $"Chunk:{hoveredChunkCoord.x},{hoveredChunkCoord.y}\nTile:{hoveredInternalTileCoord.x},{hoveredInternalTileCoord.y}\nFertility: {hoveredTileData.fertility:F1}%\nCrop: {(hoveredTileData.currentCrop != null ? hoveredTileData.currentCrop.cropName : "None")}\nState: {hoveredTileData.state}";
            Handles.Label(tileWorldPos + Vector3.up * 0.1f, infoText, textStyle);
        }

        // Draw brush preview
        if ((currentEditorTool == EditorTool.SquareBrush || currentEditorTool == EditorTool.CircleBrush) && hoveredTileData != null && hoveredChunk != null && hoveredChunk.isExpandedInEditor)
        {
            int halfBrush = brushSize / 2;
            for (int y = -halfBrush; y <= halfBrush; y++)
            {
                for (int x = -halfBrush; x <= halfBrush; x++)
                {
                    Vector2Int currentInternalCoord = new Vector2Int(hoveredInternalTileCoord.x + x, hoveredInternalTileCoord.y + y);
                    if (currentInternalCoord.x >= 0 && currentInternalCoord.x < hoveredChunk.internalGridSize.x &&
                        currentInternalCoord.y >= 0 && currentInternalCoord.y < hoveredChunk.internalGridSize.y)
                    {
                        // Check for circular brush
                        if (currentEditorTool == EditorTool.CircleBrush)
                        {
                            // Distance from center of brush to current tile center
                            float dist = Vector2.Distance(hoveredInternalTileCoord, currentInternalCoord);
                            if (dist > brushSize - 0.5f) continue; // Only draw if within radius (with a small buffer)
                        }

                        Vector3 tileWorldPos = landData.ChunkCoordToWorldOrigin(hoveredChunkCoord.x, hoveredChunkCoord.y) +
                                               hoveredChunk.GetLocalTileWorldPosition(currentInternalCoord.x, currentInternalCoord.y);

                        Handles.color = new Color(0, 1, 1, 0.3f); // Cyan, semi-transparent
                        Handles.DrawSolidRectangleWithOutline(GetTileRectVertices(tileWorldPos, hoveredChunk.tileSize), Handles.color, Color.cyan);
                    }
                }
            }
        }
        // Draw box selection preview
        else if (currentEditorTool == EditorTool.BoxSelect && isDraggingBox)
        {
            DrawBoxSelectionPreview(boxSelectStartGlobalCoord, boxSelectEndGlobalCoord);
        }
        // Draw lasso selection preview
        else if (currentEditorTool == EditorTool.LassoSelect)
        {
            DrawLassoPreview();
        }
    }

    private void DrawBoxSelectionPreview(Vector2Int startGlobal, Vector2Int endGlobal)
    {
        int minX = Mathf.Min(startGlobal.x, endGlobal.x);
        int maxX = Mathf.Max(startGlobal.x, endGlobal.x);
        int minY = Mathf.Min(startGlobal.y, endGlobal.y);
        int maxY = Mathf.Max(startGlobal.y, endGlobal.y);

        Handles.color = new Color(0, 0.7f, 1, 0.2f); // Blueish, semi-transparent
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2Int currentGlobalTile = new Vector2Int(x, y);
                // Convert global tile coord back to world position for drawing
                Vector3 tileWorldPos = landData.GlobalTileCoordToWorld(currentGlobalTile);
                // Draw a rectangle for each tile in the selection box
                float effectiveTileSize = landData.GetDefaultInternalTileSize(); // Use landData's default
                Handles.DrawSolidRectangleWithOutline(GetTileRectVertices(tileWorldPos, effectiveTileSize), Handles.color, Color.blue);
            }
        }
    }

    private void DrawLassoPreview()
    {
        if (lassoPointsGlobal.Count > 0)
        {
            Handles.color = new Color(1, 0.7f, 0, 0.5f); // Orange, semi-transparent
            for (int i = 0; i < lassoPointsGlobal.Count; i++)
            {
                Vector3 p1 = landData.GlobalTileCoordToWorld(lassoPointsGlobal[i]);
                Vector3 p2 = landData.GlobalTileCoordToWorld(lassoPointsGlobal[(i + 1) % lassoPointsGlobal.Count]);
                Handles.DrawLine(p1, p2);
            }
            // Draw a line from the last point to the mouse position if still drawing
            if (isLassoing && lassoPointsGlobal.Count > 0)
            {
                 Vector3 lastPointWorld = landData.GlobalTileCoordToWorld(lassoPointsGlobal[lassoPointsGlobal.Count - 1]);
                 // Get mouse world position on the ground plane
                 Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                 Plane groundPlane = new Plane(Vector3.up, landData.overallGridOrigin.y);
                 if (groundPlane.Raycast(ray, out float dist))
                 {
                    Vector3 mouseWorldPos = ray.GetPoint(dist);
                    Handles.DrawLine(lastPointWorld, mouseWorldPos);
                 }
            }
        }
    }

    // Draw Fertility Overlay using Gradient
    private void DrawFertilityOverlay()
    {
        for (int chunkY = 0; chunkY < landData.chunkGridSize.y; chunkY++)
        {
            for (int chunkX = 0; chunkX < landData.chunkGridSize.x; chunkX++)
            {
                LandDataChunk chunk = landData.GetChunk(chunkX, chunkY);
                if (chunk == null) continue;

                if (chunk.isExpandedInEditor)
                {
                    Vector3 chunkWorldOrigin = landData.ChunkCoordToWorldOrigin(chunkX, chunkY);
                    for (int y = 0; y < chunk.internalGridSize.y; y++)
                    {
                        for (int x = 0; x < chunk.internalGridSize.x; x++)
                        {
                            CropTileData tile = chunk.GetTileData(x, y);
                            if (tile == null) continue;

                            // Use the gradient to get the color based on fertility (normalized 0-100)
                            Color fertilityColor = fertilityColorGradient.Evaluate(tile.fertility / 100f);
                            Vector3 tileWorldPos = chunkWorldOrigin + chunk.GetLocalTileWorldPosition(x, y);
                            Handles.DrawSolidRectangleWithOutline(GetTileRectVertices(tileWorldPos, chunk.tileSize), fertilityColor, Color.clear);
                        }
                    }
                }
                else // Draw a single color for the whole collapsed chunk
                {
                    // Calculate average fertility for the chunk for a representative color
                    float totalFertility = 0;
                    if (chunk.tilesData != null && chunk.tilesData.Count > 0)
                    {
                        foreach(var tile in chunk.tilesData)
                        {
                            totalFertility += tile.fertility;
                        }
                        totalFertility /= chunk.tilesData.Count;
                    }
                    else
                    {
                        totalFertility = 50f; // Default if chunk is empty/uninitialized, mid-range
                    }

                    // Normalize for the gradient (0-100 range)
                    Color fertilityColor = fertilityColorGradient.Evaluate(totalFertility / 100f);
                    fertilityColor.a = 0.3f; // Slightly more transparent for chunks when collapsed
                    Handles.DrawSolidRectangleWithOutline(GetChunkRectVertices(chunkX, chunkY), fertilityColor, Color.clear);
                }
            }
        }
    }

    // Draw Environmental Influence Visuals
    private void DrawEnvironmentalInfluenceVisuals()
    {
        string[] guids = AssetDatabase.FindAssets("t:FertilityImpacts");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            FertilityImpacts influenceType = AssetDatabase.LoadAssetAtPath<FertilityImpacts>(path);

            if (influenceType == null || influenceType.affectingTags == null || influenceType.affectingTags.Count == 0) continue;

            foreach (string tag in influenceType.affectingTags)
            {
                GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag(tag);
                foreach (GameObject obj in taggedObjects)
                {
                    List<Vector2Int> occupiedTiles = GetOccupiedGlobalTilesForObject(obj);

                    foreach (Vector2Int sourceGlobalTile in occupiedTiles) // Iterate through all occupied tiles
                    {
                        Vector3 occupiedTileWorldPos = landData.GlobalTileCoordToWorld(sourceGlobalTile);
                        float tileSize = landData.GetDefaultInternalTileSize();

                        // Draw the occupied tile in a distinct color (purple)
                        Handles.color = new Color(0.8f, 0.2f, 0.8f, 0.6f);
                        Handles.DrawSolidRectangleWithOutline(GetTileRectVertices(occupiedTileWorldPos, tileSize), Handles.color, Color.magenta);
                        // Label could be simplified or moved to only one source tile for clarity if desired.
                        // Handles.Label(occupiedTileWorldPos + Vector3.up * 0.2f, $"{obj.name}\n({tag})", EditorStyles.miniBoldLabel);


                        // Draw direct spread area from this source tile
                        Handles.color = influenceType.influenceDirection == FertilityImpacts.InfluenceDirection.Positive ?
                                        new Color(0f, 1f, 0f, 0.2f) : new Color(1f, 0f, 0f, 0.2f); // Green for positive, Red for negative
                        DrawCircularTileArea(sourceGlobalTile, influenceType.directSpreadRadius, tileSize);

                        // Draw blend area from this source tile
                        Handles.color = influenceType.influenceDirection == FertilityImpacts.InfluenceDirection.Positive ?
                                        new Color(0.5f, 1f, 0.5f, 0.1f) : new Color(1f, 0.5f, 0.5f, 0.1f); // Lighter for blend
                        DrawCircularTileArea(sourceGlobalTile, influenceType.TotalRadius, tileSize);
                    }
                    // Place the object label only once, typically at the object's center position
                    Vector3 objCenterWorld = landData.GlobalTileCoordToWorld(landData.WorldToGlobalTileCoord(obj.transform.position));
                    Handles.Label(objCenterWorld + Vector3.up * 0.2f, $"{obj.name}\n({tag})", EditorStyles.miniBoldLabel);
                }
            }
        }
    }

    private void DrawCircularTileArea(Vector2Int centerGlobalTile, int radius, float tileSize)
    {
        for (int yOffset = -radius; yOffset <= radius; yOffset++)
        {
            for (int xOffset = -radius; xOffset <= radius; xOffset++)
            {
                Vector2Int currentGlobalTile = centerGlobalTile + new Vector2Int(xOffset, yOffset);

                // Calculate distance in float for accurate circle check
                float dist = Vector2.Distance(centerGlobalTile, currentGlobalTile);

                // Use the radius + 0.5f to ensure tiles whose centers are exactly on the radius or slightly inside are included.
                // This accounts for tile-based discrete checks vs continuous circles.
                if (dist <= radius + 0.5f)
                {
                    Vector3 tileWorldPos = landData.GlobalTileCoordToWorld(currentGlobalTile);
                    Handles.DrawSolidRectangleWithOutline(GetTileRectVertices(tileWorldPos, tileSize), Handles.color, Color.clear);
                }
            }
        }
    }


    // --- NEW METHOD: ApplyAllEnvironmentalInfluences ---
    private void ApplyAllEnvironmentalInfluences()
    {
        // Get all environmental influence definitions from assets
        List<FertilityImpacts> allInfluences = new List<FertilityImpacts>();
        string[] guids = AssetDatabase.FindAssets("t:FertilityImpacts");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            FertilityImpacts influence = AssetDatabase.LoadAssetAtPath<FertilityImpacts>(path);
            if (influence != null)
            {
                allInfluences.Add(influence);
            }
        }

        // Create a temporary dictionary to store the total fertility change for each tile
        // Key: Global Tile Coordinate (Vector2Int)
        // Value: Total fertility change (float)
        Dictionary<Vector2Int, float> tileFertilityChanges = new Dictionary<Vector2Int, float>();

        // Sort influences by priority (higher priority applies later, potentially overriding)
        allInfluences = allInfluences.OrderBy(inf => inf.priority).ToList();

        foreach (FertilityImpacts influence in allInfluences)
        {
            if (influence == null || influence.affectingTags == null || influence.affectingTags.Count == 0) continue;

            foreach (string tag in influence.affectingTags)
            {
                GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag(tag);
                foreach (GameObject obj in taggedObjects)
                {
                    List<Vector2Int> occupiedTiles = GetOccupiedGlobalTilesForObject(obj); // Get all occupied tiles

                    foreach (Vector2Int sourceGlobalTile in occupiedTiles) // Iterate through each occupied tile as a source
                    {
                        // Iterate through all tiles within the influence's total radius originating from this source tile
                        int maxRadius = influence.TotalRadius; // This is directSpreadRadius + blendRadius
                        for (int yOffset = -maxRadius; yOffset <= maxRadius; yOffset++)
                        {
                            for (int xOffset = -maxRadius; xOffset <= maxRadius; xOffset++)
                            {
                                Vector2Int currentGlobalTile = sourceGlobalTile + new Vector2Int(xOffset, yOffset);
                                float distance = Vector2Int.Distance(sourceGlobalTile, currentGlobalTile);

                                // Check if the tile is within the total influence radius
                                if (distance <= maxRadius + 0.5f) // Add 0.5 for a bit of buffer for tile centers
                                {
                                    float calculatedFertilityChange = 0f;

                                    if (distance <= influence.directSpreadRadius + 0.5f) // Within direct spread
                                    {
                                        calculatedFertilityChange = influence.fertilityChange;
                                    }
                                    else // Within blend radius
                                    {
                                        // Calculate interpolation factor: 0 at directSpreadRadius, 1 at TotalRadius
                                        // Clamp to prevent issues if distance is slightly outside expected range
                                        float blendFactor = (distance - influence.directSpreadRadius) / influence.blendRadius;
                                        calculatedFertilityChange = Mathf.Lerp(influence.fertilityChange, 0f, Mathf.Clamp01(blendFactor));
                                    }

                                    // Apply direction
                                    if (influence.influenceDirection == FertilityImpacts.InfluenceDirection.Negative)
                                    {
                                        calculatedFertilityChange *= -1f;
                                    }

                                    // Accumulate fertility change. Higher priority influences will be processed later
                                    // and their effects will be added on top.
                                    if (tileFertilityChanges.ContainsKey(currentGlobalTile))
                                    {
                                        tileFertilityChanges[currentGlobalTile] += calculatedFertilityChange;
                                    }
                                    else
                                    {
                                        tileFertilityChanges[currentGlobalTile] = calculatedFertilityChange;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // Now, apply the accumulated fertility changes to the actual land data tiles
        // Get the internal chunk size from LandData for correct global to internal conversion
        Vector2Int internalChunkSize = landData.GetDefaultInternalChunkSize();

        foreach (var entry in tileFertilityChanges)
        {
            Vector2Int globalTileCoord = entry.Key;
            float totalCalculatedChange = entry.Value;

            int chunkX = globalTileCoord.x / internalChunkSize.x;
            int chunkY = globalTileCoord.y / internalChunkSize.y;

            // Check if chunk coordinates are within bounds of the overall grid
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
                            // Record undo for the chunk before modifying
                            Undo.RecordObject(targetChunk, "Apply Environmental Influence");

                            // Apply the calculated change, clamping between 0 and 100
                            tile.fertility = Mathf.Clamp(tile.fertility + totalCalculatedChange, 0f, 100f);
                            EditorUtility.SetDirty(targetChunk); // Mark the chunk dirty
                        }
                    }
                }
            }
        }
        // No need to call EditorUtility.SetDirty(landData) here, as individual chunks are marked dirty
        // and AssetDatabase.SaveAssets() is called by the button click directly.
    }


    // --- Helper for getting all global tiles an object occupies ---
    private List<Vector2Int> GetOccupiedGlobalTilesForObject(GameObject obj)
    {
        List<Vector2Int> occupiedTiles = new List<Vector2Int>();
        Bounds objectBounds;

        // Try to get bounds from Renderer
        Renderer objRenderer = obj.GetComponentInChildren<Renderer>();
        if (objRenderer != null)
        {
            objectBounds = objRenderer.bounds;
        }
        else // Fallback to Collider bounds
        {
            Collider objCollider = obj.GetComponentInChildren<Collider>();
            if (objCollider != null)
            {
                objectBounds = objCollider.bounds;
            }
            else // Fallback to a small default bounding box around transform.position
            {
                // If no renderer or collider, assume it occupies a 1x1 tile area at its position
                // Use a default tile size for this fallback bound.
                float defaultTileSize = landData.GetDefaultInternalTileSize();
                Vector3 center = obj.transform.position;
                objectBounds = new Bounds(center, Vector3.one * defaultTileSize);
            }
        }

        // --- Debugging Logs (can be commented out once confirmed working) ---
        // Debug.Log($"[Footprint Debug] Object: {obj.name}");
        // Debug.Log($"[Footprint Debug] Transform Position: {obj.transform.position}");
        // Debug.Log($"[Footprint Debug] Transform Scale: {obj.transform.localScale}");
        // Debug.Log($"[Footprint Debug] Object Bounds (World): Center={objectBounds.center}, Size={objectBounds.size}");

        // Convert world bounds min/max to global tile coordinates
        // We use WorldToGlobalTileCoord which essentially floors the world position to a tile index.
        // For the min, we want the tile at or just below the min bound.
        // For the max, we want the tile at or just above the max bound.
        Vector2Int minGlobalTile = landData.WorldToGlobalTileCoord(objectBounds.min);
        Vector2Int maxGlobalTile = landData.WorldToGlobalTileCoord(objectBounds.max);

        // Ensure min <= max for correct iteration order
        int startX = Mathf.Min(minGlobalTile.x, maxGlobalTile.x);
        int endX = Mathf.Max(minGlobalTile.x, maxGlobalTile.x);
        int startY = Mathf.Min(minGlobalTile.y, maxGlobalTile.y);
        int endY = Mathf.Max(minGlobalTile.y, maxGlobalTile.y);

        // Debug.Log($"[Footprint Debug] Raw Global Tile Range from Bounds: X={minGlobalTile.x}-{maxGlobalTile.x}, Y={minGlobalTile.y}-{maxGlobalTile.y}");
        // Debug.Log($"[Footprint Debug] Adjusted Iteration Range: X={startX}-{endX}, Y={startY}-{endY}");


        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                Vector2Int currentTile = new Vector2Int(x, y);
                // Get the world position of the center of this tile
                Vector3 tileCenterWorld = landData.GlobalTileCoordToWorld(currentTile);

                // --- MODIFIED `Contains` CHECK ---
                // For a 2D grid footprint, we only care if the XZ projection of the tile center
                // is within the XZ projection of the object's bounds.
                // We create a 2D rectangle from the bounds' XZ and check if the tile center (XZ) is inside it.
                Vector2 boundsMinXZ = new Vector2(objectBounds.min.x, objectBounds.min.z);
                Vector2 boundsMaxXZ = new Vector2(objectBounds.max.x, objectBounds.max.z);

                Rect boundsRectXZ = new Rect(boundsMinXZ.x, boundsMinXZ.y, boundsMaxXZ.x - boundsMinXZ.x, boundsMaxXZ.y - boundsMinXZ.y);

                Vector2 tileCenterXZ = new Vector2(tileCenterWorld.x, tileCenterWorld.z);

                if (boundsRectXZ.Contains(tileCenterXZ))
                {
                    occupiedTiles.Add(currentTile);
                }
            }
        }

        // Fallback: If for some reason, no tiles were added (e.g., extremely small object,
        // or its bounds don't actually overlap any tile centers, or outside grid calculation range),
        // at least add the tile at its transform position as a fallback.
        if (occupiedTiles.Count == 0)
        {
            Vector2Int centralTile = landData.WorldToGlobalTileCoord(obj.transform.position);
            occupiedTiles.Add(centralTile);
            Debug.LogWarning($"[Footprint Debug] Object {obj.name} at {obj.transform.position} did not occupy any tiles based on its calculated bounds. Falling back to single tile {centralTile}. This might indicate a very small object or bounds not crossing tile centers.");
        }

        // Debug.Log($"[Footprint Debug] Object {obj.name} finalized occupied {occupiedTiles.Count} tiles.");
        return occupiedTiles;
    }


    // --- Apply Brush/Tool Logic ---

    /// <summary>
    /// Applies the current brush settings to a single tile.
    /// </summary>
    private void ApplyBrushToSingleTile(LandDataChunk targetChunk, int internalX, int internalY)
    {
        if (targetChunk == null) return;

        CropTileData tile = targetChunk.GetTileData(internalX, internalY);
        if (tile == null) return;

        // Record undo state for the specific chunk asset *before* modifying
        Undo.RecordObject(targetChunk, "Modify Tile Data");

        if (currentBrushMode == BrushMode.Fertility)
        {
            // Clamp fertility to 0-100
            tile.fertility = Mathf.Clamp(brushFertilityValue, 0f, 100f);
        }
        else if (currentBrushMode == BrushMode.CropType)
        {
            if (brushCropType != null)
            {
                tile.currentCrop = brushCropType;
                tile.currentGrowthStage = 0;
                tile.growthProgress = 0f;
                tile.state = TileState.Sown;
                // Example: Impact fertility when planting (clamped to 0-100)
                tile.fertility = Mathf.Clamp(tile.fertility + brushCropType.fertilityImpact, 0f, 100f);
            }
            else // If brushCropType is null, clear the tile
            {
                tile.currentCrop = null;
                tile.currentGrowthStage = 0;
                tile.growthProgress = 0f;
                tile.state = TileState.Unprepared;
            }
        }
        EditorUtility.SetDirty(targetChunk); // Mark the chunk as dirty after modification
    }

    /// <summary>
    /// Applies the square brush in a square area around a center internal coordinate within a chunk.
    /// </summary>
    private void ApplySquareBrushToArea(LandDataChunk targetChunk, int internalCenterX, int internalCenterY, int size) // Renamed from ApplyBrushToArea
    {
        if (targetChunk == null) return;

        int halfSize = size / 2;
        int startX = internalCenterX - halfSize;
        int startY = internalCenterY - halfSize;
        int endX = internalCenterX + halfSize;
        int endY = internalCenterY + halfSize;

        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                // Ensure internal coordinates are within chunk bounds
                if (x >= 0 && x < targetChunk.internalGridSize.x && y >= 0 && y < targetChunk.internalGridSize.y)
                {
                    ApplyBrushToSingleTile(targetChunk, x, y);
                }
            }
        }
    }

    /// <summary>
    /// Applies the circular brush in a circular area around a center internal coordinate within a chunk.
    /// </summary>
    private void ApplyCircleBrushToArea(LandDataChunk targetChunk, int internalCenterX, int internalCenterY, int radius) // New CircleBrush apply method
    {
        if (targetChunk == null) return;

        // Iterate through a square bounding box around the circle
        for (int yOffset = -radius; yOffset <= radius; yOffset++)
        {
            for (int xOffset = -radius; xOffset <= radius; xOffset++)
            {
                Vector2Int currentInternalCoord = new Vector2Int(internalCenterX + xOffset, internalCenterY + yOffset);

                // Check if the tile is within the circular radius
                float dist = Vector2.Distance(new Vector2(internalCenterX, internalCenterY), currentInternalCoord);
                if (dist <= radius + 0.5f) // Add 0.5f buffer for tile centers
                {
                    // Ensure internal coordinates are within chunk bounds
                    if (currentInternalCoord.x >= 0 && currentInternalCoord.x < targetChunk.internalGridSize.x &&
                        currentInternalCoord.y >= 0 && currentInternalCoord.y < targetChunk.internalGridSize.y)
                    {
                        ApplyBrushToSingleTile(targetChunk, currentInternalCoord.x, currentInternalCoord.y);
                    }
                }
            }
        }
    }


    /// <summary>
    /// Applies the brush to all tiles within a global box selection.
    /// </summary>
    private void ApplyBrushToBox(Vector2Int startGlobal, Vector2Int endGlobal)
    {
        // Normalize coordinates for iteration
        int minX = Mathf.Min(startGlobal.x, endGlobal.x);
        int maxX = Mathf.Max(startGlobal.x, endGlobal.x);
        int minY = Mathf.Min(startGlobal.y, endGlobal.y);
        int maxY = Mathf.Max(startGlobal.y, endGlobal.y);

        // Iterate through all global tile coordinates within the box
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2Int currentGlobalTile = new Vector2Int(x, y);

                // Determine which chunk and internal tile this global coordinate corresponds to
                Vector2Int internalChunkSize = landData.GetDefaultInternalChunkSize();

                int chunkX = currentGlobalTile.x / internalChunkSize.x;
                int chunkY = currentGlobalTile.y / internalChunkSize.y;

                LandDataChunk targetChunk = landData.GetChunk(chunkX, chunkY);

                if (targetChunk != null)
                {
                    Vector2Int internalTileCoord = new Vector2Int(
                        currentGlobalTile.x % internalChunkSize.x,
                        currentGlobalTile.y % internalChunkSize.y
                    );
                    ApplyBrushToSingleTile(targetChunk, internalTileCoord.x, internalTileCoord.y);
                }
            }
        }
    }

    /// <summary>
    /// Applies the brush to all tiles within the global lasso selection.
    /// </summary>
    private void ApplyBrushToLasso(List<Vector2Int> lassoPointsGlobal)
    {
        if (lassoPointsGlobal.Count < 3) return; // Not a valid polygon

        // Convert global tile coordinates to Vector2 for point-in-polygon check
        Vector2[] polygon = lassoPointsGlobal.Select(p => new Vector2(p.x, p.y)).ToArray();

        // Determine bounding box of the lasso to limit iteration
        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        foreach (Vector2Int p in lassoPointsGlobal)
        {
            minX = Mathf.Min(minX, p.x);
            maxX = Mathf.Max(maxX, p.x);
            minY = Mathf.Min(minY, p.y);
            maxY = Mathf.Max(maxY, p.y);
        }

        // Expand bounding box slightly to ensure tiles on edges are considered
        minX -= 1; maxX += 1;
        minY -= 1; maxY += 1;

        // Iterate through all global tile coordinates within the bounding box
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2Int currentGlobalTile = new Vector2Int(x, y);
                // Use the tile's global coordinate as the point for the polygon check
                Vector2 pointToCheck = new Vector2(currentGlobalTile.x, currentGlobalTile.y);

                if (IsPointInPolygon(polygon, pointToCheck))
                {
                    // Determine which chunk and internal tile this global coordinate corresponds to
                    Vector2Int internalChunkSize = landData.GetDefaultInternalChunkSize();

                    int chunkX = currentGlobalTile.x / internalChunkSize.x;
                    int chunkY = currentGlobalTile.y / internalChunkSize.y;

                    LandDataChunk targetChunk = landData.GetChunk(chunkX, chunkY);

                    if (targetChunk != null)
                    {
                        Vector2Int internalTileCoord = new Vector2Int(
                            currentGlobalTile.x % internalChunkSize.x,
                            currentGlobalTile.y % internalChunkSize.y
                        );
                        ApplyBrushToSingleTile(targetChunk, internalTileCoord.x, internalTileCoord.y);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Checks if a point is inside a polygon using the ray casting algorithm.
    /// </summary>
    /// <param name="polygon">The polygon vertices in order (e.g., global tile coordinates as Vector2).</param>
    /// <param name="point">The point to check (e.g., global tile coordinate as Vector2).</param>
    /// <returns>True if the point is inside the polygon, false otherwise.</returns>
    private bool IsPointInPolygon(Vector2[] polygon, Vector2 point)
    {
        int intersections = 0;
        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2 p1 = polygon[i];
            Vector2 p2 = polygon[(i + 1) % polygon.Length]; // Next point, wraps around for the last segment

            // Check if the ray from 'point' (horizontally to the right) intersects the segment (p1, p2)
            // A point is on the edge if it's collinear and within the segment's bounds
            if (IsPointOnSegment(p1, p2, point))
            {
                return true; // Point is on an edge, consider it inside
            }

            // Check if the segment crosses the horizontal ray extending from 'point'
            if (((p1.y <= point.y && p2.y > point.y) || (p1.y > point.y && p2.y <= point.y)) &&
                (point.x < (p2.x - p1.x) * (point.y - p1.y) / (p2.y - p1.y) + p1.x))
            {
                intersections++;
            }
        }
        // If the number of intersections is odd, the point is inside
        return (intersections % 2 == 1);
    }

    /// <summary>
    /// Checks if a point lies on a line segment.
    /// </summary>
    private bool IsPointOnSegment(Vector2 p1, Vector2 p2, Vector2 point)
    {
        float crossProduct = (point.y - p1.y) * (p2.x - p1.x) - (point.x - p1.x) * (p2.y - p1.y);

        // If cross product is non-zero, points are not collinear
        if (Mathf.Abs(crossProduct) > Mathf.Epsilon) return false;

        // Check if point is within the bounding box of the segment
        bool inX = (point.x >= Mathf.Min(p1.x, p2.x) && point.x <= Mathf.Max(p1.x, p2.x));
        bool inY = (point.y >= Mathf.Min(p1.y, p2.y) && point.y <= Mathf.Max(p1.y, p2.y));

        return inX && inY;
    }


    // --- Helper for generating rectangle vertices for Handles.DrawSolidRectangleWithOutline ---
    private Vector3[] GetChunkRectVertices(int chunkX, int chunkY)
    {
        Vector3 origin = landData.ChunkCoordToWorldOrigin(chunkX, chunkY);
        float chunkSize = landData.chunkSizeUnityUnits;

        Vector3 p1 = new Vector3(origin.x, origin.y, origin.z);
        Vector3 p2 = new Vector3(origin.x + chunkSize, origin.y, origin.z);
        Vector3 p3 = new Vector3(origin.x + chunkSize, origin.y, origin.z + chunkSize);
        Vector3 p4 = new Vector3(origin.x, origin.y, origin.z + chunkSize);

        float displayYOffset = 0.005f; // Slight offset to ensure it's visible above the ground plane
        p1.y += displayYOffset; p2.y += displayYOffset; p3.y += displayYOffset; p4.y += displayYOffset;
        return new Vector3[] { p1, p2, p3, p4 };
    }

    private Vector3[] GetTileRectVertices(Vector3 tileCenterWorldPos, float tileSize)
    {
        float halfSize = tileSize / 2f;

        // Vertices for a square centered at tileCenterWorldPos
        Vector3 p1 = new Vector3(tileCenterWorldPos.x - halfSize, tileCenterWorldPos.y, tileCenterWorldPos.z - halfSize);
        Vector3 p2 = new Vector3(tileCenterWorldPos.x + halfSize, tileCenterWorldPos.y, tileCenterWorldPos.z - halfSize);
        Vector3 p3 = new Vector3(tileCenterWorldPos.x + halfSize, tileCenterWorldPos.y, tileCenterWorldPos.z + halfSize);
        Vector3 p4 = new Vector3(tileCenterWorldPos.x - halfSize, tileCenterWorldPos.y, tileCenterWorldPos.z + halfSize);

        float displayYOffset = 0.01f; // A slightly higher offset for individual tiles
        p1.y += displayYOffset; p2.y += displayYOffset; p3.y += displayYOffset; p4.y += displayYOffset;
        return new Vector3[] { p1, p2, p3, p4 };
    }
}
