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

    private int lockedCount = 0;
    private float fallTimer = 0f;

    private TetrisPiece active;
    private readonly List<Transform> activeBlocks = new();

    private System.Random rng = new System.Random();

    private bool ended = false;

    // 7-bag generator
    private readonly List<int> bag = new();

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
        if (KeyDownLeft()) TryMove(new Vector2Int(-1, 0));
        if (KeyDownRight()) TryMove(new Vector2Int(1, 0));

        if (KeyDownRotate()) TryRotateCW();

        // Optional: manual down step on key down.
        if (KeyDownDown()) StepDown();
    }

    private void StepDown()
    {
        if (active == null) return;

        if (!TryMove(new Vector2Int(0, -1)))
        {
            // Lock
            bool overflow = LocksAboveTop(active.cells, active.position);
            board.LockPiece(active.cells, active.position, active.color);
            ClearActiveVisuals();
            lockedCount++;

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

        for (int i = 0; i < active.cells.Length; i++)
        {
            GameObject go;
            if (board.blockPrefab != null)
            {
                go = Instantiate(board.blockPrefab, transform);
            }
            else
            {
                go = new GameObject("ActiveBlock");
                go.transform.SetParent(transform);
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
        for (int i = 0; i < activeBlocks.Count; i++)
        {
            var cell = active.cells[i] + active.position;
            activeBlocks[i].position = board.CellToWorld(cell);
        }
    }

    private void ClearActiveVisuals()
    {
        for (int i = 0; i < activeBlocks.Count; i++)
        {
            if (activeBlocks[i] != null)
                Destroy(activeBlocks[i].gameObject);
        }
        activeBlocks.Clear();
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
    private bool KeyDownLeft()  => Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow);
    private bool KeyDownRight() => Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow);
    private bool KeyDownDown()  => Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow);
    private bool KeyDownRotate()=> Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);

    private bool IsSoftDropping()=> Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
}
