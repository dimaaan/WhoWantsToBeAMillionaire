using System.Collections.Generic;

/* 
 * Supress naming style warnings.
 * Due to System.Text.Json can't map names with underscores and
 * i don't want to add attributes this is most clean arrpoach
 */
#pragma warning disable IDE1006

namespace BotApi
{
    public record BotApiEmptyResponse(
        bool ok,
        int error_code,
        string? description
    );

    public record BotApiResponse<T>(
        bool ok,
        int error_code,
        string? description,
        T result
    ) : BotApiEmptyResponse(ok, error_code, description);

    public record WebhookInfo(
        string url,
        bool has_custom_certificate,
        int pending_update_count,
        long? last_error_date,
        string? last_error_message,
        int? max_connections,
        string[]? allowed_updates
    );

    public record UpdateParams(
        int? offset,
        int? limit,
        int? timeout,
        string[]? allowed_updates
    );

    public record Update(
        int update_id,
        Message? message
    );

    public record SendMessageParams(
        long chat_id,
        string text,
        string? parse_mode,
        bool? disable_notification,
        ReplyKeyboardMarkup? reply_markup
    );

    public record Message(
        int message_id,
        User from,
        int date,
        Chat chat,
        string? text
    );

    public record User(
        long id,
        bool is_bot,
        string first_name,
        string? last_name,
        string? username,
        string? language_code
    );

    public record Chat(
        long id,
        string type
    );

    public record ReplyKeyboardMarkup(
        IEnumerable<IEnumerable<KeyboardButton>> keyboard,
        bool? one_time_keyboard
    );

    public record KeyboardButton(
        string text
    );
}