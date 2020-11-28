using AutoFixture;
using AutoFixture.AutoMoq;
using Moq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Tests
{
    public static class GameTests
    {
        public class UpdateGameTests
        {
            [Fact]
            public async Task ShouldIgnoreMessageWithNoText()
            {
                // Arrange
                var fixture = new Fixture()
                    .Customize(new AutoMoqCustomization());

                fixture.Customize<BotApi.Message>(c => c.Without(m => m.text));
                var update = fixture.Create<BotApi.Update>();

                var client = fixture.Freeze<Mock<BotApi.IClient>>();
                client.Setup(c => c.SendMessageAsync(It.IsAny<BotApi.SendMessageParams>(), CancellationToken.None)).ReturnsAsync(new BotApi.Message());

                var game = fixture.Create<Game>();

                // Act
                await game.UpdateGame(update, CancellationToken.None);

                // Assert
                client.VerifyNoOtherCalls();
            }

            [Fact]
            public async Task ShouldIgnoreBots()
            {
                // Arrange
                var fixture = new Fixture()
                    .Customize(new AutoMoqCustomization());

                fixture.Customize<BotApi.User>(c => c.With(u => u.is_bot, true));
                var update = fixture.Create<BotApi.Update>();

                var client = fixture.Freeze<Mock<BotApi.IClient>>();
                client.Setup(c => c.SendMessageAsync(It.IsAny<BotApi.SendMessageParams>(), CancellationToken.None)).ReturnsAsync(new BotApi.Message());

                var game = fixture.Create<Game>();

                // Act
                await game.UpdateGame(update, CancellationToken.None);

                // Assert
                client.VerifyNoOtherCalls();
            }

            [Theory]
            [InlineData("/help")]
            [InlineData(" /help ")]
            [InlineData("/HELP")]
            [InlineData("/HeLp")]
            public async Task ShouldDisplayHelp(string command)
            {
                // Arrange
                var fixture = new Fixture()
                    .Customize(new AutoMoqCustomization());

                fixture.Customize<BotApi.User>(c => c.With(u => u.is_bot, false));
                fixture.Customize<BotApi.Message>(c => c.With(m => m.text, command));
                var update = fixture.Create<BotApi.Update>();

                var client = fixture.Freeze<Mock<BotApi.IClient>>();
                client.Setup(c => c.SendMessageAsync(It.Is<BotApi.SendMessageParams>(m => m.chat_id == update.message!.chat.id && m.text == Narrator.Help()), CancellationToken.None))
                    .ReturnsAsync(update.message!)
                    .Verifiable();

                var game = fixture.Create<Game>();

                // Act
                await game.UpdateGame(update, CancellationToken.None);

                // Assert
                client.VerifyAll();
                client.VerifyNoOtherCalls();
            }
        }
    }
}
