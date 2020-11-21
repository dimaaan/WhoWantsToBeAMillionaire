using System;
using System.Linq;

namespace Tests
{
    static class Utils
    {
        /// <summary>
        /// All possible question numbers
        /// </summary>
        public static readonly byte[] Levels = Enumerable.Range(0, 15).Select(n => (byte)n).ToArray();

        public static readonly char[] Variants = new[] { 'A', 'B', 'C', 'D' };

        public static Question CreateQuestion(
            char rightAVariant,
            string question = "Question",
            string a = "Answer A",
            string b = "Answer B",
            string c = "Answer C",
            string d = "Answer D")
        {
            return new Question
            {
                Text = question,
                A = a,
                B = b,
                C = c,
                D = d,
                RightVariant = rightAVariant
            };
        }
    }
}
