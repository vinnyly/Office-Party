using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField]
    private Vector2Int startCell = new Vector2Int(0, 0);

    [SerializeField, Min(0.01f)]
    private float cellsPerSecond = 4f;

    [SerializeField]
    private float heightOffset = 1f;

    private BoardGrid grid;
    private Vector2Int currentCell;
    private Vector2Int destinationCell;
    private Vector3 targetPosition;
    private bool isMoving;

    // Places the player at its starting cell and clears the previous move.
    public bool Initialize(BoardGrid board)
    {
        grid = board;

        if (!grid.IsWalkable(startCell) || cellsPerSecond <= 0f)
        {
            Debug.LogError(
                "Player needs a walkable Start Cell and positive speed.",
                this
            );

            return false;
        }

        currentCell = startCell;
        targetPosition = GetPosition(startCell);
        transform.position = targetPosition;
        isMoving = false;

        return true;
    }

    // Accepts one key press and moves toward the next valid cell.
    public void Tick(float deltaTime)
    {
        if (!isMoving)
        {
            Vector2Int direction = ReadDirection();
            Vector2Int next = currentCell + direction;

            if (direction != Vector2Int.zero && grid.IsWalkable(next))
            {
                destinationCell = next;
                targetPosition = GetPosition(next);
                isMoving = true;
            }
        }

        if (!isMoving)
            return;

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            cellsPerSecond * grid.cellSize * deltaTime
        );

        if (transform.position == targetPosition)
        {
            transform.position = targetPosition;
            currentCell = destinationCell;
            isMoving = false;
        }
    }

    // Reads a new WASD press; holding a key does not repeat it.
    private Vector2Int ReadDirection()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return Vector2Int.zero;

        if (keyboard.wKey.wasPressedThisFrame) return Vector2Int.up;
        if (keyboard.sKey.wasPressedThisFrame) return Vector2Int.down;
        if (keyboard.aKey.wasPressedThisFrame) return Vector2Int.left;
        if (keyboard.dKey.wasPressedThisFrame) return Vector2Int.right;

        return Vector2Int.zero;
    }

    // Positions the player's pivot at the correct height above a cell.
    private Vector3 GetPosition(Vector2Int cell)
    {
        return grid.CellToWorld(cell) + Vector3.up * heightOffset;
    }
}