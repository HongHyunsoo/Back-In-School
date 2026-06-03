using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Paint-by-number minigame with inspector-defined puzzle list + palette UI buttons.
/// </summary>
public class PixelPaintMinigameController : MonoBehaviour
{
    private const float MinigameSfxBoost = 2f;

    private const string TutorialCompletedPrefKey = "DAY1_MINIGAME_TUTORIAL_PIXELPAINT_DONE";

    public enum PuzzleSelectMode
    {
        FixedIndex,
        SequentialLoop,
        Random
    }

    [Serializable]
    public class PixelPaintPuzzleDefinition
    {
        [Tooltip("Optional label shown in UI.")]
        public string title = "Puzzle";

        [Tooltip("Rows for paint-by-number map.\nUse one of these formats:\n1) \"00112200\" (char per cell)\n2) \"0 0 1 1 2 2 0 0\" (space/comma separated)\n0 means empty.")]
        public string[] rows;

        [Tooltip("Optional per-puzzle palette override. If empty, controller palette is used.")]
        public Color[] customPalette;
    }

    [Header("Config (Optional)")]
    public PixelPaintMinigameConfig config;

    [Header("Puzzle")]
    [Tooltip("Add 3+ puzzles here. You can fully control each pattern.")]
    public List<PixelPaintPuzzleDefinition> puzzles = new List<PixelPaintPuzzleDefinition>();
    public PuzzleSelectMode selectMode = PuzzleSelectMode.SequentialLoop;
    [Tooltip("Used only when selectMode == FixedIndex.")]
    public int fixedPuzzleIndex = 0;

    [Header("Board Visual")]
    public float cellSize = 0.8f;
    public Vector2 boardOrigin = new Vector2(-3.2f, -2.8f);
    [Tooltip("Bigger value makes numbers larger on each cell.")]
    public float numberTextScaleMultiplier = 0.48f;
    [Tooltip("Font for cell numbers.")]
    public TMP_FontAsset numberFontAsset;
    [Range(0f, 1f)] public float emptyCellAlpha = 0f;
    public bool hideEmptyCellOutline = true;
    public Color boardBackgroundColor = new Color(0.08f, 0.08f, 0.08f, 0f);
    public Vector2 boardBackgroundPadding = new Vector2(0.35f, 0.35f);

    [Header("Auto Fit")]
    [Tooltip("Automatically fit board size/position to the current orthographic camera.")]
    public bool autoFitToCamera = true;
    [Range(0.5f, 0.98f)]
    [Tooltip("Screen usage ratio for board fit.")]
    public float fitRatio = 0.92f;

    [Header("Zoom")]
    [Tooltip("Allow mouse wheel zoom on orthographic camera.")]
    public bool enableWheelZoom = true;
    public float zoomSpeed = 3.5f;
    [Tooltip("Higher value = finer wheel zoom step.")]
    public float wheelStepDamping = 4.0f;
    public float minOrthoSize = 0.8f;
    public float maxOrthoSize = 18.0f;

    [Header("Pan")]
    [Tooltip("Hold middle mouse button and drag to pan camera.")]
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
    [Tooltip("If true, all puzzles in list are played in one run.")]
    public bool playAllPuzzlesInOneRun = true;
    [Tooltip("Seconds to show solved picture before moving to next puzzle/end.")]
    public float solvedPreviewSeconds = 1.0f;

    [Header("Audio")]
    public AudioSource audioSource;
    private AudioSource sfxSource;
    public AudioClip loopClip;
    [Range(0f, 1f)] public float loopVolume = 0.16f;
    public AudioClip paintSfx;
    [Range(0f, 1f)] public float paintSfxVolume = 0.5f;
    public AudioClip eraseSfx;
    [Range(0f, 1f)] public float eraseSfxVolume = 0.5f;
    public AudioClip selectSfx;
    [Range(0f, 1f)] public float selectSfxVolume = 0.5f;
    public AudioClip puzzleSolvedSfx;
    [Range(0f, 1f)] public float puzzleSolvedSfxVolume = 0.85f;
    public AudioClip minigameSuccessSfx;
    [Range(0f, 1f)] public float minigameSuccessSfxVolume = 0.9f;
    public AudioClip minigameFailSfx;
    [Range(0f, 1f)] public float minigameFailSfxVolume = 0.85f;
    [Range(0.01f, 0.2f)] public float paintSfxCooldown = 0.04f;

    private static int sequentialCursor = 0;

    private int width;
    private int height;
    private int[,] target;
    private int[,] painted;
    private CellView[,] views;

    private int selectedColor = 1;
    private bool ended;
    private bool solvedWaitForContinue;
    private float solvedContinueAtUnscaledTime;
    private int activePuzzleIndex = -1;
    private string activePuzzleTitle = "";
    private Color[] defaultPalette;
    private readonly List<int> sessionPuzzleOrder = new List<int>();
    private int sessionPuzzleCursor;

    private Camera mainCam;
    private Sprite cellSprite;

    private Canvas uiCanvas;
    private TextMeshProUGUI headerText;
    private readonly List<Button> paletteButtons = new List<Button>();
    private readonly List<Image> paletteButtonImages = new List<Image>();
    private readonly List<GameObject> runtimeBoardObjects = new List<GameObject>();
    private int lastLeftPaintedX = -1;
    private int lastLeftPaintedY = -1;
    private int lastRightPaintedX = -1;
    private int lastRightPaintedY = -1;
    private bool isPanning;
    private Vector3 lastMouseScreenPos;
    private float defaultOrthoSize = -1f;
    private Vector3 defaultCameraPosition;
    private float lastPaintSfxTime = -999f;
    private MinigameTutorialOverlay tutorialOverlay;
    private bool tutorialActive;
    private int tutorialStep;

    private class CellView
    {
        public SpriteRenderer fill;
        public SpriteRenderer edge;
        public TextMeshPro label;
        public TextMeshPro wrongMark;
    }

    private const float RuntimeNumberScale = 0.38f;
    private static readonly Color RuntimeNumberColor = new Color(0.08f, 0.08f, 0.08f, 0.58f);
    private static readonly Color RuntimeNumberOutlineColor = new Color(0f, 0f, 0f, 1f);
    private const float RuntimeNumberOutlineWidth = 0.0f;
    private const float RuntimeNumberOutlineSoftness = 0.0f;
    private static readonly Color WrongMarkColor = new Color(0.95f, 0.08f, 0.08f, 0.92f);

