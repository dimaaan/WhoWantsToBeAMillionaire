using System;
using System.Collections.Generic;
using System.Text.Json;

/* 
 * Supress naming style warnings.
 * Due to System.Text.Json can't map names with underscores and
 * i don't want to add attributes this is most clean arrpoach
 */
#pragma warning disable IDE1006

public class BotApiDto
{
    static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
    {
        IgnoreNullValues = true
    };

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, GetType(), JsonOpts);
    }
}

public class BotApiEmptyResponse : BotApiDto
{
    public bool ok { get; set; }
    public int error_code { get; set; }
    public string? description { get; set; }
}

public class BotApiResponse<T> : BotApiEmptyResponse
{
    public T result { get; set; } = default!;
}

public class BotApiException : Exception
{
    public int Code { get; }

    public BotApiException(string description, int code = 0) : base(message: description)
    {
        Code = code;
    }
}

public class WebhookInfo : BotApiDto
{
    public string url { get; set; } = default!;
    public bool has_custom_certificate { get; set; }
    public int pending_update_count { get; set; }
    public long? last_error_date { get; set; }
    public string? last_error_message { get; set; }
    public int? max_connections { get; set; }
    public string[]? allowed_updates { get; set; }
}

public class UpdateParams : BotApiDto
{
    public int? offset { get; set; }
    public int? limit { get; set; }
    public int? timeout { get; set; }
    public string[]? allowed_updates { get; set; }
}

public class Update : BotApiDto
{
    public int update_id { get; set; }
    public Message? message { get; set; }
}

public class SendMessageParams : BotApiDto
{
    public long chat_id { get; set; }
    public string text { get; set; } = default!;
    public string? parse_mode { get; set; }
    public bool? disable_notification { get; set; }
    public ReplyKeyboardMarkup? reply_markup { get; set; }
}

public class Message : BotApiDto
{
    public int message_id { get; set; }
    public User from { get; set; } = default!;
    public int date { get; set; }
    public Chat chat { get; set; } = default!;
    public string? text { get; set; }
}

public class User : BotApiDto
{
    public int id { get; set; }
    public bool is_bot { get; set; }
    public string first_name { get; set; } = default!;
    public string? last_name { get; set; }
    public string? username { get; set; }
    public string? language_code { get; set; }
}

public class Chat : BotApiDto
{
    public long id { get; set; }
    public string type { get; set; } = default!;
}

public class ReplyKeyboardMarkup : BotApiDto
{
    public IEnumerable<IEnumerable<KeyboardButton>> keyboard { get; set; } = default!;
    public bool? one_time_keyboard { get; set; }
}

public class KeyboardButton : BotApiDto
{
    public string text { get; set; } = default!;
}
