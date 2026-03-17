using Microsoft.UI.Xaml;
using NexusWorks.Guardian.UI.Services;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace NexusWorks.Guardian.UI.Platforms.Windows;

internal sealed class WindowsPathSelectionService : IPathSelectionService
{
    public async Task<string?> PickBaselineFileAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, GetWindowHandle());

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    public async Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            CommitButtonText = string.IsNullOrWhiteSpace(title) ? "Select" : title,
        };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, GetWindowHandle());

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    private static IntPtr GetWindowHandle()
    {
        var window = Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as Window;
        if (window is null)
        {
            throw new InvalidOperationException("No active window was found for the picker.");
        }

        return WindowNative.GetWindowHandle(window);
    }
}
