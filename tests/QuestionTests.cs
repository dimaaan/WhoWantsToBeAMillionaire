using System;
using Xunit;

namespace Tests
{
    public static class QuestionTests
    {
        public class AnswerOfTests
        {
            [Fact]
            public void ShouldReturnAnswerText()
            {
                // Arrange
                var question = Utils.CreateQuestion('A');

                // Act
                var answerA = question.AnswerOf('A');
                var answerB = question.AnswerOf('B');
                var answerC = question.AnswerOf('C');
                var answerD = question.AnswerOf('D');

                // Assert
                Assert.Equal(question.A, answerA);
                Assert.Equal(question.B, answerB);
                Assert.Equal(question.C, answerC);
                Assert.Equal(question.D, answerD);
            }

            [Fact]
            public void ShouldThrowOnIncorrectVariant()
            {
                // Arrange
                var question = Utils.CreateQuestion('A');

                // Assert
                Assert.Throws<ArgumentOutOfRangeException>(() => question.AnswerOf('Q'));
            }
        }
    }
}
