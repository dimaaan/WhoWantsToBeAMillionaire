using System;
using Xunit;

namespace BotApi
{
    public class ErrorResponseParserTest
    {
        static readonly BotApiEmptyResponse Response = new BotApiEmptyResponse
        {
            ok = false,
            description = "description",
            error_code = 1
        };

        [Fact]
        public void ShouldReturnErrorCode()
        {
            // Act
            var result = Response.ToException();

            // Assert
            Assert.Equal(1, result.Code);
        }

        [Fact]
        public void ShouldReturnDescriptionCode()
        {
            // Act
            var result = Response.ToException();

            // Assert
            Assert.Equal("description", result.Message);
        }
    }
}
