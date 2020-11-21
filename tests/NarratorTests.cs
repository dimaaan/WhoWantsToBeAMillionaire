using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Tests
{
    public static class NarratorTests
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
            public void ShouldUseFirstQuestion()
            {
                // Act
                var result = narrator.AskQuestionSpeech("", 0, new Question());

                // Assert
                Assert.StartsWith("First question", result);
            }

            public static IEnumerable<object[]> ShouldUseNotFirstQuestionParams =>
                Utils.Levels.Skip(1).Select(n => new object[] { n });

            [Theory]
            [MemberData(nameof(ShouldUseNotFirstQuestionParams))]
            public void ShouldUseNotFirstQuestion(byte level)
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

        public class HelpTests
        {
            [Fact]
            public void ShouldReturnText()
            {
                // Act
                var helpText = Narrator.Help();

                // Assert
                Assert.NotEmpty(helpText);
            }
        }

        public class RightAnswerSpeechTests
        {
            readonly Narrator narrator = new Narrator(new Speech
            {
                EarnedCantFire = new[] { "EarnedCantFire,{0},{1},{2}" },
                RightAnswer = new[] { "RightAnswer,{0},{1},{2}" }
            });

            readonly Question TestQuestion = Utils.CreateQuestion('A');

            public static IEnumerable<object[]> ShouldReturnTextParams =>
               Utils.Levels.Select(n => new object[] { n });

            [Theory]
            [MemberData(nameof(ShouldReturnTextParams))]
            public void ShouldReturnText(byte level)
            {
                // Act
                var text = narrator.RightAnswerSpeech(level, TestQuestion);

                // Assert
                Assert.Matches(new Regex(@"^(EarnedCantFire|RightAnswer),A,Answer A,\d+$"), text);
            }

            [Theory]
            [InlineData(new object[] { 5 })]
            [InlineData(new object[] { 10 })]
            public void ShouldReturnEarnedCantFireTextOnQuestions5and10(byte level)
            {
                // Act
                var text = narrator.RightAnswerSpeech(level, TestQuestion);

                // Assert
                Assert.StartsWith("EarnedCantFire", text);
            }

            public static IEnumerable<object[]> ShouldReturnRightAnswerTextOnAllQuestionsExcept5and10Params =>
               Utils.Levels.Where(l => l != 5 && l != 10).Select(n => new object[] { n });

            [Theory]
            [MemberData(nameof(ShouldReturnRightAnswerTextOnAllQuestionsExcept5and10Params))]
            public void ShouldReturnRightAnswerTextOnAllQuestionsExcept5and10(byte level)
            {
                // Act
                var text = narrator.RightAnswerSpeech(level, TestQuestion);

                // Assert
                Assert.StartsWith("RightAnswer", text);
            }
        }

        public class FiftyFiftyTests
        {
            readonly Narrator narrator = new Narrator(new Speech
            {
                FiftyFifty = new[] { "FiftyFifty" }
            });

            public static IEnumerable<object[]> AllVariants =>
               Utils.Variants.Select(n => new object[] { n });

            [Theory]
            [MemberData(nameof(AllVariants))]
            public void ShouldReturnText(char rightVariant)
            {
                // Arrange
                var question = Utils.CreateQuestion(rightVariant);

                // Act
                var (text, _, _) = narrator.FiftyFifty(question);

                // Assert
                Assert.Contains("FiftyFifty", text);
            }

            [Theory]
            [MemberData(nameof(AllVariants))]
            public void ShouldNotRemoveRightVariant(char rightVariant)
            {
                // Arrange
                var question = Utils.CreateQuestion(rightVariant);

                // Act
                var (_, removed, removed2) = narrator.FiftyFifty(question);

                // Assert
                Assert.NotEqual(rightVariant, removed);
                Assert.NotEqual(rightVariant, removed2);
            }
        }

        public class PeopleHelpTests
        {
            readonly Narrator narrator = new Narrator(new Speech
            {
                PeopleHelp = new[] { "PeopleHelp,{0},{1}" }
            });

            public static IEnumerable<object[]> ShouldReturnTextParams =>
                from level in Utils.Levels
                from rightVariant in Utils.Variants
                from removed1 in Utils.Variants.Concat(new[] { default(char) })
                from removed2 in Utils.Variants.Concat(new[] { default(char) })
                where rightVariant != removed1 && rightVariant != removed2
                select new object[] { level, rightVariant, removed1, removed2 };

            [Theory]
            [MemberData(nameof(ShouldReturnTextParams))]
            public void ShouldReturnText(byte level, char rightVariant, char removed1, char removed2)
            {
                // Arrange
                var userName = "userName";
                var question = Utils.CreateQuestion(rightVariant);

                // Act
                var text = narrator.PeopleHelp(userName, level, question, removed1, removed2);

                // Assert
                Assert.Matches(new Regex(@"^PeopleHelp,userName,Question\nA \|-* \d+%\nB \|-* \d+%\nC \|-* \d+%\nD \|-* \d+%\n$"), text);
            }
        }

        public class CallFriendTests
        {
            readonly Narrator narrator = new Narrator(new Speech
            {
                CallFriend = new[] { new[] { "CallFriend", "{0}", "{1}", "{2}", "{3}", "{4}" } },
                FriendsNames = new[] { "Friend1" }
            });

            public static IEnumerable<object[]> ShouldReturnTextParams =>
                from level in Utils.Levels
                from rightVariant in Utils.Variants
                from removed1 in Utils.Variants.Concat(new[] { default(char) })
                from removed2 in Utils.Variants.Concat(new[] { default(char) })
                where rightVariant != removed1 && rightVariant != removed2
                select new object[] { level, rightVariant, removed1, removed2 };

            [Theory]
            [MemberData(nameof(ShouldReturnTextParams))]
            public void ShoudReturnText(byte level, char rightVariant, char removed1, char removed2)
            {
                // Arrange
                var userName = "userName";
                var question = Utils.CreateQuestion(rightVariant);

                // Act
                var text = narrator.CallFriend(userName, level, question, removed1, removed2);

                // Assert
                Assert.Matches(new Regex(@"^CallFriend\nuserName\nFriend1\nQuestion\n[A-D]\nAnswer [A-D]$"), text);
            }
        }

        public class TwoAnswersStep1Tests
        {
            readonly Narrator narrator = new Narrator(new Speech
            {
                TwoAnswersStep1 = new[] { "TwoAnswersStep1" },
            });

            [Fact]
            public void ShouldReturnText()
            {
                // Act
                var text = narrator.TwoAnswersStep1();

                // Assert
                Assert.NotEmpty(text);
            }
        }

        public class TwoAnswersStep2Tests
        {
            readonly Narrator narrator = new Narrator(new Speech
            {
                TwoAnswersStep2 = new[] { "TwoAnswersStep2" },
            });

            [Fact]
            public void ShouldReturnText()
            {
                // Act
                var text = narrator.TwoAnswersStep2();

                // Assert
                Assert.NotEmpty(text);
            }
        }

        public class NewQuestionTests
        {
            readonly Narrator narrator = new Narrator(new Speech
            {
                NewQuestion = new[] { "NewQuestion" },
            });

            [Fact]
            public void ShouldReturnText()
            {
                // Act
                var text = narrator.NewQuestion();

                // Assert
                Assert.NotEmpty(text);
            }
        }

        public class TryAgainSpeechTests
        {
            readonly Narrator narrator = new Narrator(new Speech
            {
                TryAgain = new[] { "TryAgain" },
            });

            [Fact]
            public void ShouldReturnText()
            {
                // Act
                var text = narrator.TryAgainSpeech();

                // Assert
                Assert.NotEmpty(text);
            }
        }

        public class WinSpeechTests
        {
            readonly Narrator narrator = new Narrator(new Speech
            {
                Win = new[] { "Win" },
            });

            [Fact]
            public void ShouldReturnText()
            {
                // Act
                var text = narrator.WinSpeech();

                // Assert
                Assert.NotEmpty(text);
            }
        }

        public class RequestLimitSpeechTests
        {
            readonly Narrator narrator = new Narrator(new Speech
            {
                RequestLimit = new[] { "RequestLimit" },
            });

            [Fact]
            public void ShouldReturnText()
            {
                // Arrange
                var questionText = "text";
                var delay = TimeSpan.FromSeconds(10);

                // Act
                var text = narrator.RequestLimitSpeech(questionText, delay);

                // Assert
                Assert.NotEmpty(text);
            }
        }
    }
}
