using Microsoft.Data.Sqlite;
using System;

public interface ISessions
{
    States.State? Get(long chatId);

    void Update(long chatId, States.State state);

    void Remove(long chatId);

    long Count();
}

public class Sessions : ISessions
{
    readonly SqliteOptions Options;

    public Sessions(SqliteOptions options)
    {
        Options = options;
    }

    public States.State? Get(long chatId)
    {
        using var connection = new SqliteConnection(Options.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT state, level, question, usedHints, removed1, removed2, firstAnswer FROM Sessions WHERE chat = $chat";
        command.Parameters.AddWithValue("$chat", chatId);
        var reader = command.ExecuteReader();

        if (!reader.Read())
            return null;

        var stateId = reader.GetByte(0);

        return stateId switch
        {
            0 => ReadPlayingState(reader),
            1 => ReadWaitingTwoAnswersState(reader),
            2 => new States.Over(),
            _ => throw new Exception($"Unknown state {stateId}. Expected: 0, 1, 2")
        };

        static States.Playing ReadPlayingState(SqliteDataReader reader) =>
            new States.Playing(
                level: reader.GetByte(1),
                question: reader.GetInt16(2),
                usedHints: (States.Playing.Hints)reader.GetByte(3),
                removed1: reader.IsDBNull(4) ? null : reader.GetChar(4),
                removed2: reader.IsDBNull(5) ? null : reader.GetChar(5)
            );

        static States.WaitingTwoAnswers ReadWaitingTwoAnswersState(SqliteDataReader reader) =>
            new States.WaitingTwoAnswers(
                p: ReadPlayingState(reader),
                firstAnswer: reader.GetChar(6)
            );
    }

    public void Update(long chatId, States.State state)
    {
        using var connection = new SqliteConnection(Options.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"REPLACE INTO Sessions (chat, state, level, question, usedHints, removed1, removed2, firstAnswer) VALUES ($chat, $state, $level, $question, $usedHints, $removed1, $removed2, $firstAnswer)";

        object stateId, level, question, usedHints, removed1, removed2, firstAnswer;

        switch (state)
        {
            case States.WaitingTwoAnswers w:
                stateId = 1;
                level = w.Level;
                question = w.Question;
                usedHints = w.UsedHints;
                removed1 = w.Removed1 ?? (object)DBNull.Value;
                removed2 = w.Removed2 ?? (object)DBNull.Value;
                firstAnswer = w.FirstAnswer;
                break;
            case States.Playing p:
                stateId = 0;
                level = p.Level;
                question = p.Question;
                usedHints = p.UsedHints;
                removed1 = p.Removed1 ?? (object)DBNull.Value;
                removed2 = p.Removed2 ?? (object)DBNull.Value;
                firstAnswer = DBNull.Value;
                break;
            case States.Over:
                stateId = 2;
                level = DBNull.Value;
                question = DBNull.Value;
                usedHints = DBNull.Value;
                removed1 = DBNull.Value;
                removed2 = DBNull.Value;
                firstAnswer = DBNull.Value;
                break;
            default:
                throw new Exception($"Unknown state {state}");
        }

        command.Parameters.AddWithValue("$chat", chatId);
        command.Parameters.AddWithValue("$state", stateId);
        command.Parameters.AddWithValue("$level", level);
        command.Parameters.AddWithValue("$question", question);
        command.Parameters.AddWithValue("$usedHints", usedHints);
        command.Parameters.AddWithValue("$removed1", removed1);
        command.Parameters.AddWithValue("$removed2", removed2);
        command.Parameters.AddWithValue("$firstAnswer", firstAnswer);
        command.ExecuteNonQuery();
    }

    public void Remove(long chatId)
    {
        using var connection = new SqliteConnection(Options.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"DELETE FROM Sessions WHERE chat = $chat";
        command.Parameters.AddWithValue("$chat", chatId);
        command.ExecuteNonQuery();
    }

    public long Count()
    {
        using var connection = new SqliteConnection(Options.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT COUNT(*) FROM Sessions";
        return (long)command.ExecuteScalar();
    }
}