    private void Awake()
    {
        ApplyConfigIfNeeded();

        string flowId = PlayerPrefs.GetString("FLOW_ID", "");
        if (string.IsNullOrEmpty(flowId) || !flowId.StartsWith("CLASS2_"))
        {
            enabled = false;
            return;
        }

        mainCam = Camera.main;
        if (mainCam == null)
            mainCam = FindAnyObjectByType<Camera>();

        defaultPalette = palette != null ? (Color[])palette.Clone() : Array.Empty<Color>();
        EnsureNumberFont();
        EnsureAudioSource();

        EnsurePuzzlesOrFallback();
        BuildSessionPuzzleOrder();
        SelectAndLoadPuzzle();
        EnsurePaletteCapacityForPuzzle();
        AutoFitBoardToCamera();
        CaptureDefaultZoomState();
        // Force runtime value to avoid stale inspector overrides.
        numberTextScaleMultiplier = RuntimeNumberScale;

        BuildBoardVisuals();
        BuildRuntimeUI();
        RefreshHeader();
        RefreshPaletteUI();
        StartLoopIfNeeded();
        BeginTutorialIfNeeded();

        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        if (config == null)
            return;

        ApplyConfigIfNeeded();

        #if UNITY_EDITOR
        EditorUtility.SetDirty(this);
        #endif
    }

    private void ApplyConfigIfNeeded()
    {
        if (config == null)
            return;

        if (config.puzzles != null && config.puzzles.Count > 0)
            puzzles = new List<PixelPaintPuzzleDefinition>(config.puzzles);

        selectMode = config.selectMode;
        fixedPuzzleIndex = config.fixedPuzzleIndex;

        cellSize = config.cellSize;
        boardOrigin = config.boardOrigin;
        numberTextScaleMultiplier = config.numberTextScaleMultiplier;
        if (config.numberFontAsset != null)
            numberFontAsset = config.numberFontAsset;
        emptyCellAlpha = config.emptyCellAlpha;
        hideEmptyCellOutline = config.hideEmptyCellOutline;
        boardBackgroundColor = config.boardBackgroundColor;
        boardBackgroundPadding = config.boardBackgroundPadding;

        autoFitToCamera = config.autoFitToCamera;
        fitRatio = config.fitRatio;

        enableWheelZoom = config.enableWheelZoom;
        zoomSpeed = config.zoomSpeed;
        wheelStepDamping = config.wheelStepDamping;
        minOrthoSize = config.minOrthoSize;
        maxOrthoSize = config.maxOrthoSize;

        enableMiddleMousePan = config.enableMiddleMousePan;
        panSpeed = config.panSpeed;

        if (config.palette != null && config.palette.Length > 0)
            palette = (Color[])config.palette.Clone();
        palettePanelAnchoredPosition = config.palettePanelAnchoredPosition;
        palettePanelSize = config.palettePanelSize;
        paletteButtonSize = config.paletteButtonSize;
        paletteSpacing = config.paletteSpacing;

        penaltyOnGiveUp = config.penaltyOnGiveUp;
        playAllPuzzlesInOneRun = config.playAllPuzzlesInOneRun;
        solvedPreviewSeconds = config.solvedPreviewSeconds;
    }

    private void OnDisable()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;

