using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    private struct BufferedInput
    {
        public Vector2Int direction;
        public float expireTime;
    }

    [SerializeField]
    private Vector2Int startCell = new Vector2Int(0, 0);

    [SerializeField, Min(0.01f)]
    private float cellsPerSecond = 18f;

    [SerializeField]
    private float heightOffset = 1f;

    [Header("Input Responsiveness")]
    [SerializeField, Min(0f)]
    private float inputBufferSeconds = 0.16f;

    [SerializeField, Range(0.05f, 0.5f)]
    private float stickDeadzone = 0.15f;

    [SerializeField, Min(0f)]
    private float joystickHoldStepDelay = 0f;

    private const int MaxBufferedInputs = 2;

    private BoardGrid grid;
    private PlayerControlMode controlMode = PlayerControlMode.Wasd;
    private int gamepadDeviceId = InputDevice.InvalidDeviceId;
    private Gamepad cachedGamepad;

    private Vector2Int currentCell;
    private Vector2Int destinationCell;
    private Vector3 targetPosition;
    private bool isMoving;

    private Vector2Int lastStickDirection;
    private Vector2Int bufferedStickDirection;
    private float bufferedStickExpireTime;
    private float holdDelayTimer;

    private readonly Queue<BufferedInput> inputBuffer = new Queue<BufferedInput>(4);

    public bool Initialize(BoardGrid board)
    {
        return Initialize(board, PlayerControlMode.Wasd, InputDevice.InvalidDeviceId);
    }

    public bool Initialize(BoardGrid board, PlayerControlMode mode, int deviceId)
    {
        grid = board;
        controlMode = mode;
        gamepadDeviceId = deviceId;
        cachedGamepad = mode == PlayerControlMode.RemoteGamepad
            ? InputSystem.GetDeviceById(deviceId) as Gamepad
            : null;

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
        lastStickDirection = Vector2Int.zero;
        bufferedStickDirection = Vector2Int.zero;
        bufferedStickExpireTime = 0f;
        holdDelayTimer = 0f;
        inputBuffer.Clear();

        return true;
    }

    public void Tick(float deltaTime)
    {
        CollectInputs();
        PruneExpiredInputs();

        if (!isMoving && holdDelayTimer > 0f)
        {
            holdDelayTimer = Mathf.Max(0f, holdDelayTimer - deltaTime);
        }

        float remainingDistance = cellsPerSecond * grid.cellSize * deltaTime;

        while (remainingDistance > 0f)
        {
            if (!isMoving)
            {
                if (!TryStartNextStep())
                    break;
            }

            float distanceToTarget = Vector3.Distance(transform.position, targetPosition);

            if (remainingDistance >= distanceToTarget)
            {
                transform.position = targetPosition;
                remainingDistance -= distanceToTarget;
                currentCell = destinationCell;
                isMoving = false;
                holdDelayTimer = joystickHoldStepDelay;
            }
            else
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    targetPosition,
                    remainingDistance
                );

                remainingDistance = 0f;
            }
        }
    }

    private bool TryStartNextStep()
    {
        while (inputBuffer.Count > 0)
        {
            Vector2Int direction = inputBuffer.Dequeue().direction;

            if (TryBeginMove(direction))
                return true;
        }

        if (controlMode == PlayerControlMode.RemoteGamepad)
        {
            // Consume a newly flicked stick direction immediately (no hold delay).
            if (bufferedStickDirection != Vector2Int.zero &&
                Time.unscaledTime <= bufferedStickExpireTime)
            {
                Vector2Int flickDir = bufferedStickDirection;
                bufferedStickDirection = Vector2Int.zero;

                if (TryBeginMove(flickDir))
                    return true;
            }

            // Otherwise keep moving continuously while the stick stays held.
            if (holdDelayTimer <= 0f)
            {
                Vector2Int held = ReadRawJoystickDirection(currentCell);

                if (TryBeginMove(held))
                    return true;
            }
        }

        return false;
    }

    private bool TryBeginMove(Vector2Int direction)
    {
        if (direction == Vector2Int.zero)
            return false;

        Vector2Int next = currentCell + direction;

        if (!grid.IsWalkable(next))
            return false;

        destinationCell = next;
        targetPosition = GetPosition(next);
        isMoving = true;
        holdDelayTimer = 0f;
        return true;
    }

    // Allows instant 180-degree reversal mid-tile instead of waiting to reach the next cell.
    private bool TryReverseMidStep(Vector2Int desiredDirection)
    {
        if (!isMoving || desiredDirection == Vector2Int.zero)
            return false;

        Vector2Int currentMoveDir = destinationCell - currentCell;

        if (desiredDirection != -currentMoveDir)
            return false;

        Vector2Int previous = currentCell;
        currentCell = destinationCell;
        destinationCell = previous;
        targetPosition = GetPosition(destinationCell);
        return true;
    }

    private void CollectInputs()
    {
        switch (controlMode)
        {
            case PlayerControlMode.Wasd:
                CollectWasdInputs();
                break;
            case PlayerControlMode.Arrows:
                CollectArrowInputs();
                break;
            case PlayerControlMode.RemoteGamepad:
                CollectGamepadInputs();
                break;
        }
    }

    private void CollectWasdInputs()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.wKey.wasPressedThisFrame) HandleDiscreteInput(Vector2Int.up);
        if (keyboard.sKey.wasPressedThisFrame) HandleDiscreteInput(Vector2Int.down);
        if (keyboard.aKey.wasPressedThisFrame) HandleDiscreteInput(Vector2Int.left);
        if (keyboard.dKey.wasPressedThisFrame) HandleDiscreteInput(Vector2Int.right);
    }

    private void CollectArrowInputs()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.upArrowKey.wasPressedThisFrame) HandleDiscreteInput(Vector2Int.up);
        if (keyboard.downArrowKey.wasPressedThisFrame) HandleDiscreteInput(Vector2Int.down);
        if (keyboard.leftArrowKey.wasPressedThisFrame) HandleDiscreteInput(Vector2Int.left);
        if (keyboard.rightArrowKey.wasPressedThisFrame) HandleDiscreteInput(Vector2Int.right);
    }

    private void CollectGamepadInputs()
    {
        Gamepad gamepad = GetBoundGamepad();
        if (gamepad == null)
            return;

        if (gamepad.dpad.up.wasPressedThisFrame) HandleDiscreteInput(Vector2Int.up);
        if (gamepad.dpad.down.wasPressedThisFrame) HandleDiscreteInput(Vector2Int.down);
        if (gamepad.dpad.left.wasPressedThisFrame) HandleDiscreteInput(Vector2Int.left);
        if (gamepad.dpad.right.wasPressedThisFrame) HandleDiscreteInput(Vector2Int.right);

        // Look ahead to destinationCell while moving so corner turns register early.
        Vector2Int referenceCell = isMoving ? destinationCell : currentCell;
        Vector2Int stickDir = ReadRawJoystickDirection(referenceCell);

        if (stickDir == Vector2Int.zero)
        {
            lastStickDirection = Vector2Int.zero;
            return;
        }

        if (stickDir != lastStickDirection)
        {
            lastStickDirection = stickDir;

            if (TryReverseMidStep(stickDir))
            {
                bufferedStickDirection = Vector2Int.zero;
                return;
            }

            bufferedStickDirection = stickDir;
            bufferedStickExpireTime = Time.unscaledTime + inputBufferSeconds;
        }
    }

    private void HandleDiscreteInput(Vector2Int direction)
    {
        if (TryReverseMidStep(direction))
        {
            inputBuffer.Clear();
            return;
        }

        EnqueueDirection(direction);
    }

    // Reads raw unprocessed stick input to bypass Unity's built-in 0.125 deadzone delay.
    private Vector2Int ReadRawJoystickDirection(Vector2Int referenceCell)
    {
        Gamepad gamepad = GetBoundGamepad();
        if (gamepad == null)
            return Vector2Int.zero;

        Vector2 stick = gamepad.leftStick.ReadUnprocessedValue();
        float absX = Mathf.Abs(stick.x);
        float absY = Mathf.Abs(stick.y);

        if (absX < stickDeadzone && absY < stickDeadzone)
            return Vector2Int.zero;

        Vector2Int horizontal = absX >= stickDeadzone
            ? new Vector2Int(stick.x > 0f ? 1 : -1, 0)
            : Vector2Int.zero;

        Vector2Int vertical = absY >= stickDeadzone
            ? new Vector2Int(0, stick.y > 0f ? 1 : -1)
            : Vector2Int.zero;

        Vector2Int primary = absX >= absY ? horizontal : vertical;
        Vector2Int secondary = absX >= absY ? vertical : horizontal;

        if (primary != Vector2Int.zero && grid.IsWalkable(referenceCell + primary))
            return primary;

        if (secondary != Vector2Int.zero && grid.IsWalkable(referenceCell + secondary))
            return secondary;

        return primary;
    }

    private Gamepad GetBoundGamepad()
    {
        if (cachedGamepad == null || !cachedGamepad.added)
        {
            cachedGamepad = InputSystem.GetDeviceById(gamepadDeviceId) as Gamepad;
        }

        return cachedGamepad;
    }

    private void EnqueueDirection(Vector2Int direction)
    {
        if (direction == Vector2Int.zero)
            return;

        while (inputBuffer.Count >= MaxBufferedInputs)
        {
            inputBuffer.Dequeue();
        }

        inputBuffer.Enqueue(new BufferedInput
        {
            direction = direction,
            expireTime = Time.unscaledTime + inputBufferSeconds
        });
    }

    private void PruneExpiredInputs()
    {
        float now = Time.unscaledTime;

        while (inputBuffer.Count > 0 && inputBuffer.Peek().expireTime < now)
        {
            inputBuffer.Dequeue();
        }

        if (bufferedStickDirection != Vector2Int.zero && bufferedStickExpireTime < now)
        {
            bufferedStickDirection = Vector2Int.zero;
        }
    }

    private Vector3 GetPosition(Vector2Int cell)
    {
        return grid.CellToWorld(cell) + Vector3.up * heightOffset;
    }
}