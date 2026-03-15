using UnityEngine;

/// <summary>
/// Logical board for a simple Tetris (no line clears).
/// Coordinates: (0,0) bottom-left.
/// </summary>
public class TetrisBoard : MonoBehaviour
{
    public enum MaskSampleMode
    {
        Auto = 0,
        Alpha = 1,
        Luma = 2
    }

    [Header("Board")]
    public int width = 9;
    public int height = 10;

    [Header("Visual")]
    public float cellSize = 0.5f;
    public Vector2 origin = new Vector2(-2.5f, -2.5f);
    [Tooltip("Derive grid cell size/origin directly from this object's SpriteRenderer bounds (best when board art is exact 9x10 cells).")]
    public bool autoFitGridToBoardSprite;
    [Tooltip("Auto-center logical grid inside this object's SpriteRenderer bounds at runtime.")]
    public bool autoCenterGridToBoardSprite;

    [Tooltip("Prefab for a single block (SpriteRenderer recommended). If null, a runtime sprite will be used.")]
    public GameObject blockPrefab;
    [Header("Shape Mask (Optional)")]
    public Texture2D maskTexture;
    public MaskSampleMode maskMode = MaskSampleMode.Auto;
    [Range(0f, 1f)] public float maskThreshold = 0.5f;
    public bool invertMask;
    public bool maskFlipX;
    public bool maskFlipY;

    // Occupied cells -> block transform
    private Transform[,] blocks;
    private bool[,] occupied;
    private bool[,] usableCells;

    private Sprite fallbackSprite;

    public void Init()
    {
        AutoCenterGridToSpriteIfNeeded();
        blocks = new Transform[width, height];
        occupied = new bool[width, height];
        usableCells = new bool[width, height];
        BuildUsableMask();
        if (blockPrefab == null)
        {
            fallbackSprite = CreateFallbackSprite();
        }
    }

    public bool IsInside(Vector2Int cell)
    {
        return cell.x >= 0 &&
               cell.x < width &&
               cell.y >= 0 &&
               cell.y < height &&
               IsUsableCell(cell.x, cell.y);
    }

