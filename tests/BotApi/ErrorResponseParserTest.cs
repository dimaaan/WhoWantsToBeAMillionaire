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

        [Fact]
        public void ShouldReturnTooManyRequestsError()
        {
            // Arrange
            var response = new BotApiEmptyResponse
            {
                ok = false,
                error_code = 429,
                description = "Too Many Requests: retry after 10"
            };

            // Act
            var result = response.ToException();

            // Assert
            var e = Assert.IsType<TooManyRequestsException>(result);
            Assert.Equal(e.RetryAfter, TimeSpan.FromSeconds(10));
        }
    }
}
