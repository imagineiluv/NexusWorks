using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

#if MACCATALYST
using Foundation;
using Microsoft.Maui.ApplicationModel;
using UIKit;
#endif

#if WINDOWS
using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;
using WinRT.Interop;
#endif

namespace NexusWorks.Guardian.UI.Services;

public interface IPathSelectionService
{
    Task<string?> PickBaselineFileAsync(CancellationToken cancellationToken = default);

    Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken = default);
}

public sealed class PlatformPathSelectionService : IPathSelectionService
{
#if MACCATALYST
    private const string MacDataDocumentType = "public.data";
    private const string MacFolderDocumentType = "public.folder";
    private readonly List<NSUrl> _securityScopedUrls = [];
#endif

    public Task<string?> PickBaselineFileAsync(CancellationToken cancellationToken = default)
    {
#if WINDOWS
        return PickWindowsFileAsync(cancellationToken);
#elif MACCATALYST
        return PickMacDocumentAsync([MacDataDocumentType], cancellationToken);
#else
        return PickDefaultFileAsync(cancellationToken);
#endif
    }

    public Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken = default)
    {
#if WINDOWS
        return PickWindowsFolderAsync(title, cancellationToken);
#elif MACCATALYST
        return PickMacDocumentAsync([MacFolderDocumentType], cancellationToken);
#else
        throw new NotSupportedException("Folder selection is not supported on this platform.");
#endif
    }

#if WINDOWS
    private static async Task<string?> PickWindowsFileAsync(CancellationToken cancellationToken)
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

    private static async Task<string?> PickWindowsFolderAsync(string title, CancellationToken cancellationToken)
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
#elif MACCATALYST
#pragma warning disable CA1422
    private Task<string?> PickMacDocumentAsync(string[] documentTypes, CancellationToken cancellationToken)
    {
        var taskCompletionSource = new TaskCompletionSource<string?>();
        var registration = cancellationToken.Register(() => taskCompletionSource.TrySetCanceled(cancellationToken));

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                var viewController = Platform.GetCurrentUIViewController();
                if (viewController is null)
                {
                    taskCompletionSource.TrySetException(new InvalidOperationException("No active view controller was found for the picker."));
                    return;
                }

                var controller = new UIDocumentPickerViewController(documentTypes, UIDocumentPickerMode.Open)
                {
                    AllowsMultipleSelection = false,
                    ModalPresentationStyle = UIModalPresentationStyle.FormSheet,
                };

                controller.Delegate = new DocumentPickerDelegate(
                    url => taskCompletionSource.TrySetResult(RegisterAccess(url)),
                    () => taskCompletionSource.TrySetResult(null));

                viewController.PresentViewController(controller, true, null);
            }
            catch (Exception ex)
            {
                taskCompletionSource.TrySetException(ex);
            }
        });

        return AwaitSelectionAsync(taskCompletionSource.Task, registration);
    }

    private string? RegisterAccess(NSUrl? url)
    {
        if (url is null)
        {
            return null;
        }

        if (url.StartAccessingSecurityScopedResource())
        {
            _securityScopedUrls.Add(url);
        }

        return url.Path;
    }

    private static async Task<string?> AwaitSelectionAsync(Task<string?> task, CancellationTokenRegistration registration)
    {
        try
        {
            return await task;
        }
        finally
        {
            registration.Dispose();
        }
    }

    private sealed class DocumentPickerDelegate : UIDocumentPickerDelegate
    {
        private readonly Action<NSUrl?> _onPicked;
        private readonly Action _onCancelled;

        public DocumentPickerDelegate(Action<NSUrl?> onPicked, Action onCancelled)
        {
            _onPicked = onPicked;
            _onCancelled = onCancelled;
        }

        public override void DidPickDocument(UIDocumentPickerViewController controller, NSUrl[] urls)
            => _onPicked(urls.FirstOrDefault());

        public override void WasCancelled(UIDocumentPickerViewController controller)
            => _onCancelled();
    }
#pragma warning restore CA1422
#else
    private static async Task<string?> PickDefaultFileAsync(CancellationToken cancellationToken)
    {
        var result = await FilePicker.Default.PickAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return result?.FullPath;
    }
#endif
}
