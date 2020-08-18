using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

public class StateSerializer
{
    readonly string StateFile;
    readonly ILogger<StateSerializer> Logger;

    public StateSerializer(string stateFile, ILogger<StateSerializer> logger)
    {
        StateFile = stateFile;
        Logger = logger;
    }

    public IEnumerable<KeyValuePair<long, States.State>> Load()
    {
        if (!File.Exists(StateFile))
        {
            Logger.LogInformation("State not found at {File}", StateFile);
            yield break;
        }

        Logger.LogInformation("Loading state from {File}...", StateFile);

        using var stateStream = File.OpenRead(StateFile);
        var counter = 0;
        foreach (var el in JsonDocument.ParseAsync(stateStream).Result.RootElement.EnumerateObject())
        {
            var id = long.Parse(el.Name);
            var type = el.Value.GetProperty("type").GetByte();
            States.State state = type switch
            {
                1 => LoadPlayingState(el.Value),
                2 => new States.Over(),
                3 => LoadWatingsTwoAnswersState(el.Value),
                _ => throw new Exception($"Unknown type {type}. id: {id}")
            };
            yield return new KeyValuePair<long, States.State>(id, state);
            counter++;
        }

        Logger.LogInformation("State loaded. Active games: {Count}", counter);
    }

    public void Save(IEnumerable<KeyValuePair<long, States.State>> games)
    {
        Logger.LogInformation("Saving state to {File}...", StateFile);

        using var stream = File.Create(StateFile);
        using var writer = new Utf8JsonWriter(stream);
        var counter = 0;

        writer.WriteStartObject();
        foreach (var game in games)
        {
            writer.WritePropertyName(game.Key.ToString());
            writer.WriteStartObject();
            switch (game.Value)
            {
                case States.WaitingTwoAnswers w:
                    writer.WriteNumber("type", 3);
                    WritePlayingState(writer, w);
                    writer.WriteString("firstAnswer", w.FirstAnswer.ToString());
                    break;
                case States.Playing p:
                    writer.WriteNumber("type", 1);
                    WritePlayingState(writer, p);
                    break;
                case States.Over _:
                    writer.WriteNumber("type", 2);
                    break;
                default:
                    throw new Exception($"Unknown type {game.Value.GetType().Name}");
            }
            writer.WriteEndObject();
            counter++;
        }
        writer.WriteEndObject();

        Logger.LogInformation("{Count} games saved", counter);
    }

    static void WritePlayingState(Utf8JsonWriter writer, States.Playing p)
    {
        writer.WriteNumber("level", p.Level);
        writer.WriteNumber("question", p.Question);
        writer.WriteNumber("hints", (byte)p.UsedHints);
        if (p.Removed1 != default)
            writer.WriteString("removed1", p.Removed1.ToString());
        if (p.Removed2 != default)
            writer.WriteString("removed2", p.Removed2.ToString());
    }

    static States.Playing LoadPlayingState(JsonElement el)
    {
        return new States.Playing(
            level: el.GetProperty("level").GetByte(),
            question: el.GetProperty("question").GetInt16(),
            usedHints: (States.Playing.Hints)el.GetProperty("hints").GetByte(),
            removed1: el.TryGetProperty("removed1", out var r1) ? r1.GetString()[0] : default,
            removed2: el.TryGetProperty("removed2", out var r2) ? r2.GetString()[0] : default
        );
    }

    States.WaitingTwoAnswers LoadWatingsTwoAnswersState(JsonElement el)
    {
        var p = LoadPlayingState(el);
        var firstAnswer = el.GetProperty("firstAnswer").GetString()[0];
        return new States.WaitingTwoAnswers(p, firstAnswer);
    }
}