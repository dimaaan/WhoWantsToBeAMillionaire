using System;

namespace BotApi
{
    /// <summary>
    /// General Telegram Bot Api related exception. 
    /// Base class for other Telegram Bot Api exceptions
    /// </summary>
    class BotApiException : Exception
    {
        public BotApiException(string description)
            : base(message: description)
        {
        }
    }

    /// <summary>
    /// Telegram Bot Api response error
    /// </summary>
    class BotApiResponseException : BotApiException
    {
        public int Code { get; }

        public BotApiResponseException(string description, int code)
            : base(description)
        {
            Code = code;
        }
    }
}
