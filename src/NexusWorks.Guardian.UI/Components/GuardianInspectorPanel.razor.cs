using Microsoft.AspNetCore.Components;
using NexusWorks.Guardian.Models;

namespace NexusWorks.Guardian.UI.Components;

public partial class GuardianInspectorPanel
{
    [Parameter] public ComparisonItemResult? Item { get; set; }
    [Parameter] public string? CurrentArtifactPath { get; set; }
    [Parameter] public string? PatchArtifactPath { get; set; }
    [Parameter] public bool CanOpenCurrentArtifact { get; set; }
    [Parameter] public bool CanOpenPatchArtifact { get; set; }
    [Parameter] public EventCallback<string?> OnOpenPath { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }
    [Parameter] public bool ShowCloseButton { get; set; }
    [Parameter] public bool CompactMode { get; set; }
    [Parameter] public string PanelClass { get; set; } = "gw-panel flex min-h-0 flex-col overflow-hidden";

    private const string SectionSourceFiles = "source-files";
    private const string SectionRule = "rule";
    private const string SectionHash = "hash";
    private const string SectionMessages = "messages";
    private const string SectionJar = "jar";
    private const string SectionXml = "xml";
    private const string SectionYaml = "yaml";

    private readonly HashSet<string> _openSections = new(StringComparer.OrdinalIgnoreCase);
    private string? _lastSectionStateKey;

    protected override void OnParametersSet()
    {
        var sectionStateKey = $"{CompactMode}:{Item?.RelativePath}";
        if (string.Equals(sectionStateKey, _lastSectionStateKey, StringComparison.Ordinal))
        {
            return;
        }

        _lastSectionStateKey = sectionStateKey;
        ResetSectionState();
    }

    private Task OpenPathAsync(string? path)
        => OnOpenPath.HasDelegate ? OnOpenPath.InvokeAsync(path) : Task.CompletedTask;

    private Task CloseAsync()
        => OnClose.HasDelegate ? OnClose.InvokeAsync() : Task.CompletedTask;

    private void ToggleSection(string section)
    {
        if (!_openSections.Add(section))
        {
            _openSections.Remove(section);
        }
    }

    private bool IsSectionOpen(string section)
        => _openSections.Contains(section);

    private void ResetSectionState()
    {
        _openSections.Clear();

        _openSections.Add(SectionSourceFiles);
        _openSections.Add(SectionRule);
        _openSections.Add(SectionHash);
        _openSections.Add(SectionMessages);

        if (!CompactMode)
        {
            if (Item?.JarDetail is not null)
            {
                _openSections.Add(SectionJar);
            }

            if (Item?.XmlDetail is not null)
            {
                _openSections.Add(SectionXml);
            }

            if (Item?.YamlDetail is not null)
            {
                _openSections.Add(SectionYaml);
            }
        }
    }

    private static string GetStatusBadgeClass(CompareStatus status)
        => GuardianBadgeStyles.ForStatus(status);
}
