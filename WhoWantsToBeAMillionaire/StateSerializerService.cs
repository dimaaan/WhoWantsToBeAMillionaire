using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

class StateSerializerService
{
    readonly string StateFile;
    readonly ILogger<StateSerializerService> Logger;

    public StateSerializerService(string stateFile, ILogger<StateSerializerService> logger)
    {
        StateFile = stateFile;
        Logger = logger;
    }

    public IEnumerable<KeyValuePair<long, States.State>> Load() {
        if (!File.Exists(StateFile))
        {
            Logger.LogInformation("No state found");
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
                1 => new States.Playing
                {
                    Level = el.Value.GetProperty("level").GetByte(),
                    Question = el.Value.GetProperty("question").GetInt32()
                },
                2 => new States.Over(),
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
                case States.Playing p:
                    writer.WriteNumber("type", 1);
                    writer.WriteNumber("level", p.Level);
                    writer.WriteNumber("question", p.Question);
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
}