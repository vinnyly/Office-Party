using System.Collections.Generic;
using UnityEngine;

public class BossMovement : MonoBehaviour
{
    [SerializeField]
    private Vector2Int startCell = new Vector2Int(4, 4);

    [SerializeField, Min(0.01f)]
    private float cellsPerSecond = 2f;

    [SerializeField]
    private float heightOffset = 1f;

    private BoardGrid grid;
    private PlayerMovement[] players;

    private float currentCellsPerSecond;
    private Vector2Int currentCell;
    private Vector2Int destinationCell;
    private Vector3 targetPosition;
    private bool isMoving;

    private readonly List<Vector2Int> patrol = new List<Vector2Int>();
    private int patrolIndex;

    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.left
    };

    public float BaseSpeed => cellsPerSecond;
    public float CurrentSpeed => currentCellsPerSecond;

    // Resets the boss at its base speed and builds the patrol for this round's seed.
    public bool Initialize(
        BoardGrid board,
        PlayerMovement[] trackedPlayers,
        int seed)
    {
        return Initialize(board, trackedPlayers, seed, cellsPerSecond);
    }

    // Resets the boss with an explicit starting speed and builds the patrol for this seed.
    public bool Initialize(
        BoardGrid board,
        PlayerMovement[] trackedPlayers,
        int seed,
        float initialSpeed)
    {
        grid = board;
        players = trackedPlayers;

        if (!grid.IsWalkable(startCell) || initialSpeed <= 0f)
        {
            Debug.LogError(
                "Boss needs a walkable Start Cell and positive speed.",
                this
            );

            return false;
        }

        currentCellsPerSecond = initialSpeed;
        currentCell = startCell;
        transform.position = GetPosition(startCell);
        isMoving = false;

        GeneratePatrol(seed);
        return true;
    }

    // Updates the boss's runtime movement speed without mutating the Inspector base speed.
    public void SetSpeed(float newCellsPerSecond)
    {
        currentCellsPerSecond = Mathf.Max(0.01f, newCellsPerSecond);
    }

    // Shuffles the nine corners and intersections into a repeatable route.
    private void GeneratePatrol(int seed)
    {
        patrol.Clear();

        int spacing = BoardGrid.Size / 2;

        for (int x = 0; x < BoardGrid.Size; x += spacing)
        {
            for (int y = 0; y < BoardGrid.Size; y += spacing)
            {
                patrol.Add(new Vector2Int(x, y));
            }
        }

        System.Random random = new System.Random(seed);

        for (int i = patrol.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);

            Vector2Int temporary = patrol[i];
            patrol[i] = patrol[j];
            patrol[j] = temporary;
        }

        patrolIndex = 0;
    }

    // Finishes the current step before choosing another patrol or chase move.
    public void Tick(float deltaTime)
    {
        if (!isMoving)
        {
            destinationCell = ChooseNextCell();

            if (destinationCell == currentCell)
                return;

            targetPosition = GetPosition(destinationCell);
            isMoving = true;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            currentCellsPerSecond * grid.cellSize * deltaTime
        );

        if (transform.position == targetPosition)
        {
            transform.position = targetPosition;
            currentCell = destinationCell;
            isMoving = false;
        }
    }

    // Chases a visible player or resumes the next unfinished patrol waypoint.
    private Vector2Int ChooseNextCell()
    {
        if (FindVisibleDirection(out Vector2Int direction))
        {
            Vector2Int next = currentCell + direction;

            return grid.IsWalkable(next)
                ? next
                : currentCell;
        }

        if (currentCell == patrol[patrolIndex])
        {
            patrolIndex = (patrolIndex + 1) % patrol.Count;
        }

        return FindNextStep(patrol[patrolIndex]);
    }

    // Reports current visibility independently of the boss's previous decision.
    public bool CanSeeAnyPlayer()
    {
        return FindVisibleDirection(out _);
    }

    // Finds the direction toward the nearest player in an unobstructed sight line.
    private bool FindVisibleDirection(out Vector2Int direction)
    {
        direction = Vector2Int.zero;

        float nearest = float.PositiveInfinity;
        bool found = false;

        foreach (PlayerMovement player in players)
        {
            if (player == null || !player.isActiveAndEnabled)
                continue;

            if (!HasLineOfSight(player.transform.position))
                continue;

            Vector3 difference =
                player.transform.position - transform.position;

            float distance =
                Mathf.Abs(difference.x) + Mathf.Abs(difference.z);

            if (distance >= nearest)
                continue;

            nearest = distance;
            found = true;

            direction = Mathf.Abs(difference.x) > Mathf.Abs(difference.z)
                ? new Vector2Int(System.Math.Sign(difference.x), 0)
                : new Vector2Int(0, System.Math.Sign(difference.z));
        }

        return found;
    }

    // Checks horizontal or vertical alignment and treats blocked cells as walls.
    private bool HasLineOfSight(Vector3 target)
    {
        Vector3 difference = target - transform.position;
        float tolerance = grid.cellSize * 0.001f;

        if (Mathf.Abs(difference.x) > tolerance &&
            Mathf.Abs(difference.z) > tolerance)
        {
            return false;
        }

        Vector2Int cell = grid.WorldToCell(transform.position);
        Vector2Int goal = grid.WorldToCell(target);

        if (!grid.IsWalkable(cell) || !grid.IsWalkable(goal))
            return false;

        if (cell.x != goal.x && cell.y != goal.y)
            return false;

        Vector2Int step = new Vector2Int(
            System.Math.Sign(goal.x - cell.x),
            System.Math.Sign(goal.y - cell.y)
        );

        while (cell != goal)
        {
            cell += step;

            if (!grid.IsWalkable(cell))
                return false;
        }

        return true;
    }

    // Uses breadth-first search to find one step along a shortest valid path.
    private Vector2Int FindNextStep(Vector2Int goal)
    {
        if (currentCell == goal)
            return currentCell;

        Queue<Vector2Int> frontier = new Queue<Vector2Int>();

        Dictionary<Vector2Int, Vector2Int> previous =
            new Dictionary<Vector2Int, Vector2Int>();

        frontier.Enqueue(currentCell);
        previous[currentCell] = currentCell;

        while (frontier.Count > 0)
        {
            Vector2Int cell = frontier.Dequeue();

            if (cell == goal)
            {
                Vector2Int next = goal;

                while (previous[next] != currentCell)
                {
                    next = previous[next];
                }

                return next;
            }

            foreach (Vector2Int direction in Directions)
            {
                Vector2Int next = cell + direction;

                if (!grid.IsWalkable(next) || previous.ContainsKey(next))
                    continue;

                previous[next] = cell;
                frontier.Enqueue(next);
            }
        }

        return currentCell;
    }

    // Positions the boss's pivot at the correct height above a cell.
    private Vector3 GetPosition(Vector2Int cell)
    {
        return grid.CellToWorld(cell) + Vector3.up * heightOffset;
    }
}