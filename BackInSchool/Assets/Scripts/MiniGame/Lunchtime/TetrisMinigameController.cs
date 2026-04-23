using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Playables;
using UnityEngine.Animations;

/// <summary>
/// Simple Tetris: move/rotate + gravity, no line clears.
/// Goal: lock N pieces successfully.
/// Controls: WASD or Arrow keys.
/// </summary>
public class TetrisMinigameController : MonoBehaviour
{
    [Header("Config (Optional)")]
    public TetrisMinigameConfig config;
    public bool overrideCoreValues;
    public bool overrideBoardValues;
    public bool overrideVisualValues;
    [Tooltip("If false, keep scene Board layout (width/height/cell/origin). If true, apply layout from config.")]
    public bool applyBoardLayoutFromConfig;

    [Header("Goal")]
    public int targetLockedPieces = 15;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip loopClip;
    [Range(0f, 1f)] public float loopVolume = 0.35f;
    public AudioClip moveSfx;
    [Range(0f, 1f)] public float moveSfxVolume = 0.6f;
    public AudioClip rotateSfx;
    [Range(0f, 1f)] public float rotateSfxVolume = 0.7f;
    [Tooltip("Legacy single lock sound fallback. Used only when Lock Sfx Clips is empty.")]
    public AudioClip lockSfx;
    public AudioClip[] lockSfxClips;
    [Range(0f, 1f)] public float lockSfxVolume = 0.85f;
    public AudioClip successSfx;
    [Range(0f, 1f)] public float successSfxVolume = 0.9f;
    public AudioClip failSfx;
    [Range(0f, 1f)] public float failSfxVolume = 0.9f;

    [Header("Difficulty")]
    public float fallInterval = 0.75f;
    public float softDropInterval = 0.06f;

    [Header("Board")]
    public TetrisBoard board;
    [Tooltip("Optional spawn anchor in scene. If assigned, piece spawn starts near this point.")]
    public Transform spawnPoint;

    [Header("Side HUD")]
    public Transform nextPreviewAnchor;
    public TMP_Text nextPreviewLabel;
    public TMP_Text remainingCountLabel;
    public Transform nextBlockImage;
    public bool useNextBlockImageRenderer = true;
    public TMP_Text leftBocks;
    public TMP_Text maxBLock;
    public Vector3 nextPreviewScale = new Vector3(1.7f, 1.7f, 1f);
    public Vector3 nextPreviewLocalOffset = Vector3.zero;
    public int nextPreviewSortingOrder = 6;
    public string nextPreviewTitleKo = "다음";
    public string nextPreviewTitleEn = "Next";
    public string remainingCountFormatKo = "남은 {0}개";
    public string remainingCountFormatEn = "{0} pieces left";

    [Header("Visual")]
    public bool tintBlocksByShape = true;
    [Tooltip("If true, use one prefab object per tetromino (composite sprite mode).")]
    public bool useCompositePieceVisuals = true;
    [Tooltip("Derive collision cells from composite sprite mesh (for non-standard art).")]
    public bool useSpriteDrivenCollision;
    [Tooltip("Apply extra pivot compensation for composite sprite pieces. Keep OFF for stable board alignment.")]
    public bool applyCompositePivotCompensation;
    [Tooltip("If enabled, visual sprite bounds center is auto-aligned to logical shape bounds center. Keep OFF when pivots are authored correctly.")]
    public bool autoAlignCompositeToBounds;
    [Tooltip("Auto-apply logical shape bounds center offset for composite visuals (prevents half-cell drift).")]
    public bool applyCompositeShapeCenterOffset = true;
    public bool debugLogs;

    [Header("Flow")]
    [Tooltip("Penalty to add when failed. (FlowManager penaltyDelta)")]
    public int penaltyOnFail = 1;

    [Header("Fail Overlay")]
    public bool showFailOverlay = true;
    [Min(0f)] public float failOverlaySeconds = 3f;
    public GameObject failOverlayObject;
    public TMP_Text failOverlayTextLabel;
    [Tooltip("Objects that should remain hidden during normal play and appear only during the fail presentation.")]
    public GameObject[] failOverlayShowObjects;
    public GameObject[] failOverlayHideObjects;
    public int failOverlayBackgroundSortingOrder = -1;
    public int failOverlayContentSortingOrder = 2;
    [TextArea] public string failOverlayTextKo = "점심시간이 조금 늦어졌다...";
    [TextArea] public string failOverlayTextEn = "Lunch ran a little late...";
    public Color failOverlayDimColor = new Color(0f, 0f, 0f, 0.72f);
    public Color failOverlayPanelColor = new Color(0.97f, 0.95f, 0.90f, 1f);
    public Color failOverlayTextColor = new Color(0.12f, 0.12f, 0.14f, 1f);
    public TMP_FontAsset failOverlayFont;
    [Header("Fail Character")]
    public GameObject failCharacterObject;
    public Animator failCharacterAnimator;
    public AnimationClip failCharacterIdleClip;
    public AnimationClip failCharacterFailClip;
    [Min(0f)] public float failCharacterDelayBeforeOverlay = 1f;

    [Header("Jelly Feel")]
    public bool enableJelly = true;
    [Range(0f, 0.35f)] public float fallStretchAmount = 0.08f;
    [Range(0f, 0.45f)] public float landSquashAmount = 0.16f;
    [Range(0f, 0.2f)] public float rotateJellyAmount = 0.06f;
    [Range(0.01f, 0.2f)] public float landLockDelay = 0.06f;
    [Range(1f, 40f)] public float jellySnapSpeed = 22f;
    [Range(1f, 40f)] public float jellyReturnSpeed = 14f;

    private int lockedCount = 0;
    private float fallTimer = 0f;

    private TetrisPiece active;
    private int activeShapeIndex = -1;
    private int activeRotationQuarterTurns = 0;
    private GameObject activePiecePrefab;
    private Vector3 activeCompositeOffsetLocal = Vector3.zero;
    private float activeCompositeBaseRotationZ = 0f;
    private readonly List<Transform> activeBlocks = new();
    private Transform activeVisualRoot;
    private Transform nextPreviewVisualRoot;
    private SpriteRenderer nextBlockImageRenderer;
    private int lastLockSfxIndex = -1;

    private System.Random rng = new System.Random();

    private bool ended = false;
    private bool boardAutoCreated = false;
    private Coroutine failSequenceRoutine;
    private Coroutine failCharacterRoutine;
    private PlayableGraph failCharacterGraph;
    private AnimationPlayableOutput failCharacterOutput;
    private AnimationClipPlayable failCharacterClipPlayable;
    private AnimationClip currentFailCharacterClip;

    // 7-bag generator
    private readonly List<int> bag = new();
    private Vector2 jellyScale = Vector2.one;
    private Vector2 jellyTargetScale = Vector2.one;
    private bool lockPending = false;
    private float lockPendingTimer = 0f;

    private static readonly Vector2Int[][] SHAPES = new Vector2Int[][]
    {
        // I
        new []{ new Vector2Int(-1,0), new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(2,0) },
        // O
        new []{ new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(0,1), new Vector2Int(1,1) },
        // T
        new []{ new Vector2Int(-1,0), new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(0,1) },
        // S
        new []{ new Vector2Int(-1,0), new Vector2Int(0,0), new Vector2Int(0,1), new Vector2Int(1,1) },
        // Z
        new []{ new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(-1,1), new Vector2Int(0,1) },
        // J
        new []{ new Vector2Int(-1,0), new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(-1,1) },
        // L
        new []{ new Vector2Int(-1,0), new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(1,1) },
    };

    private static readonly Color[] COLORS = new Color[]
    {
        new Color(0.1f, 0.8f, 0.9f),
        new Color(0.95f, 0.85f, 0.1f),
        new Color(0.7f, 0.3f, 0.9f),
        new Color(0.2f, 0.9f, 0.3f),
        new Color(0.9f, 0.2f, 0.2f),
        new Color(0.2f, 0.4f, 0.9f),
        new Color(0.95f, 0.55f, 0.15f),
    };

