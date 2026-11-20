using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public enum PlayerControlMode
{
    None,
    Wasd,
    Arrows,
    WaitingForGamepad,
    RemoteGamepad
}

public struct PlayerSlotState
{
    public PlayerControlMode mode;
    public int gamepadDeviceId;
    public string deviceLabel;

    public bool IsReady =>
        mode == PlayerControlMode.Wasd ||
        mode == PlayerControlMode.Arrows ||
        mode == PlayerControlMode.RemoteGamepad;

    public void Clear()
    {
        mode = PlayerControlMode.None;
        gamepadDeviceId = InputDevice.InvalidDeviceId;
        deviceLabel = "";
    }
}

public static class RemoteGamepadUtility
{
    public static bool IsRemoteGamepad(InputDevice device, out Gamepad gamepad)
    {
        gamepad = device as Gamepad;

        if (gamepad == null)
            return false;

        bool matchesName =
            !string.IsNullOrEmpty(gamepad.name) &&
            gamepad.name.StartsWith("XInputController", StringComparison.OrdinalIgnoreCase);

        bool matchesLayout =
            !string.IsNullOrEmpty(gamepad.layout) &&
            gamepad.layout.StartsWith("XInputController", StringComparison.OrdinalIgnoreCase);

        return matchesName || matchesLayout;
    }

    public static bool HasPairingInput(Gamepad gamepad)
    {
        if (gamepad == null)
            return false;

        var controls = gamepad.allControls;

        for (int i = 0; i < controls.Count; i++)
        {
            if (controls[i] is ButtonControl button && button.wasPressedThisFrame)
                return true;
        }

        return gamepad.leftStick.ReadValue().sqrMagnitude >= 0.25f ||
               gamepad.rightStick.ReadValue().sqrMagnitude >= 0.25f;
    }
}