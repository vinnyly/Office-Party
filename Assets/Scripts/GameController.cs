using System;
using UnityEngine;

[DisallowMultipleComponent]
public class GameController : MonoBehaviour
{
    private enum GameState
    {
        Menu,
        Playing,
        Victory,
        Defeat
    }

    [SerializeField] private BoardGrid grid;
    [SerializeField] private BossMovement boss;
    [SerializeField] private PlayerMovement[] players;

    [SerializeField, Min(0.1f)]
    private float roundDuration = 10f;

    [SerializeField, Min(0)]
    private int seenPenaltyPerSecond = 1;

    [SerializeField, Range(0.05f, 0.49f)]
    private float catchDistanceInCells = 0.3f;

    [SerializeField] private GameState state;
    [SerializeField] private int currentSeed;

    private GameSaveStore saves;
    private GameUI ui;
    private bool recordsLoaded;

    private double elapsed;
    private double seenSeconds;
    private double runDuration;
    private int runPenalty;

    private Vector3[] previousPlayers;
    private string saveError = "";

    private int Score =>
        RoundRules.Score(elapsed, seenSeconds, runPenalty);

    // Loads records, builds the overlay, and waits at the start menu.
    private void Start()
    {
        state = GameState.Menu;

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
            PrepareActors(saves.Data.nextBossSeed);
        }
    }

    // Checks scene references and resets every actor for a new round.
    private bool PrepareActors(int seed)
    {
        if (grid == null ||
            boss == null ||
            players == null ||
            players.Length == 0)
        {
            ShowError("Assign Grid, Boss and Players on GameController.");
            return false;
        }

        if (grid.cellSize <= 0f || !boss.isActiveAndEnabled)
        {
            ShowError("Use a positive Cell Size and enable the boss.");
            return false;
        }

        foreach (PlayerMovement player in players)
        {
            if (player == null || !player.isActiveAndEnabled)
            {
                ShowError(
                    "Every Players slot needs an enabled PlayerMovement."
                );

                return false;
            }
        }

        foreach (PlayerMovement player in players)
        {
            if (!player.Initialize(grid))
            {
                ShowError("Check the player's Start Cell and speed.");
                return false;
            }
        }

        if (!boss.Initialize(grid, players, seed))
        {
            ShowError("Check the boss's Start Cell and speed.");
            return false;
        }

        return true;
    }

    // Starts a fresh round and saves the seed to use next time.
    public void Play()
    {
        if (state == GameState.Playing)
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

        elapsed = 0;
        seenSeconds = 0;

        runDuration = Math.Max(0.1, roundDuration);
        runPenalty = Math.Max(0, seenPenaltyPerSecond);

        previousPlayers = new Vector3[players.Length];

        state = GameState.Playing;

        ui.ShowPlaying();
        UpdateHud();
    }

    // Advances movement and timers only while a round is running.
    private void Update()
    {
        if (state != GameState.Playing)
            return;

        double dt = Math.Min(
            Time.deltaTime,
            runDuration - elapsed
        );

        if (dt <= 0)
            return;

        bool wasSeen = boss.CanSeeAnyPlayer();
        Vector3 previousBoss = boss.transform.position;

        for (int i = 0; i < players.Length; i++)
        {
            previousPlayers[i] = players[i].transform.position;
            players[i].Tick((float)dt);
        }

        boss.Tick((float)dt);

        double catchTime = FindCatch(previousBoss);
        double fraction = catchTime < 0 ? 1.0 : catchTime;

        elapsed = Math.Min(
            runDuration,
            elapsed + dt * fraction
        );

        if (wasSeen)
        {
            seenSeconds = Math.Min(
                elapsed,
                seenSeconds + dt * fraction
            );
        }

        if (catchTime >= 0)
        {
            boss.transform.position = Vector3.Lerp(
                previousBoss,
                boss.transform.position,
                (float)catchTime
            );

            for (int i = 0; i < players.Length; i++)
            {
                players[i].transform.position = Vector3.Lerp(
                    previousPlayers[i],
                    players[i].transform.position,
                    (float)catchTime
                );
            }

            EndRound(false);
        }
        else if (elapsed >= runDuration)
        {
            EndRound(true);
        }
        else
        {
            UpdateHud();
        }
    }

    // Checks whether a player touched or crossed the boss during this frame.
    private double FindCatch(Vector3 previousBoss)
    {
        double earliest = -1;
        double radius = catchDistanceInCells * grid.cellSize;

        for (int i = 0; i < players.Length; i++)
        {
            Vector3 before =
                previousPlayers[i] - previousBoss;

            Vector3 after =
                players[i].transform.position - boss.transform.position;

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

    // Updates the high-score record, saves the completed round, and shows its result.
    private void EndRound(bool won)
    {
        if (state != GameState.Playing)
            return;

        state = won
            ? GameState.Victory
            : GameState.Defeat;

        int finalScore = Score;

        // Keep the score and elapsed time from the same record-setting round.
        // A tie replaces the elapsed time with the latest matching round.
        if (finalScore >= saves.Data.highScore)
        {
            saves.Data.highScore = finalScore;
            saves.Data.highScoreRoundElapsedSeconds = elapsed;
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
            elapsed,
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
        if (state == GameState.Playing)
            return;

        state = GameState.Menu;

        ui.ShowMenu(
            saves.Data.highScore,
            "Hide from the boss!"
        );

        if (!string.IsNullOrEmpty(saveError))
        {
            ShowError(saveError);
        }
    }

    // Refreshes the remaining time, score and visibility display.
    private void UpdateHud()
    {
        ui.UpdateHud(
            Math.Max(0, runDuration - elapsed),
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