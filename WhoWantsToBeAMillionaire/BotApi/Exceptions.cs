using System;

namespace BotApi
{
    public class BotApiException : Exception
    {
        public int Code { get; }

        public BotApiException(string description, int code = 0) : base(message: description)
        {
            Code = code;
        }
    }
}