    private void Awake()
    {
        ApplyConfigIfNeeded();

        string flowId = PlayerPrefs.GetString("FLOW_ID", "");
        if (string.IsNullOrEmpty(flowId) || !flowId.StartsWith("LUNCH_"))
        {
            enabled = false;
            return;
        }

        if (board == null)
            board = GetComponentInChildren<TetrisBoard>(true);

        if (board == null)
        {
            var boardTr = transform.Find("Board");
            if (boardTr != null)
                board = boardTr.GetComponent<TetrisBoard>() ?? boardTr.gameObject.AddComponent<TetrisBoard>();
        }

        if (board == null)
        {
            var bgo = new GameObject("TetrisBoard");
            bgo.transform.SetParent(transform);
            board = bgo.AddComponent<TetrisBoard>();
            boardAutoCreated = true;
            ApplyBoardConfigIfNeeded();
        }
        else
        {
            boardAutoCreated = false;
            ApplyBoardConfigIfNeeded();
        }

        AutoBindFailOverlayReferences();
        if (failOverlayObject != null && failOverlayObject.activeSelf)
            failOverlayObject.SetActive(false);
        AutoBindFailOverlayShowObjects();
        SetFailOverlayShowObjectsActive(false);
        AutoBindFailOverlayHideObjects();
        AutoBindFailCharacterObject();
        AutoBindFailCharacterAnimator();
        AutoBindFailCharacterClips();
        AutoBindSideHudReferences();
        EnsureAudioSource();
    }

    private void Start()
    {
        if (failOverlayObject != null)
            failOverlayObject.SetActive(false);

        board.Init();
        StartLoopIfNeeded();
        PlayFailCharacterLoop(failCharacterIdleClip);
        SpawnNewPiece();
        RefreshHud();
    }

    private void OnDestroy()
    {
        if (failCharacterRoutine != null)
            StopCoroutine(failCharacterRoutine);

        if (failCharacterGraph.IsValid())
            failCharacterGraph.Destroy();

        ClearNextPreviewVisual();
    }

    private void OnDisable()
    {
        if (failCharacterRoutine != null)
        {
            StopCoroutine(failCharacterRoutine);
            failCharacterRoutine = null;
        }

        if (failOverlayObject != null)
            failOverlayObject.SetActive(false);

        SetFailOverlayShowObjectsActive(false);
        SetFailOverlayHideObjectsActive(true);
        SetHudVisible(false);
        ClearNextPreviewVisual();
    }

    public void HideInactiveArtifacts()
    {
        AutoBindFailOverlayReferences();
        AutoBindFailOverlayShowObjects();
        AutoBindFailOverlayHideObjects();
        AutoBindSideHudReferences();

        if (failOverlayObject != null)
            failOverlayObject.SetActive(false);

        SetFailOverlayShowObjectsActive(false);
        SetFailOverlayHideObjectsActive(true);
        SetHudVisible(false);
        ClearNextPreviewVisual();
    }

    private void Update()
    {
        if (ended) return;

        TickJelly();
        HandleInput();

        if (lockPending)
        {
            if (TickLockPending()) return;
            UpdateActiveVisuals();
            return;
        }

        float interval = IsSoftDropping() ? softDropInterval : fallInterval;
        fallTimer += Time.deltaTime;
        if (fallTimer >= interval)
        {
            fallTimer = 0f;
            StepDown();
        }

        UpdateActiveVisuals();
    }

    private void HandleInput()
    {
        if (KeyDownLeft()) TryMove(new Vector2Int(-1, 0));
        if (KeyDownRight()) TryMove(new Vector2Int(1, 0));

        if (KeyDownRotate()) TryRotateCW();

        // Optional: manual down step on key down.
        if (KeyDownDown()) StepDown();
    }

    private void StepDown()
    {
        if (active == null) return;

        // During lock delay, pressing down should not force immediate lock.
        // Keep the piece movable sideways/rotatable until timer expires.
        if (lockPending)
            return;

        if (TryMove(new Vector2Int(0, -1)))
        {
            TriggerFallStretch();
            return;
        }

        if (enableJelly)
        {
            TriggerLandSquash();
            lockPending = true;
            lockPendingTimer = landLockDelay;
            return;
        }

        LockCurrentPieceAndContinue();
    }

    private void SpawnNewPiece()
    {
        int shapeIdx = NextFromBag();
        activeShapeIndex = shapeIdx;
        var color = COLORS[shapeIdx];
        activePiecePrefab = ResolveVisualPrefabForCurrentDay(shapeIdx);
        activeCompositeOffsetLocal = ResolveShapeVisualOffsetForCurrentDay(shapeIdx);
        activeCompositeBaseRotationZ = ResolveShapeBaseRotationForCurrentDay(shapeIdx);
        var cells = ResolveCollisionCells(shapeIdx, activePiecePrefab, activeCompositeBaseRotationZ);

        // Find a valid spawn position (important when board mask shape is irregular).
        if (!TryFindSpawnPosition(cells, out var spawnPos))
        {
            End(false);
            return;
        }
        if (debugLogs)
            Debug.Log($"[TetrisMinigame] spawn shape={shapeIdx} at {spawnPos} origin={board.origin} size={board.width}x{board.height}", this);

        active = new TetrisPiece(cells, spawnPos, color);
        activeRotationQuarterTurns = 0;

        // Game over if spawn is blocked
        if (board.IsSpawnBlocked(active.cells, active.position))
        {
            End(false);
            return;
        }

        BuildActiveVisuals();
        lockPending = false;
        lockPendingTimer = 0f;
        jellyScale = Vector2.one;
        jellyTargetScale = Vector2.one;
        UpdateActiveVisuals();
        RefreshHud();
    }

    private int NextFromBag()
    {
        EnsureBagFilled();
        int idx = bag[bag.Count - 1];
        bag.RemoveAt(bag.Count - 1);
        return idx;
    }

    private int PeekNextFromBag()
    {
        EnsureBagFilled();
        return bag[bag.Count - 1];
    }

    private void EnsureBagFilled()
    {
        if (bag.Count > 0)
            return;

        for (int i = 0; i < 7; i++)
            bag.Add(i);

        for (int i = bag.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (bag[i], bag[j]) = (bag[j], bag[i]);
        }
    }

    private bool TryFindSpawnPosition(Vector2Int[] cells, out Vector2Int spawnPos)
    {
        int centerX;
        int spawnY;
        if (TryGetSpawnAnchorCell(out var anchor))
        {
            centerX = anchor.x;
            // Never allow low spawn rows from misplaced anchors.
            spawnY = Mathf.Max(anchor.y, Mathf.Max(0, board.height - 2));
        }
        else
        {
            centerX = Mathf.Max(0, board.width / 2 - 1);
            spawnY = Mathf.Max(0, board.height - 2);
        }

        // Build a center-out x order so natural spawn preference stays centered.
        var xOrder = new List<int>(board.width);
        xOrder.Add(centerX);
        for (int d = 1; d < board.width; d++)
        {
            int right = centerX + d;
            int left = centerX - d;
            if (right >= 0 && right < board.width) xOrder.Add(right);
            if (left >= 0 && left < board.width) xOrder.Add(left);
        }

        // Spawn is valid only on one top row. If blocked, it's game over.
        for (int xi = 0; xi < xOrder.Count; xi++)
        {
            var p = new Vector2Int(xOrder[xi], spawnY);
            if (board.CanPlace(cells, p) && !board.IsSpawnBlocked(cells, p))
            {
                spawnPos = p;
                return true;
            }
        }

        spawnPos = new Vector2Int(centerX, spawnY);
        return false;
    }

