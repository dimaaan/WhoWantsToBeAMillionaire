using System;
using System.Text.RegularExpressions;

namespace BotApi
{
    static class ErrorResponseParser
    {
        public static BotApiException ToException(this BotApiEmptyResponse response)
        {
            var errMsg = !String.IsNullOrWhiteSpace(response.description)
               ? response.description
               : "No description provided";

            return response.error_code switch
            {
                429 => new BotApiTooManyRequestsException(
                    description: errMsg,
                    code: response.error_code,
                    retryAfter: TimeSpan.FromSeconds(int.Parse(TooManyRequestsPattern.Match(response.description).Groups[1].Value))
                ),
                _ => new BotApiException(
                    description: errMsg, 
                    code: response.error_code
                ),
            };
        }

        static readonly Regex TooManyRequestsPattern = new Regex(
            pattern: "Too Many Requests: retry after (\\d+)",
            options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
            matchTimeout: TimeSpan.FromSeconds(5)
        );
    }

    /// <summary>
    /// Telegram Bot Api response error
    /// </summary>
    class BotApiException: Exception
    {
        public int Code { get; }

        public BotApiException(string description, int code)
            : base(description)
        {
            Code = code;
        }
    }

    class BotApiTooManyRequestsException : BotApiException
    {
        public TimeSpan RetryAfter { get; }

        public BotApiTooManyRequestsException(string description, int code, TimeSpan retryAfter)
            : base(description, code)
        {
            RetryAfter = retryAfter;
        }
    }
}
