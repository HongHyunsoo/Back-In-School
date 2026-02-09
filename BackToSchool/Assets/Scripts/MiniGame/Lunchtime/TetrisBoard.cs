using UnityEngine;

/// <summary>
/// Logical board for a simple Tetris (no line clears).
/// Coordinates: (0,0) bottom-left.
/// </summary>
public class TetrisBoard : MonoBehaviour
{
    [Header("Board")]
    public int width = 10;
    public int height = 20;

    [Header("Visual")]
    public float cellSize = 0.5f;
    public Vector2 origin = new Vector2(-2.5f, -4.5f);

    [Tooltip("Prefab for a single block (SpriteRenderer recommended). If null, a runtime sprite will be used.")]
    public GameObject blockPrefab;

    // Occupied cells -> block transform
    private Transform[,] blocks;

    private Sprite fallbackSprite;

    public void Init()
    {
        blocks = new Transform[width, height];
        if (blockPrefab == null)
        {
            fallbackSprite = CreateFallbackSprite();
        }
    }

    public bool IsInside(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;
    }

    public bool IsEmpty(Vector2Int cell)
    {
        if (!IsInside(cell)) return false;
        return blocks[cell.x, cell.y] == null;
    }

    public bool CanPlace(Vector2Int[] cells, Vector2Int position)
    {
        for (int i = 0; i < cells.Length; i++)
        {
            Vector2Int c = cells[i] + position;
            // Above the top is allowed only for the active falling piece spawn; for placement checks,
            // we'll allow y >= height only if it's NOT locking (caller controls).
            if (c.x < 0 || c.x >= width) return false;
            if (c.y < 0) return false;
            if (c.y >= height) continue;
            if (blocks[c.x, c.y] != null) return false;
        }
        return true;
    }

    public bool IsSpawnBlocked(Vector2Int[] cells, Vector2Int position)
    {
        for (int i = 0; i < cells.Length; i++)
        {
            Vector2Int c = cells[i] + position;
            if (c.x < 0 || c.x >= width) return true;
            if (c.y < 0) return true;
            if (c.y >= height) continue;
            if (blocks[c.x, c.y] != null) return true;
        }
        return false;
    }

    public void LockPiece(Vector2Int[] cells, Vector2Int position, Color color)
    {
        for (int i = 0; i < cells.Length; i++)
        {
            Vector2Int c = cells[i] + position;
            if (c.y >= height)
            {
                // locking above visible height => treated as overflow (game over handled by controller)
                continue;
            }

            if (!IsInside(c)) continue;

            var block = CreateBlockVisual(color);
            block.position = CellToWorld(c);
            blocks[c.x, c.y] = block;
        }
    }

    public Vector3 CellToWorld(Vector2Int cell)
    {
        return new Vector3(origin.x + (cell.x + 0.5f) * cellSize, origin.y + (cell.y + 0.5f) * cellSize, 0f);
    }

    private Transform CreateBlockVisual(Color color)
    {
        GameObject go;
        if (blockPrefab != null)
        {
            go = Instantiate(blockPrefab, transform);
        }
        else
        {
            go = new GameObject("Block");
            go.transform.SetParent(transform);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = fallbackSprite;
            sr.sortingOrder = 10;
            go.transform.localScale = Vector3.one * cellSize;
        }

        var sr2 = go.GetComponent<SpriteRenderer>();
        if (sr2 == null) sr2 = go.AddComponent<SpriteRenderer>();
        sr2.color = color;
        sr2.sortingOrder = 10;
        return go.transform;
    }

    private Sprite CreateFallbackSprite()
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }
}
