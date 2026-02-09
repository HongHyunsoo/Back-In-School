using UnityEngine;

/// <summary>
/// Represents an active falling tetromino.
/// </summary>
public class TetrisPiece
{
    public Vector2Int[] cells;     // relative cells around pivot
    public Vector2Int position;    // pivot position on board
    public Color color;

    public TetrisPiece(Vector2Int[] cells, Vector2Int position, Color color)
    {
        this.cells = cells;
        this.position = position;
        this.color = color;
    }

    public Vector2Int[] RotatedCW()
    {
        // 90 deg rotation: (x, y) -> (y, -x)
        var rotated = new Vector2Int[cells.Length];
        for (int i = 0; i < cells.Length; i++)
        {
            var c = cells[i];
            rotated[i] = new Vector2Int(c.y, -c.x);
        }
        return rotated;
    }
}