    public bool IsEmpty(Vector2Int cell)
    {
        if (!IsInside(cell)) return false;
        return !occupied[cell.x, cell.y];
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
            if (!IsUsableCell(c.x, c.y)) return false;
            if (occupied[c.x, c.y]) return false;
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
            if (!IsUsableCell(c.x, c.y)) return true;
            if (occupied[c.x, c.y]) return true;
        }
        return false;
    }

    public void LockPiece(Vector2Int[] cells, Vector2Int position, Color color)
    {
        LockPiece(cells, position, color, null, true, false, 0f, Vector3.zero);
    }

    public void LockPiece(
        Vector2Int[] cells,
        Vector2Int position,
        Color color,
        GameObject prefabOverride,
        bool applyColor,
        bool useCompositeVisual,
        float compositeRotationZ = 0f,
        Vector3 compositeLocalOffset = default(Vector3))
    {
        Transform compositeVisual = null;
        if (useCompositeVisual && prefabOverride != null)
        {
            // One visual object represents the whole tetromino; all occupied cells point to same transform.
            compositeVisual = CreateBlockVisual(color, prefabOverride, applyColor);
            compositeVisual.localRotation = Quaternion.Euler(0f, 0f, compositeRotationZ);
            var rotatedOffset = compositeVisual.localRotation * compositeLocalOffset;
            compositeVisual.localPosition = CellToLocal(position) + rotatedOffset;
        }

        for (int i = 0; i < cells.Length; i++)
        {
            Vector2Int c = cells[i] + position;
            if (c.y >= height)
            {
                // locking above visible height => treated as overflow (game over handled by controller)
                continue;
            }

            if (!IsInside(c)) continue;

            if (compositeVisual != null)
            {
                occupied[c.x, c.y] = true;
                blocks[c.x, c.y] = compositeVisual;
            }
            else
            {
                occupied[c.x, c.y] = true;
                var block = CreateBlockVisual(color, prefabOverride, applyColor);
                block.localPosition = CellToLocal(c);
                blocks[c.x, c.y] = block;
            }
        }
    }

    public Vector3 CellToWorld(Vector2Int cell)
    {
        return transform.TransformPoint(CellToLocal(cell));
    }

    public Vector3 CellToLocal(Vector2Int cell)
    {
        return new Vector3(
            origin.x + (cell.x + 0.5f) * cellSize,
            origin.y + (cell.y + 0.5f) * cellSize,
            0f);
    }

    private Transform CreateBlockVisual(Color color, GameObject prefabOverride, bool applyColor)
    {
        GameObject go;
        var selectedPrefab = prefabOverride != null ? prefabOverride : blockPrefab;
        if (selectedPrefab != null)
        {
            go = Instantiate(selectedPrefab, transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
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
        if (applyColor)
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

    private bool IsUsableCell(int x, int y)
    {
        if (usableCells == null || x < 0 || y < 0 || x >= width || y >= height)
            return false;
        return usableCells[x, y];
    }

    private Color SampleMaskCellCenter(int x, int y)
    {
        float u = (x + 0.5f) / Mathf.Max(1, width);
        float v = (y + 0.5f) / Mathf.Max(1, height);
        if (maskFlipX) u = 1f - u;
        if (maskFlipY) v = 1f - v;

        int tx = Mathf.Clamp(Mathf.FloorToInt(u * maskTexture.width), 0, maskTexture.width - 1);
        int ty = Mathf.Clamp(Mathf.FloorToInt(v * maskTexture.height), 0, maskTexture.height - 1);
        return maskTexture.GetPixel(tx, ty);
    }

    private void AutoCenterGridToSpriteIfNeeded()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null)
            return;

        var sp = sr.sprite;
        float ppu = sp.pixelsPerUnit <= 0f ? 100f : sp.pixelsPerUnit;
        float spriteW = sp.rect.width / ppu;
        float spriteH = sp.rect.height / ppu;
        Vector2 pivotUnits = sp.pivot / ppu;
        Vector2 spriteMin = new Vector2(-pivotUnits.x, -pivotUnits.y);
        Vector2 spriteCenter = spriteMin + new Vector2(spriteW * 0.5f, spriteH * 0.5f);

        if (autoFitGridToBoardSprite)
        {
            int w = Mathf.Max(1, width);
            int h = Mathf.Max(1, height);
            float cellX = spriteW / w;
            float cellY = spriteH / h;
            cellSize = Mathf.Max(0.01f, Mathf.Min(cellX, cellY));
            origin = spriteMin;
            return;
        }

        if (autoCenterGridToBoardSprite)
        {
            float gridW = Mathf.Max(1, width) * Mathf.Max(0.01f, cellSize);
            float gridH = Mathf.Max(1, height) * Mathf.Max(0.01f, cellSize);

            float ox = spriteCenter.x - (gridW * 0.5f);
            float oy = spriteCenter.y - (gridH * 0.5f);
            origin = new Vector2(ox, oy);
        }
    }

    private void BuildUsableMask()
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                usableCells[x, y] = true;
        }

        if (maskTexture == null)
            return;

        bool readFailed = false;
        bool hasTransparentPixels = false;
        if (maskMode == MaskSampleMode.Auto)
        {
            for (int y = 0; y < height && !hasTransparentPixels; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    try
                    {
                        if (SampleMaskCellCenter(x, y).a < 0.99f)
                        {
                            hasTransparentPixels = true;
                            break;
                        }
                    }
                    catch (UnityException)
                    {
                        readFailed = true;
                        break;
                    }
                }
            }
        }

        bool useAlpha;
        if (maskMode == MaskSampleMode.Alpha)
            useAlpha = true;
        else if (maskMode == MaskSampleMode.Luma)
            useAlpha = false;
        else
            useAlpha = hasTransparentPixels;

        int usableCount = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color c;
                try
                {
                    c = SampleMaskCellCenter(x, y);
                }
                catch (UnityException)
                {
                    readFailed = true;
                    c = Color.white;
                }
                float luma = (c.r + c.g + c.b) / 3f;
                bool usable = useAlpha ? (c.a >= maskThreshold) : (luma >= maskThreshold);
                usableCells[x, y] = invertMask ? !usable : usable;
                if (usableCells[x, y])
                    usableCount++;
            }
        }

        if (readFailed)
        {
            Debug.LogWarning(
                "[TetrisBoard] maskTexture read failed. Enable Read/Write in texture import settings. " +
                "Using default full-usable board until fixed.",
                this);
        }

        Debug.Log(
            $"[TetrisBoard] mask applied. mode={(useAlpha ? "alpha" : "luma")} usableCells={usableCount}/{width * height}",
            this);
    }
}
