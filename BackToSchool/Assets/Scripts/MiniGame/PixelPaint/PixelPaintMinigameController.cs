using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Paint-by-number minigame with inspector-defined puzzle list + palette UI buttons.
/// </summary>
public class PixelPaintMinigameController : MonoBehaviour
{
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
    }

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

    [Header("Flow")]
    public int penaltyOnGiveUp = 1;

    private static int sequentialCursor = 0;

    private int width;
    private int height;
    private int[,] target;
    private int[,] painted;
    private CellView[,] views;

    private int selectedColor = 1;
    private bool ended;
    private bool solvedWaitForContinue;
    private int activePuzzleIndex = -1;
    private string activePuzzleTitle = "";

    private Camera mainCam;
    private Sprite cellSprite;

    private Canvas uiCanvas;
    private TextMeshProUGUI headerText;
    private readonly List<Button> paletteButtons = new List<Button>();
    private readonly List<Image> paletteButtonImages = new List<Image>();
    private int lastLeftPaintedX = -1;
    private int lastLeftPaintedY = -1;
    private int lastRightPaintedX = -1;
    private int lastRightPaintedY = -1;
    private bool isPanning;
    private Vector3 lastMouseScreenPos;

    private class CellView
    {
        public SpriteRenderer fill;
        public SpriteRenderer edge;
        public TextMeshPro label;
    }

    private const float RuntimeNumberScale = 0.52f;
    private static readonly Color RuntimeNumberColor = new Color(0.08f, 0.08f, 0.08f, 1f);
    private static readonly Color RuntimeNumberOutlineColor = new Color(0f, 0f, 0f, 1f);
    private const float RuntimeNumberOutlineWidth = 0.0f;
    private const float RuntimeNumberOutlineSoftness = 0.0f;

    private void Awake()
    {
        mainCam = Camera.main;
        if (mainCam == null)
            mainCam = FindAnyObjectByType<Camera>();

        EnsureNumberFont();

        EnsurePuzzlesOrFallback();
        SelectAndLoadPuzzle();
        EnsurePaletteCapacityForPuzzle();
        AutoFitBoardToCamera();
        // Force runtime value to avoid stale inspector overrides.
        numberTextScaleMultiplier = RuntimeNumberScale;

        BuildBoardVisuals();
        BuildRuntimeUI();
        RefreshHeader();
        RefreshPaletteUI();
    }

    private void Update()
    {
        if (ended) return;

        if (solvedWaitForContinue)
        {
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
                End(true);
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
                RefreshHeader("Not solved yet.");
        }

        if (Input.GetKeyDown(KeyCode.Escape))
            End(false);
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
        int count = puzzles.Count;
        if (count <= 0)
            throw new InvalidOperationException("PixelPaint puzzle list is empty.");

        switch (selectMode)
        {
            case PuzzleSelectMode.FixedIndex:
                activePuzzleIndex = Mathf.Clamp(fixedPuzzleIndex, 0, count - 1);
                break;

            case PuzzleSelectMode.Random:
                activePuzzleIndex = UnityEngine.Random.Range(0, count);
                break;

            case PuzzleSelectMode.SequentialLoop:
            default:
                activePuzzleIndex = Mathf.Abs(sequentialCursor) % count;
                sequentialCursor++;
                break;
        }

        ParsePuzzle(puzzles[activePuzzleIndex]);
        activePuzzleTitle = string.IsNullOrEmpty(puzzles[activePuzzleIndex].title)
            ? $"Puzzle {activePuzzleIndex + 1}"
            : puzzles[activePuzzleIndex].title;
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

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var root = new GameObject($"Cell_{x}_{y}");
                root.transform.SetParent(transform, false);
                root.transform.position = CellToWorld(x, y);

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
                number.fontSize = Mathf.Clamp(cellSize * 55f, 4f, 24f);
                number.fontStyle = FontStyles.Bold;
                number.sortingOrder = 12;
                ApplyRuntimeNumberStyle(number);
                number.text = target[x, y] > 0 ? target[x, y].ToString() : "";
                // Keep label readable but bounded to cell size.
                float textScale = Mathf.Clamp(cellSize * numberTextScaleMultiplier, 0.08f, 0.65f);
                textGo.transform.localScale = new Vector3(textScale, textScale, 1f);

                var col = root.AddComponent<BoxCollider2D>();
                col.size = new Vector2(cellSize * 0.96f, cellSize * 0.96f);

                var marker = root.AddComponent<PixelPaintCellMarker>();
                marker.x = x;
                marker.y = y;

                views[x, y] = new CellView { fill = fill, edge = edge, label = number };
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
        number.enableWordWrapping = false;
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
            "Assets/Fonts/Galmuri11-Condensed SDF.asset");
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

        var palettePanel = new GameObject("PalettePanel", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        palettePanel.transform.SetParent(root.transform, false);
        var panelRect = palettePanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 28f);
        panelRect.sizeDelta = new Vector2(980f, 92f);

        var h = palettePanel.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 10f;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        h.childControlWidth = false;
        h.childControlHeight = false;
        h.childAlignment = TextAnchor.MiddleCenter;

        paletteButtons.Clear();
        paletteButtonImages.Clear();

        for (int i = 0; i < palette.Length; i++)
        {
            int colorIndex = i + 1;

            var btnGo = new GameObject($"Color_{colorIndex}",
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(Outline));
            btnGo.transform.SetParent(palettePanel.transform, false);

            var btnRect = btnGo.GetComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(78f, 78f);

            var layout = btnGo.GetComponent<LayoutElement>();
            layout.preferredWidth = 78f;
            layout.preferredHeight = 78f;

            var image = btnGo.GetComponent<Image>();
            image.color = palette[i];

            var outline = btnGo.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(3f, -3f);

            var button = btnGo.GetComponent<Button>();
            button.onClick.AddListener(() =>
            {
                selectedColor = colorIndex;
                RefreshHeader();
                RefreshPaletteUI();
            });

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(btnGo.transform, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var label = textGo.GetComponent<TextMeshProUGUI>();
            label.text = colorIndex.ToString();
            label.fontSize = 24f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = (palette[i].grayscale < 0.45f) ? Color.white : Color.black;
            label.raycastTarget = false;

            paletteButtons.Add(button);
            paletteButtonImages.Add(image);
        }
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
        mainCam.orthographicSize = Mathf.Clamp(next, minOrthoSize, maxOrthoSize);
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
        RefreshHeader();
        RefreshPaletteUI();
    }

    private void HandleMousePaintInput()
    {
        if (mainCam == null) return;
        if (solvedWaitForContinue) return;

        bool isLeftHold = Input.GetMouseButton(0);
        bool isRightHold = Input.GetMouseButton(1);
        if (!isLeftHold && !isRightHold)
        {
            // Important: reset stroke anchors on release so the next click doesn't connect from old points.
            lastLeftPaintedX = -1;
            lastLeftPaintedY = -1;
            lastRightPaintedX = -1;
            lastRightPaintedY = -1;
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

        if (IsSolved())
            OnSolved();
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
        RefreshCellVisual(x, y);
    }

    private void RefreshHeader(string suffix = null)
    {
        if (headerText == null) return;

        string text =
            $"{activePuzzleTitle} ({activePuzzleIndex + 1}/{puzzles.Count})  |  " +
            $"Color {selectedColor}  |  LMB Paint / RMB Erase / Enter Submit / Esc Give up";

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
            v.fill.color = new Color(0.10f, 0.10f, 0.10f, 0.35f);
            if (v.label != null) v.label.text = "";
            if (v.label != null) v.label.enabled = false;
            if (v.edge != null) v.edge.enabled = true;
            return;
        }

        int paint = painted[x, y];
        if (paint <= 0)
        {
            v.fill.color = Color.white;
            if (v.label != null) v.label.enabled = true;
            if (v.edge != null) v.edge.enabled = true;
            return;
        }

        int idx = Mathf.Clamp(paint - 1, 0, palette.Length - 1);
        v.fill.color = palette[idx];
        if (v.label != null) v.label.enabled = false;
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
        HideBoardOutlinesAndNumbers();
        RefreshHeader("Completed! Click to continue.");
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

        if (uiCanvas != null)
            Destroy(uiCanvas.gameObject);

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