    private bool TryGetSpawnAnchorCell(out Vector2Int cell)
    {
        if (spawnPoint == null || board == null)
        {
            cell = default;
            return false;
        }

        Vector3 local = board.transform.InverseTransformPoint(spawnPoint.position);
        int x = Mathf.RoundToInt(((local.x - board.origin.x) / board.cellSize) - 0.5f);
        int y = Mathf.RoundToInt(((local.y - board.origin.y) / board.cellSize) - 0.5f);
        x = Mathf.Clamp(x, 0, board.width - 1);
        y = Mathf.Clamp(y, 0, board.height - 1);
        cell = new Vector2Int(x, y);
        return true;
    }

    private bool TryMove(Vector2Int delta)
    {
        if (active == null) return false;

        var nextPos = active.position + delta;
        if (board.CanPlace(active.cells, nextPos))
        {
            active.position = nextPos;
            if (lockPending)
            {
                lockPending = false;
                lockPendingTimer = 0f;
            }
            if (delta.x != 0)
                PlayOneShot(moveSfx, moveSfxVolume);
            return true;
        }
        return false;
    }

    private void TryRotateCW()
    {
        if (active == null) return;
        // O piece does not meaningfully rotate in grid logic.
        if (activeShapeIndex == 1)
            return;

        var rotated = active.RotatedCW();

        // Expanded kick set to reduce "looks like it should fit but won't rotate" cases.
        Vector2Int[] kicks = new Vector2Int[]
        {
            new Vector2Int(0,0),
            new Vector2Int(1,0),
            new Vector2Int(-1,0),
            new Vector2Int(2,0),
            new Vector2Int(-2,0),
            new Vector2Int(0,1),
            new Vector2Int(0,-1),
            new Vector2Int(1,-1),
            new Vector2Int(-1,-1),
            new Vector2Int(1,1),
            new Vector2Int(-1,1),
        };

        for (int i = 0; i < kicks.Length; i++)
        {
            var pos = active.position + kicks[i];
            if (board.CanPlace(rotated, pos))
            {
                active.cells = rotated;
                active.position = pos;
                activeRotationQuarterTurns = (activeRotationQuarterTurns + 1) % 4;
                if (lockPending)
                {
                    lockPending = false;
                    lockPendingTimer = 0f;
                }
                TriggerRotateJelly();
                PlayOneShot(rotateSfx, rotateSfxVolume);
                return;
            }
        }
    }

    private bool LocksAboveTop(Vector2Int[] cells, Vector2Int pos)
    {
        for (int i = 0; i < cells.Length; i++)
        {
            var c = cells[i] + pos;
            if (c.y >= board.height) return true;
        }
        return false;
    }

    private void BuildActiveVisuals()
    {
        ClearActiveVisuals();

        var root = new GameObject("ActivePieceVisual");
        root.transform.SetParent(board != null ? board.transform : transform, false);
        activeVisualRoot = root.transform;

        GameObject piecePrefab;
        if (useCompositePieceVisuals)
        {
            piecePrefab = activePiecePrefab != null
                ? activePiecePrefab
                : ResolveBlockPrefabForCurrentDay(activeShapeIndex);
        }
        else
        {
            piecePrefab = board != null ? board.blockPrefab : null;
        }
        bool useComposite = useCompositePieceVisuals && piecePrefab != null;

        if (useComposite)
        {
            var go = Instantiate(piecePrefab, activeVisualRoot);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.AddComponent<SpriteRenderer>();
            if (tintBlocksByShape)
                sr.color = active.color;
            sr.sortingOrder = 20;
            Vector3 finalOffset = activeCompositeOffsetLocal;
            if (applyCompositeShapeCenterOffset)
            {
                // Convert logical shape-center offset into sprite-local space.
                // This neutralizes baseRotation so offsets stay correct for all pieces.
                Vector3 logicalCenter = GetShapeBoundsCenterLocal(active.cells);
                Quaternion invBase = Quaternion.Euler(0f, 0f, -activeCompositeBaseRotationZ);
                finalOffset += invBase * logicalCenter;
            }
            go.transform.localPosition = finalOffset;
            if (applyCompositePivotCompensation)
            {
                Vector3 pivotCompensation = ComputeCompositePivotCompensation(
                    go.transform,
                    active.cells,
                    activeCompositeBaseRotationZ);
                go.transform.localPosition += pivotCompensation;
            }

            if (autoAlignCompositeToBounds)
            {
                // Optional extra correction for badly authored textures (non-uniform transparent margins).
                Vector3 desiredCenterLocal = GetShapeBoundsCenterLocal(active.cells);
                Vector3 currentVisualCenterLocal = GetVisualCenterLocal(go.transform, activeVisualRoot);
                Vector3 autoOffset = desiredCenterLocal - currentVisualCenterLocal;
                go.transform.localPosition += autoOffset;
            }

            // Keep final local offset for lock visuals.
            activeCompositeOffsetLocal = go.transform.localPosition;

            activeBlocks.Add(go.transform);
            return;
        }

        for (int i = 0; i < active.cells.Length; i++)
        {
            GameObject go;
            if (piecePrefab != null)
            {
                go = Instantiate(piecePrefab, activeVisualRoot);
            }
            else
            {
                go = new GameObject("ActiveBlock");
                go.transform.SetParent(activeVisualRoot);
                var sr = go.AddComponent<SpriteRenderer>();
                // board will create fallback sprite internally only for locked blocks,
                // so for active blocks we create our own 1x1 sprite.
                sr.sprite = CreateFallbackSprite();
                sr.sortingOrder = 20;
                go.transform.localScale = Vector3.one * board.cellSize;
            }

            var sr2 = go.GetComponent<SpriteRenderer>();
            if (sr2 == null) sr2 = go.AddComponent<SpriteRenderer>();
            if (tintBlocksByShape)
                sr2.color = active.color;
            sr2.sortingOrder = 20;

            activeBlocks.Add(go.transform);
        }
    }

    private void UpdateActiveVisuals()
    {
        if (active == null) return;
        if (activeVisualRoot != null)
        {
            activeVisualRoot.localPosition = board.CellToLocal(active.position);
            activeVisualRoot.localScale = new Vector3(jellyScale.x, jellyScale.y, 1f);
        }

        bool useComposite = useCompositePieceVisuals && activeBlocks.Count == 1;
        if (useComposite)
        {
            if (activeVisualRoot != null)
                activeVisualRoot.localRotation = Quaternion.Euler(
                    0f, 0f, activeCompositeBaseRotationZ - (90f * activeRotationQuarterTurns));
            if (activeBlocks[0] != null)
                activeBlocks[0].localPosition = activeCompositeOffsetLocal;
            return;
        }
        else if (activeVisualRoot != null)
        {
            activeVisualRoot.localRotation = Quaternion.identity;
        }

        for (int i = 0; i < activeBlocks.Count; i++)
        {
            if (activeBlocks[i] == null) continue;
            var cell = active.cells[i];
            activeBlocks[i].localPosition = new Vector3(
                cell.x * board.cellSize,
                cell.y * board.cellSize,
                0f);
        }
    }

    private Vector3 GetShapeBoundsCenterLocal(Vector2Int[] cells)
    {
        if (cells == null || cells.Length == 0 || board == null)
            return Vector3.zero;

        int minX = cells[0].x;
        int maxX = cells[0].x;
        int minY = cells[0].y;
        int maxY = cells[0].y;
        for (int i = 1; i < cells.Length; i++)
        {
            if (cells[i].x < minX) minX = cells[i].x;
            if (cells[i].x > maxX) maxX = cells[i].x;
            if (cells[i].y < minY) minY = cells[i].y;
            if (cells[i].y > maxY) maxY = cells[i].y;
        }

        float cx = (minX + maxX) * 0.5f * board.cellSize;
        float cy = (minY + maxY) * 0.5f * board.cellSize;
        return new Vector3(cx, cy, 0f);
    }

