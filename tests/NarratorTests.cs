using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Tests
{
    public class NarratorTests
    {
        public class GreetingsTests
        {
            readonly Narrator narrator = new Narrator(new Speech
            {
                StartGame = new[] { "Hello {0}!" },
            });

            [Theory]
            [InlineData("John Doe")]
            [InlineData("Bob")]
            public void ShouldUseUserName(string userName)
            {
                // Act
                var result = narrator.Greetings(userName);

                // Assert
                Assert.NotEmpty(result);
                Assert.Contains(userName, result);
            }
        }

        public class AskQuestionTests
        {
            const string AskQuestionTemplate = "question: {0}, userName: {1}, question number: {2}, question sum: {3}, earned money: {4}";

            readonly Narrator narrator = new Narrator(new Speech
            {
                FirstQuestion = new[] { $"First question. {AskQuestionTemplate}" },
                AskQuestion = new[] { $"Not first question. {AskQuestionTemplate}" }
            });

            public static IEnumerable<object[]> ShouldBeNonEmptyParams =>
                from level in Utils.Levels
                from rightVariant in Utils.Variants
                select new object[] { level, rightVariant };

            [Theory]
            [MemberData(nameof(ShouldBeNonEmptyParams))]
            public void ShouldFillPlaceholders(byte level, char rightVariant)
            {
                // Arrange
                var userName = "Bob";
                var questionText = "question text";
                var a = "answer a";
                var b = "answer b";
                var c = "answer c";
                var d = "answer d";
                var question = new Question
                {
                    Text = questionText,
                    A = a,
                    B = b,
                    C = c,
                    D = d,
                    RightVariant = rightVariant
                };

                // Act
                var result = narrator.AskQuestionSpeech(userName, level, question);

                // Assert
                Assert.NotEmpty(result);
                Assert.Contains(userName, result);
                Assert.Contains(questionText, result);
                Assert.Contains(a, result);
                Assert.Contains(b, result);
                Assert.Contains(c, result);
                Assert.Contains(d, result);
                Assert.Contains((level + 1).ToString(), result);
                Assert.Matches(new Regex(@"question sum: \d+"), result);
                Assert.Matches(new Regex(@"earned money: \d+"), result);
            }

            [Fact]
            public void ShouldUserFirstQuestion()
            {
                // Act
                var result = narrator.AskQuestionSpeech("", 0, new Question());

                // Assert
                Assert.StartsWith("First question", result);
            }

            public static IEnumerable<object[]> ShouldUserNotFirstQuestionParams =>
                Utils.Levels.Skip(1).Select(n => new object[] { n });

            [Theory]
            [MemberData(nameof(ShouldUserNotFirstQuestionParams))]
            public void ShouldUserNotFirstQuestion(byte level)
            {
                // Act
                var result = narrator.AskQuestionSpeech("", level, new Question());

                // Assert
                Assert.StartsWith("Not first question", result);
            }
        }

        public class ReplyToWrongAnswerTests
        {
            readonly Narrator narrator = new Narrator(new Speech
            {
                WrongAnswer = new[] { "right variant: {0}, right answer: {1}" },
            });

            public static IEnumerable<object[]> ShouldFillPlaceholdersWhenNoEarningsParams =>
               Utils.Levels.Take(5).Select(n => new object[] { n });

            [Theory]
            [MemberData(nameof(ShouldFillPlaceholdersWhenNoEarningsParams))]
            public void ShouldFillPlaceholdersWhenNoEarnings(byte level)
            {
                // Arrange
                var a = "answer A";
                var rightVariant = 'A';
                var question = new Question
                {
                    A = a,
                    RightVariant = rightVariant,
                };

                // Act
                var result = narrator.ReplyToWrongAnswer(level, question);

                // Assert
                Assert.Equal($"right variant: {rightVariant}, right answer: {a}", result);
            }

            public static IEnumerable<object[]> ShouldFillPlaceholdersWhenHasEarningsParams =>
               Utils.Levels.Skip(5).Select(n => new object[] { n });

            [Theory]
            [MemberData(nameof(ShouldFillPlaceholdersWhenHasEarningsParams))]
            public void ShouldFillPlaceholdersWhenHasEarnings(byte level)
            {
                // Arrange
                var a = "answer A";
                var rightVariant = 'A';
                var question = new Question
                {
                    A = a,
                    RightVariant = rightVariant,
                };

                // Act
                var result = narrator.ReplyToWrongAnswer(level, question);

                // Assert
                Assert.Matches(new Regex(@$"right variant: {rightVariant}, right answer: {a}\nНо вы заработали \d+ рублей, поздравляю!"), result);
            }
        }
    }
}
