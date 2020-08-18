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
    }
}
