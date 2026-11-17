using System;
using System.IO;
using UnityEngine;

[Serializable]
public class GameSaveData
{
    public string format;
    public int gameVersion;
    public int highScore;
    public int totalRoundsPlayed;
    public int nextBossSeed;
    public double highScoreRoundElapsedSeconds;
}

public sealed class GameSaveStore
{
    private const string Format = "GridPartyRecords";
    private const int CurrentGameVersion = 1;

    public string SavePath { get; }
    public GameSaveData Data { get; private set; }

    // These fields allow existing saves to load after the renaming.
    [Serializable]
    private class LegacySaveFields
    {
        public int version;
        public int roundsPlayed;
    }

    // Sets the automatic save location and first-run defaults.
    public GameSaveStore(string folder)
    {
        SavePath = Path.Combine(folder, "saveData.json");

        Data = new GameSaveData
        {
            format = Format,
            gameVersion = CurrentGameVersion,
            nextBossSeed = 1
        };
    }

    // Loads records and converts the previous field names when necessary.
    public bool Load(out string error)
    {
        try
        {
            if (File.Exists(SavePath))
            {
                string json = File.ReadAllText(SavePath);
                GameSaveData loaded =
                    JsonUtility.FromJson<GameSaveData>(json);

                if (loaded != null && loaded.gameVersion == 0)
                {
                    LegacySaveFields legacy =
                        JsonUtility.FromJson<LegacySaveFields>(json);

                    if (legacy != null && legacy.version == 1)
                    {
                        loaded.gameVersion = CurrentGameVersion;
                        loaded.totalRoundsPlayed = legacy.roundsPlayed;

                        // The old time described the last round,
                        // so its connection to the high score is unknown.
                        loaded.highScoreRoundElapsedSeconds = 0;
                    }
                }

                Validate(loaded);
                Data = loaded;
            }

            error = "";
            return true;
        }
        catch (Exception exception)
        {
            error =
                "Could not load records. " +
                "Check the Console for the save path. " +
                exception.Message;

            return false;
        }
    }

    // Writes complete JSON before replacing the save, keeping a previous backup.
    public bool Save(out string error)
    {
        string temporaryPath = SavePath + ".tmp";

        try
        {
            Validate(Data);

            Directory.CreateDirectory(
                Path.GetDirectoryName(SavePath)
            );

            File.WriteAllText(
                temporaryPath,
                JsonUtility.ToJson(Data, true)
            );

            if (File.Exists(SavePath))
            {
                File.Replace(
                    temporaryPath,
                    SavePath,
                    SavePath + ".bak"
                );
            }
            else
            {
                File.Move(temporaryPath, SavePath);
            }

            error = "";
            return true;
        }
        catch (Exception exception)
        {
            // Keep unsaved records in memory for the next save attempt.
            error =
                "Records are only kept for this session; saving failed. " +
                exception.Message;

            return false;
        }
    }

    // Rejects unsupported or invalid records before using or saving them.
    private static void Validate(GameSaveData data)
    {
        if (data == null ||
            data.format != Format ||
            data.gameVersion != CurrentGameVersion ||
            data.highScore < 0 ||
            data.totalRoundsPlayed < 0 ||
            data.nextBossSeed < 1 ||
            data.highScoreRoundElapsedSeconds < 0 ||
            double.IsNaN(data.highScoreRoundElapsedSeconds) ||
            double.IsInfinity(data.highScoreRoundElapsedSeconds))
        {
            throw new InvalidDataException(
                "Invalid or unsupported save data."
            );
        }
    }
}