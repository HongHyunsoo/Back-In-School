using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple Tetris: move/rotate + gravity, no line clears.
/// Goal: lock N pieces successfully.
/// Controls: WASD or Arrow keys.
/// </summary>
public class TetrisMinigameController : MonoBehaviour
{
    [Header("Goal")]
    public int targetLockedPieces = 15;

    [Header("Difficulty")]
    public float fallInterval = 0.75f;
    public float softDropInterval = 0.06f;

    [Header("Board")]
    public TetrisBoard board;

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
    private readonly List<Transform> activeBlocks = new();
    private Transform activeVisualRoot;

    private System.Random rng = new System.Random();

    private bool ended = false;

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
        new []{ new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(-1,1), new Vector2Int(0,1) },
        // Z
        new []{ new Vector2Int(-1,0), new Vector2Int(0,0), new Vector2Int(0,1), new Vector2Int(1,1) },
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
        string flowId = PlayerPrefs.GetString("FLOW_ID", "");
        if (string.IsNullOrEmpty(flowId) || !flowId.StartsWith("LUNCH_"))
        {
            enabled = false;
            return;
        }

        if (board == null)
        {
            var bgo = new GameObject("TetrisBoard");
            bgo.transform.SetParent(transform);
            board = bgo.AddComponent<TetrisBoard>();
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
        if (lockPending)
        {
            if (TickLockPending()) return;
            UpdateActiveVisuals();
            return;
        }

        HandleInput();

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
        if (lockPending) return;

        if (KeyDownLeft()) TryMove(new Vector2Int(-1, 0));
        if (KeyDownRight()) TryMove(new Vector2Int(1, 0));

        if (KeyDownRotate()) TryRotateCW();

        // Optional: manual down step on key down.
        if (KeyDownDown()) StepDown();
    }

    private void StepDown()
    {
        if (active == null) return;

        if (TryMove(new Vector2Int(0, -1)))
        {
            TriggerFallStretch();
            return;
        }

        if (enableJelly && !lockPending)
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
        var cells = (Vector2Int[])SHAPES[shapeIdx].Clone();
        var color = COLORS[shapeIdx];

        // Spawn around top-center, slightly above board to feel nicer
        var spawnPos = new Vector2Int(board.width / 2 - 1, board.height - 2);

        active = new TetrisPiece(cells, spawnPos, color);

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

    private bool TryMove(Vector2Int delta)
    {
        if (active == null) return false;

        var nextPos = active.position + delta;
        if (board.CanPlace(active.cells, nextPos))
        {
            active.position = nextPos;
            return true;
        }
        return false;
    }

    private void TryRotateCW()
    {
        if (active == null) return;

        var rotated = active.RotatedCW();

        // Basic wall-kick: try a few horizontal offsets
        Vector2Int[] kicks = new Vector2Int[]
        {
            new Vector2Int(0,0),
            new Vector2Int(1,0),
            new Vector2Int(-1,0),
            new Vector2Int(2,0),
            new Vector2Int(-2,0),
            new Vector2Int(0,1),
        };

        for (int i = 0; i < kicks.Length; i++)
        {
            var pos = active.position + kicks[i];
            if (board.CanPlace(rotated, pos))
            {
                active.cells = rotated;
                active.position = pos;
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
        root.transform.SetParent(transform, false);
        activeVisualRoot = root.transform;

        for (int i = 0; i < active.cells.Length; i++)
        {
            GameObject go;
            if (board.blockPrefab != null)
            {
                go = Instantiate(board.blockPrefab, activeVisualRoot);
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
            activeVisualRoot.position = board.CellToWorld(active.position);
            activeVisualRoot.localScale = new Vector3(jellyScale.x, jellyScale.y, 1f);
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

        bool overflow = LocksAboveTop(active.cells, active.position);
        board.LockPiece(active.cells, active.position, active.color);
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
}
