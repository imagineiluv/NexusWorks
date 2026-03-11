using System;

namespace SuperTutty.Services
{
    public enum LogStreamFilterKind
    {
        None,
        FixedString,
        Regex
    }

    /// <summary>
    /// Client-side filter options for log streaming ("grep-like").
    /// Filtering is applied on the client before events/analysis/persistence.
    /// </summary>
    public sealed class LogStreamFilterOptions
    {
        /// <summary>
        /// Include pattern(s). If empty/null, no include filtering is applied.
        /// Multiple patterns can be provided, separated by newline or ';'.
        /// </summary>
        public string? Include { get; set; }

        /// <summary>
        /// Exclude pattern(s). If empty/null, no exclude filtering is applied.
        /// Multiple patterns can be provided, separated by newline or ';'.
        /// </summary>
        public string? Exclude { get; set; }

        public LogStreamFilterKind Kind { get; set; } = LogStreamFilterKind.FixedString;

        /// <summary>
        /// Case-insensitive matching.
        /// </summary>
        public bool IgnoreCase { get; set; } = true;

        /// <summary>
        /// Invert include match (equivalent to grep -v for the include pattern).
        /// Only applies when <see cref="Include"/> is provided.
        /// </summary>
        public bool InvertMatch { get; set; }

        public bool HasAnyFilter()
        {
            return !string.IsNullOrWhiteSpace(Include) || !string.IsNullOrWhiteSpace(Exclude);
        }
    }
}