    private Vector3 ComputeCompositePivotCompensation(Transform visual, Vector2Int[] cells, float baseRotationDeg)
    {
        if (visual == null || board == null || cells == null || cells.Length == 0)
            return Vector3.zero;

        var sr = visual.GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null)
            return Vector3.zero;

        var sprite = sr.sprite;
        Vector2 rectSizePx = sprite.rect.size;
        if (rectSizePx.x <= 0f || rectSizePx.y <= 0f)
            return Vector3.zero;

        float ppu = sprite.pixelsPerUnit <= 0f ? 100f : sprite.pixelsPerUnit;
        float widthUnits = (rectSizePx.x / ppu) * Mathf.Abs(visual.localScale.x);
        float heightUnits = (rectSizePx.y / ppu) * Mathf.Abs(visual.localScale.y);

        Vector2 pivotPx = sprite.pivot;
        Vector2 pivotNorm = new Vector2(pivotPx.x / rectSizePx.x, pivotPx.y / rectSizePx.y);
        Vector3 centerFromPivot = new Vector3(
            (0.5f - pivotNorm.x) * widthUnits,
            (0.5f - pivotNorm.y) * heightUnits,
            0f);

        Vector3 desiredCenterLocal = GetShapeBoundsCenterLocal(cells);
        Quaternion baseRot = Quaternion.Euler(0f, 0f, baseRotationDeg);
        return desiredCenterLocal - (baseRot * centerFromPivot);
    }

    private Vector3 GetVisualCenterLocal(Transform visualRoot, Transform referenceRoot)
    {
        if (visualRoot == null || referenceRoot == null)
            return Vector3.zero;

        var renderers = visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers == null || renderers.Length == 0)
            return visualRoot.localPosition;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);

        return referenceRoot.InverseTransformPoint(b.center);
    }

    private void ClearActiveVisuals()
    {
        if (activeVisualRoot != null)
        {
            Destroy(activeVisualRoot.gameObject);
            activeVisualRoot = null;
        }

        activeBlocks.Clear();
    }

    private void LockCurrentPieceAndContinue()
    {
        if (active == null) return;

        if (!board.CanPlace(active.cells, active.position))
        {
            End(false);
            return;
        }

        bool overflow = LocksAboveTop(active.cells, active.position);
        GameObject lockedPrefab = null;
        if (useCompositePieceVisuals)
        {
            lockedPrefab = activePiecePrefab != null
                ? activePiecePrefab
                : ResolveBlockPrefabForCurrentDay(activeShapeIndex);
        }
        else if (board != null)
        {
            lockedPrefab = board.blockPrefab;
        }
        board.LockPiece(
            active.cells,
            active.position,
            active.color,
            lockedPrefab,
            tintBlocksByShape,
            useCompositePieceVisuals,
            activeCompositeBaseRotationZ - (90f * activeRotationQuarterTurns),
            activeCompositeOffsetLocal);
        PlayLockSfx();
        ClearActiveVisuals();
        lockedCount++;
        lockPending = false;
        lockPendingTimer = 0f;
        RefreshHud();

        if (overflow)
        {
            End(false);
            return;
        }

        if (lockedCount >= targetLockedPieces)
        {
            End(true);
            return;
        }

        SpawnNewPiece();
    }

    private bool TickLockPending()
    {
        if (!lockPending) return false;

        lockPendingTimer -= Time.deltaTime;
        if (lockPendingTimer <= 0f)
        {
            LockCurrentPieceAndContinue();
            return true;
        }

        return false;
    }

    private void TickJelly()
    {
        if (!enableJelly) return;

        float dt = Time.deltaTime;
        float snapT = 1f - Mathf.Exp(-jellySnapSpeed * dt);
        float returnT = 1f - Mathf.Exp(-jellyReturnSpeed * dt);

        jellyScale = Vector2.Lerp(jellyScale, jellyTargetScale, snapT);
        jellyTargetScale = Vector2.Lerp(jellyTargetScale, Vector2.one, returnT);
    }

    private void TriggerFallStretch()
    {
        if (!enableJelly) return;
        jellyTargetScale = new Vector2(1f - fallStretchAmount, 1f + fallStretchAmount);
    }

    private void TriggerLandSquash()
    {
        if (!enableJelly) return;
        jellyTargetScale = new Vector2(1f + landSquashAmount, 1f - landSquashAmount);
    }

    private void TriggerRotateJelly()
    {
        if (!enableJelly) return;
        if (rotateJellyAmount <= 0f) return;
        jellyTargetScale = new Vector2(1f + rotateJellyAmount, 1f - (rotateJellyAmount * 0.7f));
    }

    private Sprite CreateFallbackSprite()
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    private void RefreshHud()
    {
        RefreshProgressLabels();
        RefreshNextPreviewLabel();
        RefreshNextPreviewVisual();
    }

    private void RefreshProgressLabels()
    {
        if (leftBocks != null)
            leftBocks.text = lockedCount.ToString();

        if (maxBLock != null)
            maxBLock.text = targetLockedPieces.ToString();

        if (remainingCountLabel != null)
        {
            int remaining = Mathf.Max(0, targetLockedPieces - lockedCount);
            remainingCountLabel.text = string.Format(remainingCountFormatKo, remaining);
        }
    }

    private void RefreshNextPreviewLabel()
    {
        if (nextPreviewLabel == null)
            return;

        nextPreviewLabel.text = nextPreviewTitleKo;
    }

    private void RefreshNextPreviewVisual()
    {
        ClearNextPreviewVisual();

        int nextShapeIdx = PeekNextFromBag();
        float baseRotation = ResolveShapeBaseRotationForCurrentDay(nextShapeIdx);
        GameObject piecePrefab = ResolveVisualPrefabForCurrentDay(nextShapeIdx);

        Transform previewParent = nextPreviewAnchor != null ? nextPreviewAnchor : nextBlockImage;
        if (previewParent == null)
            return;

        Vector2Int[] previewCells = ResolveCollisionCells(nextShapeIdx, piecePrefab, baseRotation);

        GameObject root = new GameObject("NextPreviewVisual");
        root.transform.SetParent(previewParent, false);
        root.transform.localPosition = nextPreviewLocalOffset;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = nextPreviewScale;
        nextPreviewVisualRoot = root.transform;

        bool useComposite = useCompositePieceVisuals && piecePrefab != null;
        if (useComposite)
            BuildCompositePreviewVisual(nextPreviewVisualRoot, piecePrefab, previewCells, nextShapeIdx, baseRotation);
        else
            BuildBlockPreviewVisual(nextPreviewVisualRoot, previewCells, nextShapeIdx);

        FitNextPreviewIntoSlot();
    }

    private void BuildCompositePreviewVisual(Transform root, GameObject piecePrefab, Vector2Int[] previewCells, int shapeIdx, float baseRotation)
    {
        if (root == null || piecePrefab == null)
            return;

        GameObject go = Instantiate(piecePrefab, root);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr == null)
            sr = go.AddComponent<SpriteRenderer>();

        if (tintBlocksByShape)
            sr.color = COLORS[Mathf.Clamp(shapeIdx, 0, COLORS.Length - 1)];

        Vector3 finalOffset = ResolveShapeVisualOffsetForCurrentDay(shapeIdx);
        if (applyCompositeShapeCenterOffset)
        {
            Vector3 logicalCenter = GetShapeBoundsCenterLocal(previewCells);
            Quaternion invBase = Quaternion.Euler(0f, 0f, -baseRotation);
            finalOffset += invBase * logicalCenter;
        }

        go.transform.localPosition = finalOffset;

        if (applyCompositePivotCompensation)
        {
            Vector3 pivotCompensation = ComputeCompositePivotCompensation(
                go.transform,
                previewCells,
                baseRotation);
            go.transform.localPosition += pivotCompensation;
        }

        if (autoAlignCompositeToBounds)
        {
            Vector3 desiredCenterLocal = GetShapeBoundsCenterLocal(previewCells);
            Vector3 currentVisualCenterLocal = GetVisualCenterLocal(go.transform, root);
            go.transform.localPosition += desiredCenterLocal - currentVisualCenterLocal;
        }

        root.localRotation = Quaternion.Euler(0f, 0f, baseRotation);
        Vector3 currentCenter = GetVisualCenterLocal(go.transform, root);
        go.transform.localPosition -= currentCenter;

        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].sortingOrder = nextPreviewSortingOrder;
            if (tintBlocksByShape)
                renderers[i].color = COLORS[Mathf.Clamp(shapeIdx, 0, COLORS.Length - 1)];
        }
    }

    private void BuildBlockPreviewVisual(Transform root, Vector2Int[] previewCells, int shapeIdx)
    {
        if (root == null || previewCells == null || previewCells.Length == 0)
            return;

        Vector3 center = GetShapeBoundsCenterLocal(previewCells);
        GameObject piecePrefab = ResolveBlockPrefabForCurrentDay(shapeIdx);

        for (int i = 0; i < previewCells.Length; i++)
        {
            GameObject go;
            if (piecePrefab != null)
            {
                go = Instantiate(piecePrefab, root);
            }
            else
            {
                go = new GameObject($"PreviewBlock_{i}");
                go.transform.SetParent(root, false);
                SpriteRenderer fallbackRenderer = go.AddComponent<SpriteRenderer>();
                fallbackRenderer.sprite = CreateFallbackSprite();
            }

            go.transform.localRotation = Quaternion.identity;
            go.transform.localPosition = new Vector3(
                previewCells[i].x * board.cellSize,
                previewCells[i].y * board.cellSize,
                0f) - center;

            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            if (sr == null)
                sr = go.AddComponent<SpriteRenderer>();

            if (tintBlocksByShape)
                sr.color = COLORS[Mathf.Clamp(shapeIdx, 0, COLORS.Length - 1)];

            sr.sortingOrder = nextPreviewSortingOrder;
        }
    }

    private void ClearNextPreviewVisual()
    {
        if (nextPreviewVisualRoot == null)
            return;

        Destroy(nextPreviewVisualRoot.gameObject);
        nextPreviewVisualRoot = null;
    }

    private void FitNextPreviewIntoSlot()
    {
        if (nextPreviewVisualRoot == null || nextBlockImage == null)
            return;

        if (nextBlockImageRenderer == null)
            nextBlockImageRenderer = nextBlockImage.GetComponent<SpriteRenderer>();

        if (nextBlockImageRenderer == null)
            return;

        var previewRenderers = nextPreviewVisualRoot.GetComponentsInChildren<SpriteRenderer>(true);
        if (previewRenderers == null || previewRenderers.Length == 0)
            return;

        Bounds previewBounds = previewRenderers[0].bounds;
        for (int i = 1; i < previewRenderers.Length; i++)
            previewBounds.Encapsulate(previewRenderers[i].bounds);

        Bounds slotBounds = nextBlockImageRenderer.bounds;
        float previewWidth = Mathf.Max(0.0001f, previewBounds.size.x);
        float previewHeight = Mathf.Max(0.0001f, previewBounds.size.y);
        float slotWidth = Mathf.Max(0.0001f, slotBounds.size.x * 0.72f);
        float slotHeight = Mathf.Max(0.0001f, slotBounds.size.y * 0.72f);
        float scaleFactor = Mathf.Min(slotWidth / previewWidth, slotHeight / previewHeight);

        nextPreviewVisualRoot.localScale *= scaleFactor;
        nextPreviewVisualRoot.position += slotBounds.center - previewBounds.center;

        previewRenderers = nextPreviewVisualRoot.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < previewRenderers.Length; i++)
            previewRenderers[i].sortingOrder = Mathf.Max(previewRenderers[i].sortingOrder, nextBlockImageRenderer.sortingOrder + 1);
    }

    private void End(bool success)
    {
        if (ended)
            return;

        if (!success && showFailOverlay)
        {
            ended = true;
            StopLoopIfNeeded();
            PlayOneShot(failSfx, failSfxVolume);
            if (failSequenceRoutine != null)
                StopCoroutine(failSequenceRoutine);
            failSequenceRoutine = StartCoroutine(CoFailThenAdvance());
            return;
        }

        ended = true;
        StopLoopIfNeeded();
        PlayOneShot(success ? successSfx : failSfx, success ? successSfxVolume : failSfxVolume);
        Debug.Log($"[TetrisMinigame] End: {(success ? "SUCCESS" : "FAIL")} (locked={lockedCount}/{targetLockedPieces})");

        // Prefer FlowManager timeline
        if (FlowManager.Instance != null)
        {
            FlowManager.Instance.SetLunchFreeTimeStartMinuteForCurrentDay(success ? 30 : 40);
            int delta = success ? 0 : penaltyOnFail;
            FlowManager.Instance.CompleteCurrentEvent(delta);
            return;
        }

        // Fallback to GameManager
        var gm = FindAnyObjectByType<GameManager>();
        if (gm != null)
        {
            gm.MinigameFinished(success);
        }
    }

    private void EnsureAudioSource()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
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

    private void PlayOneShot(AudioClip clip, float volume)
    {
        EnsureAudioSource();
        if (clip == null)
            return;

        audioSource.PlayOneShot(clip, AudioSettingsService.ScaleSfx(volume));
    }

    private void PlayLockSfx()
    {
        AudioClip clip = PickRandomClip(lockSfxClips, ref lastLockSfxIndex);
        if (clip == null)
            clip = lockSfx;

        PlayOneShot(clip, lockSfxVolume);
    }

    private AudioClip PickRandomClip(AudioClip[] clips, ref int lastIndex)
    {
        if (clips == null || clips.Length == 0)
            return null;

        if (clips.Length == 1)
        {
            lastIndex = 0;
            return clips[0];
        }

        int picked = lastIndex;
        for (int safety = 0; safety < 8 && picked == lastIndex; safety++)
            picked = rng.Next(0, clips.Length);

        if (picked < 0 || picked >= clips.Length)
            picked = 0;

        lastIndex = picked;
        return clips[picked];
    }

    private System.Collections.IEnumerator CoFailThenAdvance()
    {
        yield return ShowSimpleFailOverlay();
        CompleteAfterOverlay(false);
    }

    private System.Collections.IEnumerator ShowSimpleFailOverlay()
    {
        float seconds = Mathf.Max(0f, failOverlaySeconds);
        if (seconds <= 0.01f)
            yield break;

        PlayFailCharacterOnce(failCharacterFailClip);
        float preDelay = Mathf.Max(failCharacterDelayBeforeOverlay, failCharacterFailClip != null ? failCharacterFailClip.length : 0f);
        if (preDelay > 0.01f)
            yield return new WaitForSecondsRealtime(preDelay);

        SetHudVisible(false);

        if (failOverlayObject != null)
        {
            failOverlayObject.SetActive(true);

            if (failOverlayTextLabel == null)
                failOverlayTextLabel = failOverlayObject.GetComponentInChildren<TMP_Text>(true);

            if (failOverlayTextLabel != null)
                failOverlayTextLabel.text = GetLocalizedFailOverlayText();

            SetFailOverlayHideObjectsActive(false);
            SetFailOverlayShowObjectsActive(true);
            yield return new WaitForSecondsRealtime(seconds);
            SetFailOverlayShowObjectsActive(false);
            SetFailOverlayHideObjectsActive(true);
            failOverlayObject.SetActive(false);
            SetHudVisible(true);
            yield break;
        }

        if (failOverlayFont == null)
            EnsureFailOverlayFont();

        var canvasGo = new GameObject("__LunchFailOverlay", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 5000;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var rootRect = CreateOverlayRect("Dim", canvasGo.transform);
        StretchRect(rootRect);
        var dimImage = rootRect.gameObject.AddComponent<Image>();
        dimImage.color = failOverlayDimColor;
        dimImage.raycastTarget = true;

        var panelRect = CreateOverlayRect("Panel", rootRect);
        panelRect.anchorMin = new Vector2(0.22f, 0.36f);
        panelRect.anchorMax = new Vector2(0.78f, 0.64f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        var panelImage = panelRect.gameObject.AddComponent<Image>();
        panelImage.color = failOverlayPanelColor;

        var messageRect = CreateOverlayRect("Message", panelRect);
        StretchRect(messageRect, 48f, 40f);
        var message = messageRect.gameObject.AddComponent<TextMeshProUGUI>();
        message.font = failOverlayFont != null ? failOverlayFont : TMP_Settings.defaultFontAsset;
        message.fontSize = 44f;
        message.alignment = TextAlignmentOptions.Center;
        message.enableWordWrapping = true;
        message.color = failOverlayTextColor;
        message.text = GetLocalizedFailOverlayText();

        yield return new WaitForSecondsRealtime(seconds);

        if (canvasGo != null)
        Destroy(canvasGo);
        SetHudVisible(true);
    }

    private void CompleteAfterOverlay(bool success)
    {
        Debug.Log($"[TetrisMinigame] End: {(success ? "SUCCESS" : "FAIL")} (locked={lockedCount}/{targetLockedPieces})");

        if (FlowManager.Instance != null)
        {
            FlowManager.Instance.SetLunchFreeTimeStartMinuteForCurrentDay(success ? 30 : 40);
            int delta = success ? 0 : penaltyOnFail;
            FlowManager.Instance.CompleteCurrentEvent(delta);
            return;
        }

        var gm = FindAnyObjectByType<GameManager>();
        if (gm != null)
            gm.MinigameFinished(success);
    }

    private string GetLocalizedFailOverlayText()
    {
        Language language = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.GetCurrentLanguage()
            : Language.Korean;
        return language == Language.English ? failOverlayTextEn : failOverlayTextKo;
    }

    private void AutoBindFailOverlayReferences()
    {
        if (failOverlayObject == null)
        {
            Transform found = transform.Find("Fail Overlay");
            if (found == null)
            {
                var all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null && all[i].name == "Fail Overlay")
                    {
                        found = all[i];
                        break;
                    }
                }
            }

            if (found != null)
                failOverlayObject = found.gameObject;
        }

        if (failOverlayTextLabel == null && failOverlayObject != null)
            failOverlayTextLabel = failOverlayObject.GetComponentInChildren<TMP_Text>(true);

        RefreshFailOverlaySorting();
    }

    private void AutoBindFailCharacterObject()
    {
        if (failCharacterObject != null)
            return;

        Transform found = transform.Find("PlayerCharcter");
        if (found == null)
        {
            var all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                Transform candidate = all[i];
                if (candidate != null && candidate.name == "PlayerCharcter")
                {
                    found = candidate;
                    break;
                }
            }
        }

        if (found == null)
            return;

        failCharacterObject = found.gameObject;
    }

    private void AutoBindFailCharacterAnimator()
    {
        if (failCharacterAnimator != null)
            return;

        AutoBindFailCharacterObject();
        if (failCharacterObject == null)
            return;

        failCharacterAnimator = failCharacterObject.GetComponent<Animator>();
    }

    private void AutoBindFailCharacterClips()
    {
#if UNITY_EDITOR
        if (failCharacterIdleClip == null)
        {
            failCharacterIdleClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/Animator/Player/Player_Seat.anim");
        }

        if (failCharacterFailClip == null)
        {
            failCharacterFailClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/Animator/Player/Player_Vomit.anim");
        }
#endif
    }

    private void AutoBindFailOverlayShowObjects()
    {
        if (failOverlayShowObjects != null && failOverlayShowObjects.Length > 0)
            return;

        if (failOverlayObject == null)
            return;

        List<GameObject> showObjects = new();
        for (int i = 0; i < failOverlayObject.transform.childCount; i++)
        {
            Transform child = failOverlayObject.transform.GetChild(i);
            if (child == null)
                continue;

            string childName = child.name;
            if (childName == "Square" || childName == "Text" || childName == "HealthRoom_0")
                showObjects.Add(child.gameObject);
        }

        if (showObjects.Count > 0)
            failOverlayShowObjects = showObjects.ToArray();
    }

    private void AutoBindSideHudReferences()
    {
        AutoBindNextBlockImage();
        AutoBindNextPreviewAnchor();
        AutoBindNextPreviewLabel();
        AutoBindRemainingCountLabel();
        AutoBindLeftBocksLabel();
        AutoBindMaxBLockLabel();
    }

    private void AutoBindNextBlockImage()
    {
        if (nextBlockImage != null)
            return;

        nextBlockImage = FindNamedChildRecursive(transform, "Next Block Image");
    }

    private void AutoBindNextPreviewAnchor()
    {
        if (nextPreviewAnchor != null)
            return;

        nextPreviewAnchor = FindNamedChildRecursive(transform, "NextPreviewAnchor");
    }

    private void AutoBindNextPreviewLabel()
    {
        if (nextPreviewLabel != null)
            return;

        Transform existing = FindNamedChildRecursive(transform, "NextPreviewLabel");
        if (existing != null)
            nextPreviewLabel = existing.GetComponent<TMP_Text>();
    }

    private void AutoBindRemainingCountLabel()
    {
        if (remainingCountLabel != null)
            return;

        Transform existing = FindNamedChildRecursive(transform, "RemainingCountLabel");
        if (existing != null)
            remainingCountLabel = existing.GetComponent<TMP_Text>();
    }

    private void AutoBindLeftBocksLabel()
    {
        if (leftBocks != null)
            return;

        Transform existing = FindNamedChildRecursive(transform, "LeftBocks");
        if (existing != null)
            leftBocks = existing.GetComponent<TMP_Text>();
    }

    private void AutoBindMaxBLockLabel()
    {
        if (maxBLock != null)
            return;

        Transform existing = FindNamedChildRecursive(transform, "MaxBLock");
        if (existing != null)
            maxBLock = existing.GetComponent<TMP_Text>();
    }

    private Transform FindNamedChildRecursive(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null)
                continue;

            if (child.name == targetName)
                return child;

            Transform nested = FindNamedChildRecursive(child, targetName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private void AutoBindFailOverlayHideObjects()
    {
        if (failOverlayHideObjects != null && failOverlayHideObjects.Length > 0)
            return;

        List<GameObject> hideObjects = new();

        if (board != null)
        {
            Transform boardRoot = board.transform.parent != null ? board.transform.parent : board.transform;
            if (boardRoot != null)
                hideObjects.Add(boardRoot.gameObject);
        }

        Transform sideRoot = transform.Find("Side");
        if (sideRoot != null && !hideObjects.Contains(sideRoot.gameObject))
            hideObjects.Add(sideRoot.gameObject);

        if (hideObjects.Count > 0)
            failOverlayHideObjects = hideObjects.ToArray();
    }

    private void RefreshFailOverlaySorting()
    {
        if (failOverlayObject == null)
            return;

        var renderers = failOverlayObject.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null)
                continue;

            string objectName = sr.gameObject.name;
            bool isDimBlack = string.Equals(objectName, "Black", System.StringComparison.OrdinalIgnoreCase);
            bool isPanelBackground = string.Equals(objectName, "Square", System.StringComparison.OrdinalIgnoreCase);
            bool isHealthRoom = string.Equals(objectName, "HealthRoom_0", System.StringComparison.OrdinalIgnoreCase);

            if (isDimBlack)
            {
                sr.sortingOrder = failOverlayBackgroundSortingOrder - 1;
                continue;
            }

            if (isPanelBackground)
            {
                sr.sortingOrder = failOverlayBackgroundSortingOrder;
                continue;
            }

            if (isHealthRoom)
            {
                sr.sortingOrder = failOverlayContentSortingOrder + 1;
                continue;
            }

            sr.sortingOrder = failOverlayContentSortingOrder;
        }

        if (failOverlayTextLabel != null)
        {
            var meshRenderer = failOverlayTextLabel.GetComponent<Renderer>();
            if (meshRenderer != null)
                meshRenderer.sortingOrder = failOverlayContentSortingOrder + 2;
        }
    }

    private void SetFailOverlayShowObjectsActive(bool active)
    {
        if (failOverlayShowObjects == null)
            return;

        for (int i = 0; i < failOverlayShowObjects.Length; i++)
        {
            GameObject go = failOverlayShowObjects[i];
            if (go != null)
                go.SetActive(active);
        }
    }

    private void SetFailOverlayHideObjectsActive(bool active)
    {
        if (failOverlayHideObjects == null)
            return;

        for (int i = 0; i < failOverlayHideObjects.Length; i++)
        {
            GameObject go = failOverlayHideObjects[i];
            if (go != null)
                go.SetActive(active);
        }
    }

    private void SetHudVisible(bool visible)
    {
        SetHudObjectVisible(nextBlockImage, visible);
        SetHudObjectVisible(nextPreviewAnchor, visible);
        SetHudObjectVisible(nextPreviewLabel != null ? nextPreviewLabel.transform : null, visible);
        SetHudObjectVisible(remainingCountLabel != null ? remainingCountLabel.transform : null, visible);
        SetHudObjectVisible(leftBocks != null ? leftBocks.transform : null, visible);
        SetHudObjectVisible(maxBLock != null ? maxBLock.transform : null, visible);

        if (nextPreviewVisualRoot != null)
            nextPreviewVisualRoot.gameObject.SetActive(visible);
    }

    private void SetHudObjectVisible(Transform target, bool visible)
    {
        if (target == null)
            return;

        target.gameObject.SetActive(visible);
    }


    private void EnsureFailOverlayFont()
    {
        if (failOverlayFont != null)
            return;

#if UNITY_EDITOR
        failOverlayFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/Fonts/Galmuri11-Bold SDF.asset");
#endif
    }

    private void PlayFailCharacterLoop(AnimationClip clip)
    {
        if (clip == null)
            return;

        AutoBindFailCharacterObject();
        AutoBindFailCharacterAnimator();
        if (failCharacterObject == null)
            return;

        if (failCharacterAnimator != null)
        {
            PlayFailCharacterClipWithAnimator(clip);
            return;
        }

        if (failCharacterRoutine != null)
            StopCoroutine(failCharacterRoutine);

        failCharacterRoutine = StartCoroutine(CoSampleClipLoop(clip));
    }

    private void PlayFailCharacterOnce(AnimationClip clip)
    {
        if (clip == null)
            return;

        AutoBindFailCharacterObject();
        AutoBindFailCharacterAnimator();
        if (failCharacterObject == null)
            return;

        if (failCharacterAnimator != null)
        {
            PlayFailCharacterClipWithAnimator(clip, true);
            return;
        }

        if (failCharacterRoutine != null)
            StopCoroutine(failCharacterRoutine);

        failCharacterRoutine = StartCoroutine(CoSampleClipOnce(clip));
    }

    private System.Collections.IEnumerator CoSampleClipLoop(AnimationClip clip)
    {
        if (clip == null || failCharacterObject == null)
            yield break;

        float length = Mathf.Max(0.01f, clip.length);
        while (true)
        {
            float t = 0f;
            while (t < length)
            {
                if (failCharacterObject == null)
                    yield break;

                clip.SampleAnimation(failCharacterObject, t);
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            clip.SampleAnimation(failCharacterObject, length);
        }
    }

    private System.Collections.IEnumerator CoSampleClipOnce(AnimationClip clip)
    {
        if (clip == null || failCharacterObject == null)
            yield break;

        float length = Mathf.Max(0.01f, clip.length);
        float t = 0f;
        while (t < length)
        {
            if (failCharacterObject == null)
                yield break;

            clip.SampleAnimation(failCharacterObject, t);
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        clip.SampleAnimation(failCharacterObject, length);
    }

    private void PlayFailCharacterClipWithAnimator(AnimationClip clip, bool forceRestart = false)
    {
        if (clip == null)
            return;

        if (failCharacterAnimator == null)
            return;

        if (!forceRestart && currentFailCharacterClip == clip && failCharacterGraph.IsValid())
            return;

        if (failCharacterRoutine != null)
        {
            StopCoroutine(failCharacterRoutine);
            failCharacterRoutine = null;
        }

        if (failCharacterGraph.IsValid())
            failCharacterGraph.Destroy();

        failCharacterGraph = PlayableGraph.Create("TetrisFailCharacterGraph");
        failCharacterGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        failCharacterOutput = AnimationPlayableOutput.Create(failCharacterGraph, "Animation", failCharacterAnimator);
        failCharacterClipPlayable = AnimationClipPlayable.Create(failCharacterGraph, clip);
        failCharacterClipPlayable.SetApplyFootIK(false);
        failCharacterClipPlayable.SetApplyPlayableIK(false);
        failCharacterOutput.SetSourcePlayable(failCharacterClipPlayable);
        failCharacterGraph.Play();
        currentFailCharacterClip = clip;
    }

    private static RectTransform CreateOverlayRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        return rect;
    }

    private static void StretchRect(RectTransform rect, float paddingX = 0f, float paddingY = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(paddingX, paddingY);
        rect.offsetMax = new Vector2(-paddingX, -paddingY);
    }

    // --- input helpers (old Input Manager) ---
    private bool KeyDownLeft()
    {
        return Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow);
    }

    private bool KeyDownRight()
    {
        return Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow);
    }

    private bool KeyDownDown()
    {
        return Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow);
    }

    private bool KeyDownRotate()
    {
        return Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
    }

    private bool IsSoftDropping()
    {
        return Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
    }

    private void ApplyConfigIfNeeded()
    {
        if (config == null)
            return;

        if (!overrideCoreValues)
        {
            targetLockedPieces = Mathf.Max(1, config.targetLockedPieces);
            fallInterval = Mathf.Max(0.01f, config.fallInterval);
            softDropInterval = Mathf.Max(0.01f, config.softDropInterval);
            penaltyOnFail = Mathf.Max(0, config.penaltyOnFail);

            enableJelly = config.enableJelly;
            fallStretchAmount = Mathf.Clamp(config.fallStretchAmount, 0f, 0.35f);
            landSquashAmount = Mathf.Clamp(config.landSquashAmount, 0f, 0.45f);
            rotateJellyAmount = Mathf.Clamp(config.rotateJellyAmount, 0f, 0.2f);
            landLockDelay = Mathf.Clamp(config.landLockDelay, 0.01f, 0.2f);
            jellySnapSpeed = Mathf.Clamp(config.jellySnapSpeed, 1f, 40f);
            jellyReturnSpeed = Mathf.Clamp(config.jellyReturnSpeed, 1f, 40f);
        }

        if (!overrideVisualValues)
        {
            tintBlocksByShape = config.tintBlocksByShape;
            useCompositePieceVisuals = config.useCompositePieceVisuals;
            useSpriteDrivenCollision = config.useSpriteDrivenCollision;
            applyCompositePivotCompensation = config.applyCompositePivotCompensation;
            applyCompositeShapeCenterOffset = config.applyCompositeShapeCenterOffset;
        }
    }

    private void ApplyBoardConfigIfNeeded()
    {
        if (config == null || board == null || overrideBoardValues)
            return;

        if (boardAutoCreated || applyBoardLayoutFromConfig)
        {
            board.width = Mathf.Max(4, config.boardWidth);
            board.height = Mathf.Max(8, config.boardHeight);
            board.cellSize = Mathf.Max(0.05f, config.boardCellSize);
            board.origin = config.boardOrigin;
        }
        board.autoFitGridToBoardSprite = config.autoFitGridToBoardSprite;
        board.autoCenterGridToBoardSprite = config.autoCenterGridToBoardSprite;
        if (config.blockPrefab != null)
            board.blockPrefab = config.blockPrefab;
        board.maskTexture = config.useBoardMask ? config.boardMaskTexture : null;
        board.maskMode = config.boardMaskMode;
        board.maskThreshold = Mathf.Clamp01(config.boardMaskThreshold);
        board.invertMask = config.invertBoardMask;
        board.maskFlipX = config.boardMaskFlipX;
        board.maskFlipY = config.boardMaskFlipY;
    }

    private int ResolveCurrentDay()
    {
        if (FlowManager.Instance != null)
            return FlowManager.Instance.day;

        var gm = FindAnyObjectByType<GameManager>();
        if (gm != null)
            return gm.currentDay;

        string id = PlayerPrefs.GetString("FLOW_ID", "");
        for (int d = 1; d <= 4; d++)
        {
            if (id.EndsWith(d.ToString()))
                return d;
        }

        return 1;
    }

    private GameObject ResolveBlockPrefabForCurrentDay(int shapeIdx)
    {
        if (config == null || config.dayBlockSets == null || config.dayBlockSets.Length == 0)
            return board != null ? board.blockPrefab : null;

        int day = Mathf.Clamp(ResolveCurrentDay(), 1, 4);
        TetrisMinigameConfig.DayBlockSet fallbackSet = null;
        for (int i = 0; i < config.dayBlockSets.Length; i++)
        {
            var set = config.dayBlockSets[i];
            if (set == null)
                continue;

            if (fallbackSet == null)
                fallbackSet = set;

            if (set.day != day)
                continue;

            var byShape = set.GetShapePrefab(shapeIdx);
            if (byShape != null)
                return byShape;

            if (set.defaultBlockPrefab != null)
                return set.defaultBlockPrefab;
        }

        // Fallback: if day field is not configured correctly yet, use first set.
        if (fallbackSet != null)
        {
            var byShape = fallbackSet.GetShapePrefab(shapeIdx);
            if (byShape != null)
                return byShape;
            if (fallbackSet.defaultBlockPrefab != null)
                return fallbackSet.defaultBlockPrefab;
        }

        return board != null ? board.blockPrefab : null;
    }

    private GameObject ResolveVisualPrefabForCurrentDay(int shapeIdx)
    {
        if (!useCompositePieceVisuals)
            return board != null ? board.blockPrefab : null;

        return ResolveBlockPrefabForCurrentDay(shapeIdx);
    }

    private Vector3 ResolveShapeVisualOffsetForCurrentDay(int shapeIdx)
    {
        if (config == null || config.dayBlockSets == null || config.dayBlockSets.Length == 0)
            return Vector3.zero;

        int day = Mathf.Clamp(ResolveCurrentDay(), 1, 4);
        TetrisMinigameConfig.DayBlockSet fallbackSet = null;
        for (int i = 0; i < config.dayBlockSets.Length; i++)
        {
            var set = config.dayBlockSets[i];
            if (set == null)
                continue;

            if (fallbackSet == null)
                fallbackSet = set;

            if (set.day == day)
            {
                var v = set.GetShapeOffset(shapeIdx);
                return new Vector3(v.x, v.y, 0f);
            }
        }

        if (fallbackSet != null)
        {
            var v = fallbackSet.GetShapeOffset(shapeIdx);
            return new Vector3(v.x, v.y, 0f);
        }

        return Vector3.zero;
    }

    private float ResolveShapeBaseRotationForCurrentDay(int shapeIdx)
    {
        if (config == null || config.dayBlockSets == null || config.dayBlockSets.Length == 0)
            return 0f;

        int day = Mathf.Clamp(ResolveCurrentDay(), 1, 4);
        TetrisMinigameConfig.DayBlockSet fallbackSet = null;
        for (int i = 0; i < config.dayBlockSets.Length; i++)
        {
            var set = config.dayBlockSets[i];
            if (set == null)
                continue;

            if (fallbackSet == null)
                fallbackSet = set;

            if (set.day == day)
                return set.GetShapeBaseRotation(shapeIdx);
        }

        if (fallbackSet != null)
            return fallbackSet.GetShapeBaseRotation(shapeIdx);

        return 0f;
    }

    private Vector2Int[] ResolveCollisionCells(int shapeIdx, GameObject piecePrefab, float baseRotationZ)
    {
        var fallback = (Vector2Int[])SHAPES[shapeIdx].Clone();

        if (!useSpriteDrivenCollision || !useCompositePieceVisuals || piecePrefab == null || board == null)
            return fallback;

        var prefabSr = piecePrefab.GetComponent<SpriteRenderer>();
        if (prefabSr == null || prefabSr.sprite == null)
            return fallback;

        var derived = DeriveCellsFromSpriteMesh(prefabSr.sprite, baseRotationZ, Mathf.Max(0.01f, board.cellSize));
        if (derived == null || derived.Length == 0)
            return fallback;

        return derived;
    }

    private Vector2Int[] DeriveCellsFromSpriteMesh(Sprite sprite, float baseRotationZ, float cell)
    {
        var verts = sprite.vertices;
        var tris = sprite.triangles;
        if (verts == null || tris == null || verts.Length < 3 || tris.Length < 3)
            return null;

        // Evaluate occupancy in piece-local cell centers against sprite mesh.
        // We inverse-rotate sample points so "baseRotation" visual orientation maps to logical rotation 0.
        var inv = Quaternion.Euler(0f, 0f, -baseRotationZ);
        var b = sprite.bounds;
        int minX = Mathf.FloorToInt((b.min.x / cell)) - 1;
        int maxX = Mathf.CeilToInt((b.max.x / cell)) + 1;
        int minY = Mathf.FloorToInt((b.min.y / cell)) - 1;
        int maxY = Mathf.CeilToInt((b.max.y / cell)) + 1;

        var occ = new HashSet<Vector2Int>();
        Vector2[] sampleOffsets = new Vector2[]
        {
            Vector2.zero,
            new Vector2(0.22f, 0f),
            new Vector2(-0.22f, 0f),
            new Vector2(0f, 0.22f),
            new Vector2(0f, -0.22f),
        };

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                bool hit = false;
                for (int s = 0; s < sampleOffsets.Length && !hit; s++)
                {
                    var p = new Vector3(
                        (x + sampleOffsets[s].x) * cell,
                        (y + sampleOffsets[s].y) * cell,
                        0f);
                    var q = inv * p;
                    hit = PointInSpriteMesh(new Vector2(q.x, q.y), verts, tris);
                }

                if (hit)
                    occ.Add(new Vector2Int(x, y));
            }
        }

        if (occ.Count == 0)
            return null;

        // Re-anchor around nearest center for stable rotation behavior.
        float ax = (float)occ.Average(v => v.x);
        float ay = (float)occ.Average(v => v.y);
        var anchor = new Vector2Int(Mathf.RoundToInt(ax), Mathf.RoundToInt(ay));

        var result = occ
            .Select(v => v - anchor)
            .OrderBy(v => v.y)
            .ThenBy(v => v.x)
            .ToArray();

        return result;
    }

    private bool PointInSpriteMesh(Vector2 p, Vector2[] verts, ushort[] tris)
    {
        for (int i = 0; i + 2 < tris.Length; i += 3)
        {
            Vector2 a = verts[tris[i]];
            Vector2 b = verts[tris[i + 1]];
            Vector2 c = verts[tris[i + 2]];
            if (PointInTriangle(p, a, b, c))
                return true;
        }
        return false;
    }

    private bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float s1 = Sign(p, a, b);
        float s2 = Sign(p, b, c);
        float s3 = Sign(p, c, a);

        bool hasNeg = (s1 < 0f) || (s2 < 0f) || (s3 < 0f);
        bool hasPos = (s1 > 0f) || (s2 > 0f) || (s3 > 0f);
        return !(hasNeg && hasPos);
    }

    private float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }

}