        CleanupRuntimeUI();
    }

    private void OnDestroy()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;
        CleanupRuntimeUI();
    }

    private void Update()
    {
        if (ended) return;

        StartLoopIfNeeded();
        if (MinigameSettingsPauseController.HandleEscapeOrPaused())
            return;

        if (solvedWaitForContinue)
        {
            bool manualContinue = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
            if (manualContinue || Time.unscaledTime >= solvedContinueAtUnscaledTime)
                ContinueAfterSolved();
            return;
        }

        HandleKeyboardPaletteInput();
        HandleWheelZoom();
        HandleMiddleMousePan();
        HandleMousePaintInput();

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (IsSolved())
                OnSolved();
            else
                RefreshHeader(L("MINIGAME_PIXELPAINT_NOT_SOLVED", "아직 정답이 아닙니다.", "Not solved yet."));
        }

    }

    private void EnsurePuzzlesOrFallback()
    {
        if (puzzles != null && puzzles.Count > 0)
            return;

        puzzles = new List<PixelPaintPuzzleDefinition>
        {
            new PixelPaintPuzzleDefinition
            {
                title = "01",
                rows = new[]
                {
                    "0000000000000000000000000000000000000000000000000000000000000000",
                    "0000000000000000000000000000000000000000000000000000000000000000",
                    "0000000000000000000000000022200000000000000000000000000000000000",
                    "000000000000000000000000002BB20000000000000000000000000000000000",
                    "000000000000000000000000002BBB2000000000000000000000000000000000",
                    "0000000000000000000000000002B2B200000000000000000000000000000000",
                    "00000000000000000000000000022BBB20000000000000000000000000000000",
                    "0022200000000000000000000002BBBBB2000000000000000000000000000000",
                    "002BB200000000000000000000002BB22B200000000000000000000000000000",
                    "002BBB20000000000000000000002B2CCCC20000000000000000000000000000",
                    "0002BBB200000000000022200000022CCC2C2200000220000000000000000000",
                    "00002B2B2200000000026662220002CCC2CEEE20002662000000000000000000",
                    "000002BBBB222000002666266722222C2CEE22D2026666200000000000000000",
                    "000002BB22CCC222226662677777772C2EE2DDDD266666620000000000000000",
                    "0000002B2CCEEE2DD2666277777722822EE2DDD2666622272000000000000000",
                    "00000002CCCEE2ED2666267777728888822DDD62666277777200000000000000",
                    "000000002CCE2EDD266626777728888888422662662777777820000000000000",
                    "00000000022E2EDD266626777288888822244262627777822282000000200000",
                    "0000000000022EDD266662777288888244444422627788288884200002120000",
                    "00000000000002DDD26662777288882444444213222782884444200023120000",
                    "000000000000002DD62622277288882444444211532282844444420021120000",
                    "00000000000000022662EEE227888244444442111531222444444E2211112000",
                    "0000000000000000022EEEEE22288244444442111151111222222E2111112000",
                    "0000000000000000002EEEEE2332824444444511115111111133321111112000",
                    "00000000000000000002EEE23333224444422511111511111111333111132000",
                    "222222200000000000002E233333332222235311111511111111133111120000",
                    "2BBBB2C222200000000002333311133333335331111511111111113311320000",
                    "02BB2CCCEE222222220002333111111111135331111511111111111111200000",
                    "00222CCEE2D62777292222333111111111111133331111111111111444200000",
                    "0000022E2D627772884423331111111111111111114444111111114444200000",
                    "0000002E2D627782844423311111111111111111144444411111114449200000",
                    "0000000022627782444233311111111111111111199944411111119991120000",
                    "0000000000222782444233311111111111111111111199911111111111120000",
                    "0000000000000222944233111111111111111111111111111111111122220000",
                    "00000000000000002222331111111111111111222222211111111112G9920000",
                    "00000000000000000022331111111111111122GGGGG9921111111121GG920000",
                    "0000000000000000000233111111111111129GGG1GGG9921111112G1GG920000",
                    "0000000000000000000233111111111111299GGG1GGG9921111112G1GG920000",
                    "0000000000000000000233111111111111299GGG1GGG9921111115G1GF420000",
                    "0000000000000002222555555111111111244FFG1GFF4493111115GGFF420000",
                    "0000000000000222111111111331111111244FFFGFFF4493111113FFF4920000",
                    "00000000000002311111111111111111113944FFFFF444931111134444200000",
                    "000000000000022331111111111111111AA39444444444311111113999200000",
                    "00000000000000223331111111111111AAAAA399999993111111111133320000",
                    "00000000000000022333311111111111AAAAAAAAA33331111111111111112200",
                    "000000000000000022233331111111111AAAAAAAA1111111111111111AA11120",
                    "00000000000000000022333111111111111AAAAA1111111111111111AAAAA112",
                    "000000000000000000022331111111111111111111111111111111153AAAA512",
                    "0000000000000000002211111111111111111111511111111111111153AA3512",
                    "0000000000000000022111111111111111111111351111111111111115335112",
                    "0000000000000000021111111111111111111111115511111111111111551112",
                    "0000000000000000221111111333311111111111111511115531111111151120",
                    "0000000000000000233333333335511111111111111511351155111113551200",
                    "0000000000000000222222255553311111111111111153511111555113513200",
                    "0000000000000000000000002333311111111111111155111111111555332000",
                    "0000000000000000000000002333331111111111111111111111111113220000",
                    "0000000000000000000000021113333333333111111111111111111332000000",
                    "0000000000000000000000021111111333333335551111111333333220000000",
                    "0000000000000000000000211111111113333333335555555222222000000000",
                    "0000000000000000000002311111111111113333333333332000000000000000",
                    "0000000000000000000023111111111111111133333333332000000000000000",
                    "0000000000000000000021111111111111111111133333332200000000000000",
                    "0000000000000000000231111111111111111111111113333200000000000000",
                    "0000000000000000000211111111111111111111111111133220000000000000"
                }
            }
        };
    }

    private void SelectAndLoadPuzzle()
    {
        if (sessionPuzzleOrder.Count <= 0)
            BuildSessionPuzzleOrder();

        if (sessionPuzzleOrder.Count <= 0)
            throw new InvalidOperationException("PixelPaint puzzle list is empty.");

        sessionPuzzleCursor = Mathf.Clamp(sessionPuzzleCursor, 0, sessionPuzzleOrder.Count - 1);
        activePuzzleIndex = sessionPuzzleOrder[sessionPuzzleCursor];

        ParsePuzzle(puzzles[activePuzzleIndex]);
        activePuzzleTitle = string.IsNullOrEmpty(puzzles[activePuzzleIndex].title)
            ? $"Puzzle {activePuzzleIndex + 1}"
            : puzzles[activePuzzleIndex].title;
        ApplyPuzzlePalette(puzzles[activePuzzleIndex]);
    }

    private void BuildSessionPuzzleOrder()
    {
        sessionPuzzleOrder.Clear();

        int count = puzzles != null ? puzzles.Count : 0;
        if (count <= 0)
            return;

        if (!playAllPuzzlesInOneRun)
        {
            sessionPuzzleOrder.Add(ResolveInitialPuzzleIndex(count));
            sessionPuzzleCursor = 0;
            return;
        }

        if (selectMode == PuzzleSelectMode.Random)
        {
            for (int i = 0; i < count; i++)
                sessionPuzzleOrder.Add(i);

            for (int i = sessionPuzzleOrder.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                int tmp = sessionPuzzleOrder[i];
                sessionPuzzleOrder[i] = sessionPuzzleOrder[j];
                sessionPuzzleOrder[j] = tmp;
            }
        }
        else
        {
            int start = ResolveInitialPuzzleIndex(count);
            for (int i = 0; i < count; i++)
                sessionPuzzleOrder.Add((start + i) % count);
        }

        sessionPuzzleCursor = 0;
    }

    private int ResolveInitialPuzzleIndex(int count)
    {
        if (count <= 0)
            return 0;

        switch (selectMode)
        {
            case PuzzleSelectMode.FixedIndex:
                return Mathf.Clamp(fixedPuzzleIndex, 0, count - 1);

            case PuzzleSelectMode.Random:
                return UnityEngine.Random.Range(0, count);

            case PuzzleSelectMode.SequentialLoop:
            default:
                int index = Mathf.Abs(sequentialCursor) % count;
                sequentialCursor++;
                return index;
        }
    }

    private void ApplyPuzzlePalette(PixelPaintPuzzleDefinition puzzle)
    {
        if (puzzle != null && puzzle.customPalette != null && puzzle.customPalette.Length > 0)
        {
            palette = (Color[])puzzle.customPalette.Clone();
            return;
        }

        palette = defaultPalette != null ? (Color[])defaultPalette.Clone() : Array.Empty<Color>();
    }

    private void ParsePuzzle(PixelPaintPuzzleDefinition puzzle)
    {
        if (puzzle == null || puzzle.rows == null || puzzle.rows.Length == 0)
            throw new InvalidOperationException("Invalid puzzle definition.");

        List<List<int>> rowsParsed = new List<List<int>>();
        int maxWidth = 0;

        for (int r = 0; r < puzzle.rows.Length; r++)
        {
            string raw = puzzle.rows[r];
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var parsed = ParseRow(raw);
            if (parsed.Count == 0)
                continue;

            rowsParsed.Add(parsed);
            maxWidth = Mathf.Max(maxWidth, parsed.Count);
        }

        if (rowsParsed.Count == 0 || maxWidth == 0)
            throw new InvalidOperationException("Puzzle rows are empty after parsing.");

        width = maxWidth;
        height = rowsParsed.Count;

        target = new int[width, height];
        painted = new int[width, height];

        // Input rows are treated as top->bottom.
        for (int srcY = 0; srcY < rowsParsed.Count; srcY++)
        {
            int dstY = (height - 1) - srcY;
            var row = rowsParsed[srcY];
            for (int x = 0; x < row.Count; x++)
                target[x, dstY] = Mathf.Max(0, row[x]);
        }
    }

    private List<int> ParseRow(string raw)
    {
        List<int> values = new List<int>();

        if (raw.Contains(" ") || raw.Contains(",") || raw.Contains("\t"))
        {
            string[] tokens = raw.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                if (int.TryParse(tokens[i], out int v))
                    values.Add(Mathf.Max(0, v));
            }
            return values;
        }

        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (c == '.' || c == '-' || c == '_')
            {
                values.Add(0);
                continue;
            }
            if (c >= '0' && c <= '9')
            {
                values.Add(c - '0');
                continue;
            }
            if (c >= 'A' && c <= 'Z')
            {
                values.Add(10 + (c - 'A'));
                continue;
            }
            if (c >= 'a' && c <= 'z')
            {
                values.Add(10 + (c - 'a'));
                continue;
            }
        }

        return values;
    }

    private void EnsurePaletteCapacityForPuzzle()
    {
        int maxColor = 1;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
                maxColor = Mathf.Max(maxColor, target[x, y]);
        }

        if (palette == null)
            palette = Array.Empty<Color>();

        if (palette.Length >= maxColor)
            return;

        Color[] expanded = new Color[maxColor];
        for (int i = 0; i < expanded.Length; i++)
        {
            if (i < palette.Length)
            {
                expanded[i] = palette[i];
            }
            else
            {
                float h = (i * 0.173f) % 1f;
                expanded[i] = Color.HSVToRGB(h, 0.65f, 0.95f);
            }
        }
        palette = expanded;
    }

    private void AutoFitBoardToCamera()
    {
        if (!autoFitToCamera) return;
        if (mainCam == null) return;
        if (!mainCam.orthographic) return;
        if (width <= 0 || height <= 0) return;

        float camWorldHeight = mainCam.orthographicSize * 2f;
        float camWorldWidth = camWorldHeight * mainCam.aspect;

        float usableWidth = camWorldWidth * Mathf.Clamp(fitRatio, 0.5f, 0.98f);
        float usableHeight = camWorldHeight * Mathf.Clamp(fitRatio, 0.5f, 0.98f);

        float sizeByWidth = usableWidth / width;
        float sizeByHeight = usableHeight / height;
        float fittedCell = Mathf.Min(sizeByWidth, sizeByHeight);

        // Keep within reasonable bounds to avoid zero/negative scale.
        cellSize = Mathf.Clamp(fittedCell, 0.02f, 3f);

        float boardWorldWidth = width * cellSize;
        float boardWorldHeight = height * cellSize;

        Vector3 camPos = mainCam.transform.position;
        boardOrigin = new Vector2(
            camPos.x - (boardWorldWidth * 0.5f),
            camPos.y - (boardWorldHeight * 0.5f));
    }

    private void BuildBoardVisuals()
    {
        views = new CellView[width, height];
        cellSprite = CreateSolidSprite();
        BuildBoardBackground();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var root = new GameObject($"Cell_{x}_{y}");
                root.transform.SetParent(transform, false);
                root.transform.position = CellToWorld(x, y);
                runtimeBoardObjects.Add(root);

                var fillGo = new GameObject("Fill");
                fillGo.transform.SetParent(root.transform, false);
                var fill = fillGo.AddComponent<SpriteRenderer>();
                fill.sprite = cellSprite;
                fill.drawMode = SpriteDrawMode.Sliced;
                fill.size = new Vector2(cellSize * 0.94f, cellSize * 0.94f);
                fill.sortingOrder = 10;

                var edgeGo = new GameObject("Edge");
                edgeGo.transform.SetParent(root.transform, false);
                var edge = edgeGo.AddComponent<SpriteRenderer>();
                edge.sprite = cellSprite;
                edge.drawMode = SpriteDrawMode.Sliced;
                edge.size = new Vector2(cellSize, cellSize);
                edge.color = new Color(0.15f, 0.15f, 0.15f, 1f);
                edge.sortingOrder = 9;

                var textGo = new GameObject("Number");
                textGo.transform.SetParent(root.transform, false);
                var number = textGo.AddComponent<TextMeshPro>();
                number.alignment = TextAlignmentOptions.Center;
                number.fontSize = Mathf.Clamp(cellSize * 38f, 4f, 18f);
                number.fontStyle = FontStyles.Bold;
                number.sortingOrder = 12;
                ApplyRuntimeNumberStyle(number);
                number.text = target[x, y] > 0 ? target[x, y].ToString() : "";
                // Keep label readable but bounded to cell size.
                float textScale = Mathf.Clamp(cellSize * numberTextScaleMultiplier, 0.08f, 0.65f);
                textGo.transform.localScale = new Vector3(textScale, textScale, 1f);

                var wrongGo = new GameObject("WrongMark");
                wrongGo.transform.SetParent(root.transform, false);
                var wrongMark = wrongGo.AddComponent<TextMeshPro>();
                wrongMark.text = "X";
                wrongMark.alignment = TextAlignmentOptions.Center;
                wrongMark.fontSize = Mathf.Clamp(cellSize * 46f, 8f, 22f);
                wrongMark.fontStyle = FontStyles.Bold;
                wrongMark.sortingOrder = 13;
                wrongMark.color = WrongMarkColor;
                wrongMark.enabled = false;
                ApplyRuntimeUIFont(wrongMark);
                float wrongScale = Mathf.Clamp(cellSize * 0.56f, 0.12f, 0.76f);
                wrongGo.transform.localScale = new Vector3(wrongScale, wrongScale, 1f);

                var col = root.AddComponent<BoxCollider2D>();
                col.size = new Vector2(cellSize * 0.96f, cellSize * 0.96f);

                var marker = root.AddComponent<PixelPaintCellMarker>();
                marker.x = x;
                marker.y = y;

                views[x, y] = new CellView { fill = fill, edge = edge, label = number, wrongMark = wrongMark };
                RefreshCellVisual(x, y);
            }
        }
    }

    private void ApplyRuntimeNumberStyle(TextMeshPro number)
    {
        if (number == null) return;

        if (numberFontAsset != null)
            number.font = numberFontAsset;

        number.enableVertexGradient = false;
        ApplyRuntimeUIFont(number);
        number.extraPadding = false;
        number.color = RuntimeNumberColor;
        number.outlineColor = RuntimeNumberOutlineColor;
        number.outlineWidth = RuntimeNumberOutlineWidth;

        // Hard-force face/outline colors at material level too.
        if (number.fontMaterial != null)
        {
            number.fontMaterial.SetColor(ShaderUtilities.ID_FaceColor, RuntimeNumberColor);
            number.fontMaterial.SetColor(ShaderUtilities.ID_OutlineColor, RuntimeNumberOutlineColor);
            number.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, RuntimeNumberOutlineWidth);
            if (number.fontMaterial.HasProperty(ShaderUtilities.ID_OutlineSoftness))
                number.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineSoftness, RuntimeNumberOutlineSoftness);
        }
    }

    private void EnsureNumberFont()
    {
        if (numberFontAsset != null) return;

        #if UNITY_EDITOR
        numberFontAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/Fonts/Galmuri11 SDF.asset");
        #endif
    }

    private void BuildRuntimeUI()
    {
        EnsureEventSystem();

        var canvasGo = new GameObject("__PixelPaintUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        uiCanvas = canvasGo.GetComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.sortingOrder = 6000;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var root = new GameObject("Root", typeof(RectTransform));
        root.transform.SetParent(canvasGo.transform, false);
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        var headerGo = new GameObject("Header", typeof(RectTransform), typeof(TextMeshProUGUI));
        headerGo.transform.SetParent(root.transform, false);
        var headerRect = headerGo.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, -20f);
        headerRect.sizeDelta = new Vector2(0f, 72f);

        headerText = headerGo.GetComponent<TextMeshProUGUI>();
        headerText.alignment = TextAlignmentOptions.Center;
        headerText.fontSize = 30f;
        headerText.color = Color.white;
        ApplyRuntimeUIFont(headerText);

        var palettePanel = new GameObject("PalettePanel", typeof(RectTransform), typeof(GridLayoutGroup));
        palettePanel.transform.SetParent(root.transform, false);
        var panelRect = palettePanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0.5f);
        panelRect.anchorMax = new Vector2(0f, 0.5f);
        panelRect.pivot = new Vector2(0f, 0.5f);
        panelRect.anchoredPosition = palettePanelAnchoredPosition;

        int paletteCount = Mathf.Max(1, palette != null ? palette.Length : 0);
        int columns = paletteCount > 8 ? 2 : 1;
        int rows = Mathf.CeilToInt(paletteCount / (float)columns);
        float availableHeight = Mathf.Max(120f, Mathf.Min(palettePanelSize.y, 920f));
        float availableWidth = Mathf.Max(80f, palettePanelSize.x * columns);
        float fittedByHeight = (availableHeight - Mathf.Max(0, rows - 1) * paletteSpacing) / rows;
        float fittedByWidth = (availableWidth - Mathf.Max(0, columns - 1) * paletteSpacing) / columns;
        float buttonEdge = Mathf.Clamp(Mathf.Min(paletteButtonSize.x, paletteButtonSize.y, fittedByHeight, fittedByWidth), 38f, Mathf.Max(38f, Mathf.Min(paletteButtonSize.x, paletteButtonSize.y)));
        Vector2 resolvedButtonSize = new Vector2(buttonEdge, buttonEdge);
        panelRect.sizeDelta = new Vector2(
            columns * buttonEdge + Mathf.Max(0, columns - 1) * paletteSpacing,
            rows * buttonEdge + Mathf.Max(0, rows - 1) * paletteSpacing);

        var grid = palettePanel.GetComponent<GridLayoutGroup>();
        grid.spacing = new Vector2(paletteSpacing, paletteSpacing);
        grid.cellSize = resolvedButtonSize;
        grid.childAlignment = TextAnchor.MiddleCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;

        paletteButtons.Clear();
        paletteButtonImages.Clear();

        for (int i = 0; i < palette.Length; i++)
        {
            int colorIndex = i + 1;

            var btnGo = new GameObject($"Color_{colorIndex}",
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(Outline));
            btnGo.transform.SetParent(palettePanel.transform, false);

            var btnRect = btnGo.GetComponent<RectTransform>();
            btnRect.sizeDelta = resolvedButtonSize;

            var layout = btnGo.GetComponent<LayoutElement>();
            layout.preferredWidth = resolvedButtonSize.x;
            layout.preferredHeight = resolvedButtonSize.y;

            var image = btnGo.GetComponent<Image>();
            image.color = palette[i];

            var outline = btnGo.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(3f, -3f);

            var button = btnGo.GetComponent<Button>();
            button.onClick.AddListener(() => SetSelectedColor(colorIndex));

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(btnGo.transform, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var label = textGo.GetComponent<TextMeshProUGUI>();
            label.text = colorIndex.ToString();
            label.fontSize = Mathf.Clamp(buttonEdge * 0.34f, 14f, 24f);
            label.alignment = TextAlignmentOptions.Center;
            label.color = (palette[i].grayscale < 0.45f) ? Color.white : Color.black;
            label.raycastTarget = false;
            ApplyRuntimeUIFont(label);

            paletteButtons.Add(button);
            paletteButtonImages.Add(image);
        }
    }

    private void ApplyRuntimeUIFont(TMP_Text text)
    {
        if (text == null)
            return;

        if (numberFontAsset != null)
            text.font = numberFontAsset;

        text.enableWordWrapping = false;
        text.extraPadding = true;
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
            return;

        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(go);
    }

    private void HandleKeyboardPaletteInput()
    {
        int max = palette.Length;
        if (max <= 0) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) SetSelectedColor(1);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SetSelectedColor(2);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SetSelectedColor(3);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SetSelectedColor(4);
        if (Input.GetKeyDown(KeyCode.Alpha5)) SetSelectedColor(5);
        if (Input.GetKeyDown(KeyCode.Alpha6)) SetSelectedColor(6);
        if (Input.GetKeyDown(KeyCode.Alpha7)) SetSelectedColor(7);
        if (Input.GetKeyDown(KeyCode.Alpha8)) SetSelectedColor(8);
        if (Input.GetKeyDown(KeyCode.Alpha9)) SetSelectedColor(9);

        if (Input.GetKeyDown(KeyCode.Q))
            SetSelectedColor(selectedColor - 1 < 1 ? max : selectedColor - 1);
        if (Input.GetKeyDown(KeyCode.E))
            SetSelectedColor(selectedColor + 1 > max ? 1 : selectedColor + 1);
    }

    private void HandleWheelZoom()
    {
        if (!enableWheelZoom) return;
        if (mainCam == null) return;
        if (!mainCam.orthographic) return;

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) < 0.0001f) return;

        float damp = Mathf.Max(1f, wheelStepDamping);
        float next = mainCam.orthographicSize - ((scroll * zoomSpeed) / damp);
        float zoomOutLimit = defaultOrthoSize > 0f ? defaultOrthoSize : maxOrthoSize;
        mainCam.orthographicSize = Mathf.Clamp(next, minOrthoSize, zoomOutLimit);
        ClampCameraToDefaultBounds();
    }

    private void HandleMiddleMousePan()
    {
        if (!enableMiddleMousePan) return;
        if (mainCam == null) return;

        if (Input.GetMouseButtonDown(2))
        {
            isPanning = true;
            lastMouseScreenPos = Input.mousePosition;
            return;
        }

        if (Input.GetMouseButtonUp(2))
        {
            isPanning = false;
            return;
        }

        if (!isPanning || !Input.GetMouseButton(2))
            return;

        Vector3 current = Input.mousePosition;
        Vector3 delta = current - lastMouseScreenPos;
        lastMouseScreenPos = current;

        if (delta.sqrMagnitude < 0.0001f)
            return;

        if (mainCam.orthographic)
        {
            float unitsPerPixel = (mainCam.orthographicSize * 2f) / Mathf.Max(1, mainCam.pixelHeight);
            Vector3 move = new Vector3(-delta.x * unitsPerPixel, -delta.y * unitsPerPixel, 0f) * panSpeed;
            mainCam.transform.position += move;
            ClampCameraToDefaultBounds();
        }
        else
        {
            Vector3 move = new Vector3(-delta.x, -delta.y, 0f) * (0.01f * panSpeed);
            mainCam.transform.Translate(move, Space.Self);
        }
    }

    private void SetSelectedColor(int colorIndex)
    {
        selectedColor = Mathf.Clamp(colorIndex, 1, palette.Length);
        PlayOneShot(selectSfx, selectSfxVolume);
        RefreshHeader();
        RefreshPaletteUI();
    }

    private void HandleMousePaintInput()
    {
        if (mainCam == null) return;
        if (solvedWaitForContinue) return;

        bool isLeftHold = Input.GetMouseButton(0);
        bool isRightHold = Input.GetMouseButton(1);
        if (IsPointerOverRuntimeUI())
        {
            ResetStrokeAnchors();
            return;
        }

        if (!isLeftHold && !isRightHold)
        {
            ResetStrokeAnchors();
            return;
        }

        if (!TryGetHoveredCellInBounds(out int x, out int y))
            return;

        if (isLeftHold)
        {
            PaintStroke(lastLeftPaintedX, lastLeftPaintedY, x, y, selectedColor);
            lastLeftPaintedX = x;
            lastLeftPaintedY = y;
        }
        else
        {
            lastLeftPaintedX = -1;
            lastLeftPaintedY = -1;
        }

        if (isRightHold)
        {
            PaintStroke(lastRightPaintedX, lastRightPaintedY, x, y, 0);
            lastRightPaintedX = x;
            lastRightPaintedY = y;
        }
        else
        {
            lastRightPaintedX = -1;
            lastRightPaintedY = -1;
        }

        if (!tutorialActive && IsSolved())
            OnSolved();
    }

    private bool IsPointerOverRuntimeUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void ResetStrokeAnchors()
    {
        lastLeftPaintedX = -1;
        lastLeftPaintedY = -1;
        lastRightPaintedX = -1;
        lastRightPaintedY = -1;
    }

    private bool TryGetHoveredCellInBounds(out int x, out int y)
    {
        x = -1;
        y = -1;

        Vector3 world = mainCam.ScreenToWorldPoint(Input.mousePosition);
        x = Mathf.FloorToInt((world.x - boardOrigin.x) / cellSize);
        y = Mathf.FloorToInt((world.y - boardOrigin.y) / cellSize);

        return InBounds(x, y);
    }

    private void PaintStroke(int fromX, int fromY, int toX, int toY, int colorIndex)
    {
        if (!InBounds(toX, toY)) return;

        // First point of stroke.
        if (!InBounds(fromX, fromY))
        {
            PaintCellIfNeeded(toX, toY, colorIndex);
            return;
        }

        // Bresenham line to prevent gaps when dragging quickly.
        int x0 = fromX;
        int y0 = fromY;
        int x1 = toX;
        int y1 = toY;

        int dx = Mathf.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            PaintCellIfNeeded(x0, y0, colorIndex);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x0 += sx;
            }
            if (e2 <= dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    private void PaintCellIfNeeded(int x, int y, int colorIndex)
    {
        if (!InBounds(x, y)) return;
        if (target[x, y] == 0) return;

        if (painted[x, y] == colorIndex) return;
        painted[x, y] = colorIndex;
        PlayPaintSfx(colorIndex == 0 ? eraseSfx : paintSfx, colorIndex == 0 ? eraseSfxVolume : paintSfxVolume);
        RefreshCellVisual(x, y);
        HandleTutorialPaintProgress(colorIndex);
    }

    private void BeginTutorialIfNeeded()
    {
        tutorialOverlay = MinigameTutorialOverlay.Ensure(transform);
        if (!ShouldShowTutorial())
        {
            if (tutorialOverlay != null)
                tutorialOverlay.Hide();
            return;
        }

        tutorialActive = true;
        tutorialStep = 0;
        ShowTutorialStep();
    }

    private bool ShouldShowTutorial()
    {
        if (PlayerPrefs.GetInt(GetTutorialPrefKey(), 0) == 1)
            return false;

        if (FlowManager.Instance != null)
            return FlowManager.Instance.day == 1;

        return true;
    }

    private void ShowTutorialStep()
    {
        if (tutorialOverlay == null)
            return;

        if (tutorialStep == 0)
        {
            tutorialOverlay.Show("튜토리얼", "숫자가 있는 칸을 한 번 칠해보자.", "1 / 2 픽셀 페인트");
            return;
        }

        tutorialOverlay.Show("튜토리얼", "이번엔 오른쪽 클릭으로 한 칸 지워보자.", "2 / 2 픽셀 페인트");
    }

    private void HandleTutorialPaintProgress(int colorIndex)
    {
        if (!tutorialActive)
            return;

        if (tutorialStep == 0 && colorIndex != 0)
        {
            tutorialStep = 1;
            ShowTutorialStep();
            return;
        }

        if (tutorialStep == 1 && colorIndex == 0)
            CompleteTutorial();
    }

    private void CompleteTutorial()
    {
        tutorialActive = false;
        PlayerPrefs.SetInt(GetTutorialPrefKey(), 1);
        PlayerPrefs.Save();

        if (tutorialOverlay != null)
        {
            tutorialOverlay.PlaySuccess();
            tutorialOverlay.Hide();
        }

        ResetStrokeAnchors();
        RefreshHeader();
    }

    private static string GetTutorialPrefKey()
    {
        string flowId = PlayerPrefs.GetString("FLOW_ID", string.Empty);
        if (string.IsNullOrEmpty(flowId))
            return TutorialCompletedPrefKey;

        return TutorialCompletedPrefKey + "_" + flowId;
    }

    private void ClearPaintedBoard()
    {
        if (painted == null)
            return;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                painted[x, y] = 0;
                RefreshCellVisual(x, y);
            }
        }

        ResetStrokeAnchors();
    }

    private void RefreshHeader(string suffix = null)
    {
        if (headerText == null) return;

        string colorLabel = L("MINIGAME_PIXELPAINT_COLOR", "색상", "Color");
        string controls = L("MINIGAME_PIXELPAINT_CONTROLS", "좌클릭 칠하기 / 우클릭 지우기", "LMB Paint / RMB Erase");
        string format = L("MINIGAME_PIXELPAINT_HEADER_FMT", "{0} ({1}/{2})  |  {3} {4}  |  {5}", "{0} ({1}/{2})  |  {3} {4}  |  {5}");

        string text =
            string.Format(format,
                activePuzzleTitle,
                activePuzzleIndex + 1,
                puzzles.Count,
                colorLabel,
                selectedColor,
                controls);

        if (!string.IsNullOrEmpty(suffix))
            text += $"  |  {suffix}";

        headerText.text = text;
    }

    private void RefreshPaletteUI()
    {
        for (int i = 0; i < paletteButtons.Count; i++)
        {
            bool selected = (i + 1) == selectedColor;
            var t = paletteButtons[i].transform as RectTransform;
            if (t != null)
                t.localScale = selected ? new Vector3(1.12f, 1.12f, 1f) : Vector3.one;

            var img = paletteButtonImages[i];
            if (img != null)
            {
                Color baseColor = palette[i];
                img.color = selected ? Color.Lerp(baseColor, Color.white, 0.22f) : baseColor;
            }
        }
    }

    private void RefreshCellVisual(int x, int y)
    {
        if (!InBounds(x, y)) return;
        var v = views[x, y];
        if (v == null || v.fill == null) return;

        if (target[x, y] == 0)
        {
            v.fill.color = new Color(0f, 0f, 0f, emptyCellAlpha);
            if (v.label != null) v.label.text = "";
            if (v.label != null) v.label.enabled = false;
            if (v.wrongMark != null) v.wrongMark.enabled = false;
            if (v.edge != null) v.edge.enabled = !hideEmptyCellOutline;
            return;
        }

        int paint = painted[x, y];
        if (paint <= 0)
        {
            v.fill.color = Color.white;
            if (v.label != null)
            {
                v.label.enabled = true;
                v.label.color = new Color(RuntimeNumberColor.r, RuntimeNumberColor.g, RuntimeNumberColor.b, 0.58f);
            }
            if (v.wrongMark != null) v.wrongMark.enabled = false;
            if (v.edge != null) v.edge.enabled = true;
            return;
        }

        int idx = Mathf.Clamp(paint - 1, 0, palette.Length - 1);
        v.fill.color = palette[idx];
        if (v.label != null)
        {
            // Keep number visible after painting so player can verify target color index.
            v.label.enabled = true;
            v.label.color = new Color(RuntimeNumberColor.r, RuntimeNumberColor.g, RuntimeNumberColor.b, 0.40f);
        }
        if (v.wrongMark != null)
            v.wrongMark.enabled = paint != target[x, y];
        if (v.edge != null) v.edge.enabled = true;
    }

    private bool IsSolved()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (target[x, y] == 0) continue;
                if (painted[x, y] != target[x, y]) return false;
            }
        }
        return true;
    }

    private void OnSolved()
    {
        if (solvedWaitForContinue || ended) return;

        solvedWaitForContinue = true;
        PlayOneShot(puzzleSolvedSfx, puzzleSolvedSfxVolume);
        solvedContinueAtUnscaledTime = Time.unscaledTime + Mathf.Max(0.05f, solvedPreviewSeconds);
        FitCameraToBoardOverview();
        HideBoardOutlinesAndNumbers();
        RefreshHeader(L("MINIGAME_PIXELPAINT_COMPLETED", "완성! 잠시 후 다음 퍼즐", "Completed! Moving to next puzzle."));
    }

    private void ContinueAfterSolved()
    {
        if (!solvedWaitForContinue || ended)
            return;

        solvedWaitForContinue = false;

        bool hasNext = sessionPuzzleCursor + 1 < sessionPuzzleOrder.Count;
        if (!hasNext)
        {
            End(true);
            return;
        }

        sessionPuzzleCursor++;
        SelectAndLoadPuzzle();
        EnsurePaletteCapacityForPuzzle();
        AutoFitBoardToCamera();
        CaptureDefaultZoomState();
        ClearBoardVisuals();
        BuildBoardVisuals();

        // Palette size can change per puzzle.
        CleanupRuntimeUI();
        BuildRuntimeUI();

        selectedColor = 1;
        RefreshHeader();
        RefreshPaletteUI();
    }

    private void FitCameraToBoardOverview()
    {
        if (mainCam == null || !mainCam.orthographic || width <= 0 || height <= 0)
            return;

        float ratio = Mathf.Clamp(fitRatio, 0.5f, 0.98f);
        float boardWorldWidth = width * cellSize;
        float boardWorldHeight = height * cellSize;

        float sizeByHeight = boardWorldHeight / (2f * ratio);
        float sizeByWidth = boardWorldWidth / (2f * Mathf.Max(0.01f, mainCam.aspect) * ratio);
        float targetSize = Mathf.Max(sizeByHeight, sizeByWidth);

        Vector3 camPos = mainCam.transform.position;
        camPos.x = boardOrigin.x + (boardWorldWidth * 0.5f);
        camPos.y = boardOrigin.y + (boardWorldHeight * 0.5f);
        mainCam.transform.position = camPos;
        float zoomOutLimit = defaultOrthoSize > 0f ? defaultOrthoSize : maxOrthoSize;
        mainCam.orthographicSize = Mathf.Clamp(targetSize, minOrthoSize, zoomOutLimit);
    }

    private void CaptureDefaultZoomState()
    {
        if (mainCam == null || !mainCam.orthographic)
            return;

        defaultOrthoSize = Mathf.Clamp(mainCam.orthographicSize, minOrthoSize, maxOrthoSize);
        defaultCameraPosition = mainCam.transform.position;
    }

    private void ClampCameraToDefaultBounds()
    {
        if (mainCam == null || !mainCam.orthographic || defaultOrthoSize <= 0f)
            return;

        float defaultHalfHeight = defaultOrthoSize;
        float defaultHalfWidth = defaultHalfHeight * Mathf.Max(0.01f, mainCam.aspect);
        float currentHalfHeight = mainCam.orthographicSize;
        float currentHalfWidth = currentHalfHeight * Mathf.Max(0.01f, mainCam.aspect);

        Vector3 pos = mainCam.transform.position;

        float minX = defaultCameraPosition.x - (defaultHalfWidth - currentHalfWidth);
        float maxX = defaultCameraPosition.x + (defaultHalfWidth - currentHalfWidth);
        float minY = defaultCameraPosition.y - (defaultHalfHeight - currentHalfHeight);
        float maxY = defaultCameraPosition.y + (defaultHalfHeight - currentHalfHeight);

        if (minX > maxX)
            pos.x = defaultCameraPosition.x;
        else
            pos.x = Mathf.Clamp(pos.x, minX, maxX);

        if (minY > maxY)
            pos.y = defaultCameraPosition.y;
        else
            pos.y = Mathf.Clamp(pos.y, minY, maxY);

        mainCam.transform.position = pos;
    }

    private void HideBoardOutlinesAndNumbers()
    {
        if (views == null) return;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var v = views[x, y];
                if (v == null) continue;
                if (v.edge != null) v.edge.enabled = false;
                if (v.label != null) v.label.enabled = false;
            }
        }
    }

    private void End(bool success)
    {
        if (ended) return;
        ended = true;
        StopLoopIfNeeded();
        PlayOneShot(success ? minigameSuccessSfx : minigameFailSfx, success ? minigameSuccessSfxVolume : minigameFailSfxVolume);

        CleanupRuntimeUI();

        Debug.Log($"[PixelPaint] End: {(success ? "SUCCESS" : "FAIL")}");

        if (FlowManager.Instance != null)
        {
            int delta = success ? 0 : penaltyOnGiveUp;
            FlowManager.Instance.CompleteCurrentEvent(delta);
            return;
        }

        var gm = FindAnyObjectByType<GameManager>();
        if (gm != null)
            gm.MinigameFinished(success);
    }

    private void EnsureAudioSource()
    {
        if (loopClip == null)
            loopClip = AudioSettingsService.LoadResourceClip("SFX/MINIGAME/PixelPaint/BGM_PixelPaint");

        if (eraseSfx == null)
            eraseSfx = AudioSettingsService.LoadResourceClip("SFX/MINIGAME/PixelPaint/PixelPaint_Delete");

        if (selectSfx == null)
            selectSfx = AudioSettingsService.LoadResourceClip("SFX/MINIGAME/PixelPaint/PixelPaint_Select");

        if (puzzleSolvedSfx == null)
            puzzleSolvedSfx = AudioSettingsService.LoadResourceClip("SFX/MINIGAME/PixelPaint/PixelPaint_Success");

        if (minigameSuccessSfx == null)
            minigameSuccessSfx = AudioSettingsService.LoadResourceClip("SFX/MINIGAME/PixelPaint/PixelPaint_completed");

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;

        if (sfxSource == null)
            sfxSource = gameObject.AddComponent<AudioSource>();

        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;
        sfxSource.volume = 1f;
    }

    private void StartLoopIfNeeded()
    {
        EnsureAudioSource();
        if (loopClip == null)
            return;

        audioSource.clip = loopClip;
        audioSource.loop = true;
        audioSource.volume = AudioSettingsService.ScaleBgm(loopVolume);
        if (!audioSource.isPlaying)
            audioSource.Play();
    }

    private void StopLoopIfNeeded()
    {
        if (audioSource == null)
            return;

        if (audioSource.isPlaying && audioSource.clip == loopClip)
            audioSource.Stop();
        audioSource.loop = false;
        audioSource.clip = null;
    }

    private void PlayPaintSfx(AudioClip clip, float volume)
    {
        if (Time.unscaledTime - lastPaintSfxTime < paintSfxCooldown)
            return;

        lastPaintSfxTime = Time.unscaledTime;
        PlayOneShot(clip, volume);
    }

    private void PlayOneShot(AudioClip clip, float volume)
    {
        EnsureAudioSource();
        if (clip == null)
            return;

        sfxSource.PlayOneShot(clip, AudioSettingsService.ScaleSfx(Mathf.Max(0f, volume * MinigameSfxBoost)));
    }

    private void CleanupRuntimeUI()
    {
        if (uiCanvas != null)
        {
            Destroy(uiCanvas.gameObject);
            uiCanvas = null;
        }
    }

    private void BuildBoardBackground()
    {
        if (boardBackgroundColor.a <= 0.001f)
            return;

        float boardWorldWidth = width * cellSize;
        float boardWorldHeight = height * cellSize;

        var bgGo = new GameObject("BoardBackground");
        bgGo.transform.SetParent(transform, false);
        bgGo.transform.position = new Vector3(
            boardOrigin.x + (boardWorldWidth * 0.5f),
            boardOrigin.y + (boardWorldHeight * 0.5f),
            1f);

        var bg = bgGo.AddComponent<SpriteRenderer>();
        bg.sprite = cellSprite;
        bg.drawMode = SpriteDrawMode.Sliced;
        bg.size = new Vector2(
            boardWorldWidth + (boardBackgroundPadding.x * 2f),
            boardWorldHeight + (boardBackgroundPadding.y * 2f));
        bg.color = boardBackgroundColor;
        bg.sortingOrder = 1;
        runtimeBoardObjects.Add(bgGo);
    }

    private void ClearBoardVisuals()
    {
        views = null;
        for (int i = 0; i < runtimeBoardObjects.Count; i++)
        {
            if (runtimeBoardObjects[i] != null)
                Destroy(runtimeBoardObjects[i]);
        }
        runtimeBoardObjects.Clear();
    }

    private void OnLanguageChanged(Language _)
    {
        RefreshHeader();
    }

    private string L(string key, string fallbackKO, string fallbackEN)
    {
        Language lang = LocalizationManager.Instance != null ? LocalizationManager.Instance.GetCurrentLanguage() : Language.Korean;
        string fallback = lang == Language.Korean ? fallbackKO : fallbackEN;
        if (LocalizationManager.Instance == null) return fallback;
        string value = LocalizationManager.Instance.GetLine(key);
        return value == key ? fallback : value;
    }

    private bool InBounds(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    private Vector3 CellToWorld(int x, int y)
    {
        return new Vector3(
            boardOrigin.x + (x * cellSize) + (cellSize * 0.5f),
            boardOrigin.y + (y * cellSize) + (cellSize * 0.5f),
            0f
        );
    }

    private Sprite CreateSolidSprite()
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }
}

public class PixelPaintCellMarker : MonoBehaviour
{
    public int x;
    public int y;
}

