using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class GameController : MonoBehaviour
{
    public const int MaxPlayers = 4;

    private enum GameState
    {
        Menu,
        Playing,
        RoundTransition,
        Victory,
        Defeat
    }

    [SerializeField] private BoardGrid grid;
    [SerializeField] private BossMovement boss;
    [SerializeField] private PlayerMovement[] players;

    [SerializeField, Range(1, MaxPlayers)]
    private int minPlayersToStart = 1;

    [Header("Round & Speed Progression")]
    [SerializeField, Min(1f)]
    private float speedIncreaseInterval = 14f;

    [SerializeField, Min(0.1f)]
    private float speedIncrement = 1f;

    [SerializeField, Min(0)]
    private int round1SpeedUps = 2; // 2 -> 3 -> 4 over 42s

    [SerializeField, Min(0f)]
    private float nightmareStartSpeedBonus = 1f; // Jumps to 5 at Nightmare start

    [SerializeField, Min(0)]
    private int nightmareSpeedUps = 2; // 5 -> 6 -> 7 over 42s

    [SerializeField, Min(0.5f)]
    private float speedUpBannerDuration = 2.5f;

    [SerializeField, Min(0.5f)]
    private float roundCompletePause = 2.5f;

    [SerializeField, Min(0.5f)]
    private float nightmareIntroPause = 2f;

    [Header("Scoring & Catch")]
    [SerializeField, Min(0)]
    private int seenPenaltyPerSecond = 1;

    [SerializeField, Range(0.05f, 0.49f)]
    private float catchDistanceInCells = 0.3f;

    [SerializeField] private GameState state;
    [SerializeField] private int currentSeed;

    private readonly PlayerSlotState[] slots = new PlayerSlotState[MaxPlayers];
    private readonly List<PlayerMovement> activePlayers = new List<PlayerMovement>(MaxPlayers);
    private readonly List<int> activeSlotIndices = new List<int>(MaxPlayers);

    private GameSaveStore saves;
    private GameUI ui;
    private bool recordsLoaded;

    private int currentRound;
    private double roundElapsed;
    private double currentRoundDuration;
    private int currentRoundMaxSpeedUps;
    private int speedUpsTriggered;

    private double runSpeedInterval;
    private float runSpeedIncrement;
    private float runNightmareStartBonus;
    private int runPenalty;

    private double totalElapsed;
    private double seenSeconds;

    private double transitionTimer;
    private bool nightmarePrepared;

    private Vector3[] previousPlayers;
    private string saveError = "";

    private int Score =>
        RoundRules.Score(totalElapsed, seenSeconds, runPenalty);

    private string CurrentRoundName =>
        currentRound == 1 ? "Round 1" : "Nightmare Round";

    private void OnEnable()
    {
        QualitySettings.vSyncCount = 0;
        QualitySettings.maxQueuedFrames = 1;
        Application.targetFrameRate = 240;

        InputSystem.pollingFrequency = 500f;
        InputSystem.onDeviceChange += OnInputDeviceChange;
    }

    private void OnDisable()
    {
        InputSystem.onDeviceChange -= OnInputDeviceChange;
    }

    // Loads records, builds the overlay, and waits at the start menu.
    private void Start()
    {
        state = GameState.Menu;

        for (int i = 0; i < MaxPlayers; i++)
        {
            slots[i].Clear();
        }

        ui = GetComponent<GameUI>();

        if (ui == null)
        {
            ui = gameObject.AddComponent<GameUI>();
        }

        if (!ui.Build(this))
        {
            enabled = false;
            return;
        }

        saves = new GameSaveStore(Application.persistentDataPath);

        Debug.Log("Save file: " + saves.SavePath, this);

        recordsLoaded = saves.Load(out saveError);

        ShowMenu();

        if (recordsLoaded)
        {
            PreparePreviewActors(saves.Data.nextBossSeed);
        }
    }

    // Positions scene actors behind the menu before any slots are chosen.
    private void PreparePreviewActors(int seed)
    {
        if (grid == null || boss == null || grid.cellSize <= 0f)
            return;

        if (players != null)
        {
            foreach (PlayerMovement player in players)
            {
                if (player != null)
                {
                    player.Initialize(grid);
                }
            }
        }

        if (boss.isActiveAndEnabled)
        {
            boss.Initialize(grid, players ?? Array.Empty<PlayerMovement>(), seed);
        }
    }

    // Assigns WASD to a slot, resetting any previous WASD slot back to unselected.
    public void SelectWasd(int slotIndex)
    {
        if (state != GameState.Menu || !IsValidSlot(slotIndex))
            return;

        if (slots[slotIndex].mode == PlayerControlMode.Wasd)
        {
            slots[slotIndex].Clear();
        }
        else
        {
            ClearModeFromAllSlots(PlayerControlMode.Wasd);
            slots[slotIndex].Clear();
            slots[slotIndex].mode = PlayerControlMode.Wasd;
        }

        ui.UpdatePlayerSlots(slots);
        ui.SetStatus("");
    }

    // Assigns Arrow Keys to a slot, resetting any previous Arrow Keys slot back to unselected.
    public void SelectArrows(int slotIndex)
    {
        if (state != GameState.Menu || !IsValidSlot(slotIndex))
            return;

        if (slots[slotIndex].mode == PlayerControlMode.Arrows)
        {
            slots[slotIndex].Clear();
        }
        else
        {
            ClearModeFromAllSlots(PlayerControlMode.Arrows);
            slots[slotIndex].Clear();
            slots[slotIndex].mode = PlayerControlMode.Arrows;
        }

        ui.UpdatePlayerSlots(slots);
        ui.SetStatus("");
    }

    // Starts waiting for a remote XInputController gamepad, or cancels/disconnects if already set.
    public void SelectPhone(int slotIndex)
    {
        if (state != GameState.Menu || !IsValidSlot(slotIndex))
            return;

        if (slots[slotIndex].mode == PlayerControlMode.WaitingForGamepad ||
            slots[slotIndex].mode == PlayerControlMode.RemoteGamepad)
        {
            slots[slotIndex].Clear();
        }
        else
        {
            slots[slotIndex].Clear();
            slots[slotIndex].mode = PlayerControlMode.WaitingForGamepad;
        }

        ui.UpdatePlayerSlots(slots);
        ui.SetStatus("");
    }

    // Checks scene references and configures only the selected players for Round 1.
    private bool PrepareActors(int seed)
    {
        if (grid == null || boss == null || players == null || players.Length == 0)
        {
            ShowError("Assign Grid, Boss and Players on GameController.");
            return false;
        }

        if (grid.cellSize <= 0f || !boss.isActiveAndEnabled)
        {
            ShowError("Use a positive Cell Size and enable the boss.");
            return false;
        }

        activePlayers.Clear();
        activeSlotIndices.Clear();

        for (int i = 0; i < MaxPlayers; i++)
        {
            bool hasActor = i < players.Length && players[i] != null;

            if (!slots[i].IsReady)
            {
                if (hasActor)
                {
                    players[i].gameObject.SetActive(false);
                }

                continue;
            }

            if (!hasActor)
            {
                ShowError(
                    $"Player {i + 1} is selected, but GameController.players[{i}] is not assigned."
                );

                return false;
            }

            if (slots[i].mode == PlayerControlMode.RemoteGamepad &&
                InputSystem.GetDeviceById(slots[i].gamepadDeviceId) as Gamepad == null)
            {
                slots[i].Clear();
                ui.UpdatePlayerSlots(slots);
                ShowError($"Player {i + 1}'s controller disconnected.");
                return false;
            }

            players[i].gameObject.SetActive(true);

            if (!players[i].Initialize(grid, slots[i].mode, slots[i].gamepadDeviceId))
            {
                ShowError($"Check Player {i + 1}'s Start Cell and speed.");
                return false;
            }

            activePlayers.Add(players[i]);
            activeSlotIndices.Add(i);
        }

        if (activePlayers.Count < minPlayersToStart)
        {
            ShowError(
                minPlayersToStart == 1
                    ? "Select at least 1 player to start."
                    : $"Select at least {minPlayersToStart} players to start."
            );

            return false;
        }

        if (!boss.Initialize(grid, activePlayers.ToArray(), seed, boss.BaseSpeed))
        {
            ShowError("Check the boss's Start Cell and speed.");
            return false;
        }

        return true;
    }

    // Resets actors to their start cells for Nightmare Round with the escalated boss speed.
    private void PrepareNightmareActors()
    {
        for (int i = 0; i < activePlayers.Count; i++)
        {
            int slotIndex = activeSlotIndices[i];
            activePlayers[i].Initialize(
                grid,
                slots[slotIndex].mode,
                slots[slotIndex].gamepadDeviceId
            );
        }

        float nightmareStartSpeed = boss.CurrentSpeed + runNightmareStartBonus;
        int nightmareSeed = unchecked(currentSeed * 31 + 17);

        boss.Initialize(grid, activePlayers.ToArray(), nightmareSeed, nightmareStartSpeed);
    }

    // Starts a fresh match at Round 1 and saves the seed to use next time.
    public void Play()
    {
        if (state == GameState.Playing || state == GameState.RoundTransition)
            return;

        if (!recordsLoaded)
        {
            recordsLoaded = saves.Load(out saveError);

            if (!recordsLoaded)
            {
                ShowError(saveError);
                return;
            }
        }

        currentSeed = saves.Data.nextBossSeed;

        if (!PrepareActors(currentSeed))
            return;

        saves.Data.nextBossSeed =
            currentSeed == int.MaxValue ? 1 : currentSeed + 1;

        if (!saves.Save(out saveError))
        {
            saves.Data.nextBossSeed = currentSeed;
            ShowError(saveError);
            return;
        }

        runSpeedInterval = Math.Max(1.0, speedIncreaseInterval);
        runSpeedIncrement = Mathf.Max(0.1f, speedIncrement);
        runNightmareStartBonus = Mathf.Max(0f, nightmareStartSpeedBonus);
        runPenalty = Math.Max(0, seenPenaltyPerSecond);

        totalElapsed = 0;
        seenSeconds = 0;

        BeginRoundConfig(1, Math.Max(0, round1SpeedUps));

        previousPlayers = new Vector3[activePlayers.Count];
        state = GameState.Playing;

        ui.ShowPlaying();
        ui.HideAnnouncement();
        UpdateHud();
    }

    private void BeginRoundConfig(int roundNumber, int maxSpeedUps)
    {
        currentRound = roundNumber;
        roundElapsed = 0;
        speedUpsTriggered = 0;
        currentRoundMaxSpeedUps = maxSpeedUps;
        currentRoundDuration = (maxSpeedUps + 1) * runSpeedInterval;
    }

    // Handles menu pairing, round transitions, or active gameplay ticks.
    private void Update()
    {
        if (state == GameState.Menu)
        {
            PollWaitingGamepadSlots();
            return;
        }

        if (state == GameState.RoundTransition)
        {
            TickRoundTransition(Time.deltaTime);
            return;
        }

        if (state != GameState.Playing)
            return;

        double dt = Math.Min(
            Time.deltaTime,
            currentRoundDuration - roundElapsed
        );

        if (dt <= 0)
            return;

        bool wasSeen = boss.CanSeeAnyPlayer();
        Vector3 previousBoss = boss.transform.position;

        for (int i = 0; i < activePlayers.Count; i++)
        {
            previousPlayers[i] = activePlayers[i].transform.position;
            activePlayers[i].Tick((float)dt);
        }

        boss.Tick((float)dt);

        double catchTime = FindCatch(previousBoss);
        double fraction = catchTime < 0 ? 1.0 : catchTime;
        double stepSeconds = dt * fraction;

        roundElapsed = Math.Min(
            currentRoundDuration,
            roundElapsed + stepSeconds
        );

        totalElapsed += stepSeconds;

        if (wasSeen)
        {
            seenSeconds = Math.Min(
                totalElapsed,
                seenSeconds + stepSeconds
            );
        }

        if (catchTime >= 0)
        {
            boss.transform.position = Vector3.Lerp(
                previousBoss,
                boss.transform.position,
                (float)catchTime
            );

            for (int i = 0; i < activePlayers.Count; i++)
            {
                activePlayers[i].transform.position = Vector3.Lerp(
                    previousPlayers[i],
                    activePlayers[i].transform.position,
                    (float)catchTime
                );
            }

            EndRound(false);
            return;
        }

        if (roundElapsed >= currentRoundDuration)
        {
            if (currentRound == 1)
            {
                StartNightmareTransition();
            }
            else
            {
                EndRound(true);
            }

            return;
        }

        CheckSpeedEscalation();
        UpdateHud();
    }

    // Increases the boss's speed by speedIncrement every 14 seconds and shows the top banner.
    private void CheckSpeedEscalation()
    {
        int expectedSpeedUps = Math.Min(
            currentRoundMaxSpeedUps,
            (int)Math.Floor((roundElapsed + 1e-6) / runSpeedInterval)
        );

        while (speedUpsTriggered < expectedSpeedUps)
        {
            speedUpsTriggered++;
            boss.SetSpeed(boss.CurrentSpeed + runSpeedIncrement);

            ui.ShowAnnouncement(
                "The Boss is speeding up!",
                GameUI.SpeedUpBannerColor,
                speedUpBannerDuration
            );
        }
    }

    // Freezes gameplay briefly to show "Round Complete!" before starting Nightmare Round.
    private void StartNightmareTransition()
    {
        state = GameState.RoundTransition;
        transitionTimer = 0;
        nightmarePrepared = false;

        ui.ShowAnnouncement(
            "Round Complete!",
            GameUI.RoundCompleteBannerColor,
            0f
        );

        UpdateHud();
    }

    private void TickRoundTransition(float deltaTime)
    {
        transitionTimer += deltaTime;

        if (!nightmarePrepared && transitionTimer >= roundCompletePause)
        {
            nightmarePrepared = true;
            PrepareNightmareActors();
            BeginRoundConfig(2, Math.Max(0, nightmareSpeedUps));

            ui.ShowAnnouncement(
                "Nightmare Round!",
                GameUI.NightmareBannerColor,
                0f
            );

            UpdateHud();
        }

        if (transitionTimer >= roundCompletePause + nightmareIntroPause)
        {
            ui.HideAnnouncement();
            state = GameState.Playing;
            UpdateHud();
        }
    }

    // Handles remote XInputController connect/disconnect events.
    private void OnInputDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected)
        {
            bool changed = false;

            for (int i = 0; i < MaxPlayers; i++)
            {
                if (slots[i].mode == PlayerControlMode.RemoteGamepad &&
                    slots[i].gamepadDeviceId == device.deviceId)
                {
                    slots[i].Clear();
                    changed = true;
                }
            }

            if (changed && ui != null && state == GameState.Menu)
            {
                ui.UpdatePlayerSlots(slots);
            }

            return;
        }

        if (state != GameState.Menu)
            return;

        if (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected)
        {
            if (RemoteGamepadUtility.IsRemoteGamepad(device, out Gamepad gamepad))
            {
                TryBindGamepadToWaitingSlot(gamepad);
            }
        }
    }

    // Allows already-connected virtual XInputController slots to bind on first button/stick input.
    private void PollWaitingGamepadSlots()
    {
        if (FindFirstWaitingSlot() < 0)
            return;

        var gamepads = Gamepad.all;

        for (int i = 0; i < gamepads.Count; i++)
        {
            Gamepad gamepad = gamepads[i];

            if (!RemoteGamepadUtility.IsRemoteGamepad(gamepad, out _))
                continue;

            if (IsGamepadAssigned(gamepad.deviceId))
                continue;

            if (RemoteGamepadUtility.HasPairingInput(gamepad))
            {
                TryBindGamepadToWaitingSlot(gamepad);

                if (FindFirstWaitingSlot() < 0)
                    return;
            }
        }
    }

    private void TryBindGamepadToWaitingSlot(Gamepad gamepad)
    {
        if (gamepad == null || IsGamepadAssigned(gamepad.deviceId))
            return;

        int waitingSlot = FindFirstWaitingSlot();

        if (waitingSlot < 0)
            return;

        slots[waitingSlot].mode = PlayerControlMode.RemoteGamepad;
        slots[waitingSlot].gamepadDeviceId = gamepad.deviceId;
        slots[waitingSlot].deviceLabel = gamepad.name;

        if (ui != null)
        {
            ui.UpdatePlayerSlots(slots);
            ui.SetStatus("");
        }
    }

    private int FindFirstWaitingSlot()
    {
        for (int i = 0; i < MaxPlayers; i++)
        {
            if (slots[i].mode == PlayerControlMode.WaitingForGamepad)
                return i;
        }

        return -1;
    }

    private bool IsGamepadAssigned(int deviceId)
    {
        for (int i = 0; i < MaxPlayers; i++)
        {
            if (slots[i].mode == PlayerControlMode.RemoteGamepad &&
                slots[i].gamepadDeviceId == deviceId)
            {
                return true;
            }
        }

        return false;
    }

    private void ClearModeFromAllSlots(PlayerControlMode mode)
    {
        for (int i = 0; i < MaxPlayers; i++)
        {
            if (slots[i].mode == mode)
            {
                slots[i].Clear();
            }
        }
    }

    private static bool IsValidSlot(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < MaxPlayers;
    }

    // Checks whether an active player touched or crossed the boss during this frame.
    private double FindCatch(Vector3 previousBoss)
    {
        double earliest = -1;
        double radius = catchDistanceInCells * grid.cellSize;

        for (int i = 0; i < activePlayers.Count; i++)
        {
            Vector3 before =
                previousPlayers[i] - previousBoss;

            Vector3 after =
                activePlayers[i].transform.position - boss.transform.position;

            double time = RoundRules.CatchFraction(
                before.x,
                before.z,
                after.x,
                after.z,
                radius
            );

            if (time >= 0 && (earliest < 0 || time < earliest))
            {
                earliest = time;
            }
        }

        return earliest;
    }

    // Updates the high-score record, saves the completed match, and shows its result.
    private void EndRound(bool won)
    {
        if (state != GameState.Playing)
            return;

        state = won
            ? GameState.Victory
            : GameState.Defeat;

        int finalScore = Score;

        if (finalScore >= saves.Data.highScore)
        {
            saves.Data.highScore = finalScore;
            saves.Data.highScoreRoundElapsedSeconds = totalElapsed;
        }

        if (saves.Data.totalRoundsPlayed < int.MaxValue)
        {
            saves.Data.totalRoundsPlayed++;
        }

        bool saved = saves.Save(out saveError);

        ui.ShowResult(
            won,
            finalScore,
            saves.Data.highScore,
            totalElapsed,
            RoundRules.WholeSeconds(seenSeconds)
        );

        if (!saved)
        {
            ShowError(saveError);
        }
    }

    // Displays the menu without starting a timer or consuming a seed.
    public void ShowMenu()
    {
        if (state == GameState.Playing || state == GameState.RoundTransition)
            return;

        state = GameState.Menu;

        for (int i = 0; i < MaxPlayers; i++)
        {
            if (slots[i].mode == PlayerControlMode.WaitingForGamepad)
            {
                slots[i].Clear();
            }
            else if (slots[i].mode == PlayerControlMode.RemoteGamepad &&
                     InputSystem.GetDeviceById(slots[i].gamepadDeviceId) as Gamepad == null)
            {
                slots[i].Clear();
            }
        }

        ui.ShowMenu(
            saves.Data.highScore,
            "Choose controls for each player, then hide from the boss!",
            slots
        );

        if (!string.IsNullOrEmpty(saveError))
        {
            ShowError(saveError);
        }
    }

    // Refreshes the round name, remaining time, boss speed, score, and visibility display.
    private void UpdateHud()
    {
        ui.UpdateHud(
            CurrentRoundName,
            Math.Max(0, currentRoundDuration - roundElapsed),
            boss.CurrentSpeed,
            Score,
            boss.CanSeeAnyPlayer()
        );
    }

    // Shows a useful error both on the overlay and in the Console.
    private void ShowError(string message)
    {
        ui.SetStatus(message);
        Debug.LogError(message, this);
    }
}