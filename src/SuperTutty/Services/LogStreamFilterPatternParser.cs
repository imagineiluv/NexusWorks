using System;
using System.Collections.Generic;
using System.Linq;

namespace SuperTutty.Services
{
    public static class LogStreamFilterPatternParser
    {
        /// <summary>
        /// Splits a user-provided filter string into multiple patterns.
        /// Delimiters: newline and ';'.
        /// </summary>
        public static IReadOnlyList<string> Split(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return Array.Empty<string>();
            }

            return raw
                .Split(new[] { "\r\n", "\n", ";" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => (p ?? string.Empty).Trim())
                .Where(p => p.Length > 0)
                .ToArray();
        }
    }
}
