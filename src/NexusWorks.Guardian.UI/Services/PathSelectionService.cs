namespace NexusWorks.Guardian.UI.Services;

public interface IPathSelectionService
{
    Task<string?> PickBaselineFileAsync(CancellationToken cancellationToken = default);

    Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken = default);
}
