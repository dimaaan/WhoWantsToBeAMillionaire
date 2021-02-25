using System;

namespace BotApi
{
    /// <summary>
    /// Telegram Bot Api response error
    /// </summary>
    class BotApiException : Exception
    {
        public int Code { get; }

        public BotApiException(string description, int code)
            : base(description)
        {
            Code = code;
        }
    }

    class ForbiddenException : BotApiException
    {
        public ForbiddenException(string description, int code)
            : base(description, code)
        {
        }
    }

    class TooManyRequestsException : BotApiException
    {
        public TimeSpan? RetryAfter { get; }

        public TooManyRequestsException(string description, int code, TimeSpan? retryAfter)
            : base(description, code)
        {
            RetryAfter = retryAfter;
        }
    }
}
