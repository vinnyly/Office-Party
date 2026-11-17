using UnityEngine;

public class BoardGrid : MonoBehaviour
{
    public const int Size = 9;
    private const int Center = Size / 2;

    [Min(0.01f)]
    public float cellSize = 1f;

    // Checks whether a cell is on the border or middle cross.
    public bool IsWalkable(Vector2Int cell)
    {
        if (cell.x < 0 || cell.x >= Size ||
            cell.y < 0 || cell.y >= Size)
        {
            return false;
        }

        return cell.x == 0 || cell.x == Center || cell.x == Size - 1 ||
               cell.y == 0 || cell.y == Center || cell.y == Size - 1;
    }

    // Converts a cell to a floor position using this object's position as the board center.
    public Vector3 CellToWorld(Vector2Int cell)
    {
        return transform.position + new Vector3(
            (cell.x - Center) * cellSize,
            0f,
            (cell.y - Center) * cellSize
        );
    }

    // Finds the nearest cell to a world position.
    public Vector2Int WorldToCell(Vector3 position)
    {
        Vector3 offset = position - transform.position;

        return new Vector2Int(
            Mathf.RoundToInt(offset.x / cellSize + Center),
            Mathf.RoundToInt(offset.z / cellSize + Center)
        );
    }

    // Draws walkable and blocked cells in the Scene view.
    private void OnDrawGizmos()
    {
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);

                Gizmos.color = IsWalkable(cell)
                    ? Color.green
                    : Color.red;

                Gizmos.DrawWireCube(
                    CellToWorld(cell) + Vector3.up * 0.05f,
                    new Vector3(cellSize, 0.02f, cellSize)
                );
            }
        }
    }
}