using System.Collections.Generic;
using TMPro;
using UnityEngine;

[CreateAssetMenu(
    fileName = "PixelPaintConfig",
    menuName = "BackToSchool/Minigame/Pixel Paint Config",
    order = 12)]
public class PixelPaintMinigameConfig : ScriptableObject
{
    [Header("Puzzle")]
    public List<PixelPaintMinigameController.PixelPaintPuzzleDefinition> puzzles = new List<PixelPaintMinigameController.PixelPaintPuzzleDefinition>();
    public PixelPaintMinigameController.PuzzleSelectMode selectMode = PixelPaintMinigameController.PuzzleSelectMode.SequentialLoop;
    public int fixedPuzzleIndex = 0;

    [Header("Board Visual")]
    public float cellSize = 0.8f;
    public Vector2 boardOrigin = new Vector2(-3.2f, -2.8f);
    public float numberTextScaleMultiplier = 0.48f;
    public TMP_FontAsset numberFontAsset;
    [Range(0f, 1f)] public float emptyCellAlpha = 0f;
    public bool hideEmptyCellOutline = true;
    public Color boardBackgroundColor = new Color(0.08f, 0.08f, 0.08f, 0f);
    public Vector2 boardBackgroundPadding = new Vector2(0.35f, 0.35f);

    [Header("Auto Fit")]
    public bool autoFitToCamera = true;
    [Range(0.5f, 0.98f)] public float fitRatio = 0.92f;

    [Header("Zoom")]
    public bool enableWheelZoom = true;
    public float zoomSpeed = 3.5f;
    public float wheelStepDamping = 4.0f;
    public float minOrthoSize = 0.8f;
    public float maxOrthoSize = 18.0f;

    [Header("Pan")]
    public bool enableMiddleMousePan = true;
    public float panSpeed = 1.0f;

    [Header("Palette (index 1..N)")]
    public Color[] palette = new Color[]
    {
        new Color(0.90f, 0.25f, 0.25f),
        new Color(0.95f, 0.85f, 0.25f),
        new Color(0.30f, 0.70f, 0.95f),
        new Color(0.28f, 0.82f, 0.44f)
    };
    public Vector2 palettePanelAnchoredPosition = new Vector2(88f, 0f);
    public Vector2 palettePanelSize = new Vector2(96f, 760f);
    public Vector2 paletteButtonSize = new Vector2(72f, 72f);
    public float paletteSpacing = 10f;

    [Header("Flow")]
    public int penaltyOnGiveUp = 1;
    public bool playAllPuzzlesInOneRun = true;
    public float solvedPreviewSeconds = 1.0f;
}
