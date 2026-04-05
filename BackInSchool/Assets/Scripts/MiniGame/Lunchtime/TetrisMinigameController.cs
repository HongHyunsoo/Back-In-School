using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

    [Header("Difficulty")]
    public float fallInterval = 0.75f;
    public float softDropInterval = 0.06f;

    [Header("Board")]
    public TetrisBoard board;
    [Tooltip("Optional spawn anchor in scene. If assigned, piece spawn starts near this point.")]
    public Transform spawnPoint;

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

    private System.Random rng = new System.Random();

    private bool ended = false;
    private bool boardAutoCreated = false;

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
    }

    private void Start()
    {
        board.Init();
        SpawnNewPiece();
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
    }

    private int NextFromBag()
    {
        if (bag.Count == 0)
        {
            for (int i = 0; i < 7; i++) bag.Add(i);
            // shuffle
            for (int i = bag.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (bag[i], bag[j]) = (bag[j], bag[i]);
            }
        }
        int idx = bag[bag.Count - 1];
        bag.RemoveAt(bag.Count - 1);
        return idx;
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
        ClearActiveVisuals();
        lockedCount++;
        lockPending = false;
        lockPendingTimer = 0f;

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

    private void End(bool success)
    {
        ended = true;
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
