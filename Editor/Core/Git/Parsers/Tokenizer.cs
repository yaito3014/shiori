using System;
using System.Collections.Generic;

namespace Shiori
{
    internal static class Tokenizer
    {
        /// <summary>Splits NUL-separated output. A trailing terminator does not produce an empty token.</summary>
        public static IReadOnlyList<string> SplitNul(string stdout)
        {
            if (string.IsNullOrEmpty(stdout)) return Array.Empty<string>();
            var parts = stdout.Split('\0');
            var count = parts.Length;
            if (count > 0 && parts[count - 1].Length == 0) count--;
            var list = new List<string>(count);
            for (var i = 0; i < count; i++) list.Add(parts[i]);
            return list;
        }
    }
}
