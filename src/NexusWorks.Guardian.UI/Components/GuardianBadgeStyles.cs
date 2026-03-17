using NexusWorks.Guardian.Models;

namespace NexusWorks.Guardian.UI.Components;

/// <summary>
/// Centralizes Tailwind CSS badge class tokens used across the Guardian UI.
/// Maps domain statuses and severity levels to consistent visual tokens.
/// </summary>
internal static class GuardianBadgeStyles
{
    // ── Semantic Tokens ──

    public const string Ok = "bg-emerald-100 text-emerald-700 ring-1 ring-inset ring-emerald-200";
    public const string Changed = "bg-amber-100 text-amber-700 ring-1 ring-inset ring-amber-200";
    public const string Added = "bg-blue-100 text-blue-700 ring-1 ring-inset ring-blue-200";
    public const string Removed = "bg-slate-200 text-slate-700 ring-1 ring-inset ring-slate-300";
    public const string MissingRequired = "bg-red-100 text-red-700 ring-1 ring-inset ring-red-200";
    public const string Error = "bg-fuchsia-100 text-fuchsia-700 ring-1 ring-inset ring-fuchsia-200";
    public const string Neutral = "bg-slate-100 text-slate-700 ring-1 ring-inset ring-slate-200";

    // ── Severity Tokens ──

    public const string Critical = "bg-red-100 text-red-700 ring-1 ring-inset ring-red-200";
    public const string High = "bg-amber-100 text-amber-700 ring-1 ring-inset ring-amber-200";
    public const string Medium = "bg-blue-100 text-blue-700 ring-1 ring-inset ring-blue-200";
    public const string Low = "bg-emerald-100 text-emerald-700 ring-1 ring-inset ring-emerald-200";

    // ── Status Mappers ──

    public static string ForStatus(CompareStatus status)
        => status switch
        {
            CompareStatus.Ok => Ok,
            CompareStatus.Changed => Changed,
            CompareStatus.Added => Added,
            CompareStatus.Removed => Removed,
            CompareStatus.MissingRequired => MissingRequired,
            CompareStatus.Error => Error,
            _ => Neutral,
        };

    public static string ForSeverity(Severity severity)
        => severity switch
        {
            Severity.Critical => Critical,
            Severity.High => High,
            Severity.Medium => Medium,
            _ => Low,
        };
}
