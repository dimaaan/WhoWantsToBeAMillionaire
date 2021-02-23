using BotApi;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Events
{
    /// <summary>
    /// Log game events for analytic purposes
    /// </summary>
    public class EventLogger
    {
        readonly ILogger<EventLogger> Logger;
        readonly SqliteOptions Options;

        public EventLogger(SqliteOptions options, ILogger<EventLogger> logger)
        {
            Options = options;
            Logger = logger;
        }

        public void StartGame(Message msg)
        {
            LogEvent(msg, started: true);
            LogUserInfo(msg.from);
        }

        public void Answer(Message msg, byte level, short question, char answer1, char answer2, bool right)
        {
            LogEvent(msg,
                level: level,
                question: question,
                answer: answer2 == default ? answer1.ToString() : $"{answer1}{answer2}",
                right: right
            );
        }

        public void Hint(Message msg, byte level, short question, string hint)
        {
            LogEvent(msg,
                level: level,
                question: question,
                hint: hint
            );
        }

        public IEnumerable<(DateTimeOffset Date, int GamesStarted, int GamesFinished)> GamesPerDayReport()
        {
            using var connection = new SqliteConnection(Options.ConnectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"SELECT
strftime('%Y', date, 'localtime') as Year,
strftime('%m', date, 'localtime') as Month,
strftime('%d', date, 'localtime') as Day,
COUNT(CASE WHEN started = 1 THEN 1 ELSE null END) AS GamesStarted,
COUNT(CASE WHEN right = 0 THEN 1 ELSE null END) AS GamesFinished
FROM Events
WHERE right = 0 OR started = 1
GROUP BY Year, Month, Day
ORDER BY date DESC";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var year = reader.GetInt32(0);
                var month = reader.GetInt32(1);
                var day = reader.GetInt32(2);
                var started = reader.GetInt32(3);
                var finished = reader.GetInt32(4);
                var date = new DateTimeOffset(year, month, day, 0, 0, 0, TimeZoneInfo.Local.BaseUtcOffset);

                yield return (date, started, finished);
            }
        }

        /// <summary>
        /// Logs event in fire-and-forget style
        /// </summary>
        void LogEvent(Message msg, byte? level = null, short? question = null, bool? started = null, string? answer = null, bool? right = null, string? hint = null)
        {
            Task.Run(() =>
            {
                try
                {
                    using var connection = new SqliteConnection(Options.ConnectionString);
                    connection.Open();

                    using var command = connection.CreateCommand();
                    command.CommandText = @"INSERT INTO Events (chat, date, level, question, started, answer, right, hint) VALUES ($chat, $date, $level, $question, $started, $answer, $right, $hint)";
                    command.Parameters.AddWithValue("$chat", msg.chat.id);
                    command.Parameters.AddWithValue("$date", DateTimeOffset.FromUnixTimeSeconds(msg.date).ToLocalTime());
                    command.Parameters.AddWithValue("$level", level ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("$question", question ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("$started", started ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("$answer", answer ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("$right", right ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("$hint", hint ?? (object)DBNull.Value);
                    command.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    Logger.LogWarning(e, "Failed to save event to Sqlite");
                }
            });
        }

        void LogUserInfo(User user)
        {
            Task.Run(() =>
            {
                try
                {
                    using var connection = new SqliteConnection(Options.ConnectionString);
                    connection.Open();

                    using var command = connection.CreateCommand();
                    command.CommandText = @"REPLACE INTO Users (id, is_bot, first_name, last_name, username, language_code) VALUES ($id, $is_bot, $first_name, $last_name, $username, $language_code)";
                    command.Parameters.AddWithValue("$id", user.id);
                    command.Parameters.AddWithValue("$is_bot", user.is_bot);
                    command.Parameters.AddWithValue("$first_name", user.first_name);
                    command.Parameters.AddWithValue("$last_name", user.last_name ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("$username", user.username ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("$language_code", user.language_code ?? (object)DBNull.Value);
                    command.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    Logger.LogWarning(e, "Failed to save user to Sqlite");
                }
            });
        }
    }
}