using Foundation;
using Microsoft.Maui.ApplicationModel;
using NexusWorks.Guardian.UI.Services;
using UIKit;

namespace NexusWorks.Guardian.UI.Platforms.MacCatalyst;

#pragma warning disable CA1422
internal sealed class MacCatalystPathSelectionService : IPathSelectionService
{
    private const string MacDataDocumentType = "public.data";
    private const string MacFolderDocumentType = "public.folder";
    private readonly List<NSUrl> _securityScopedUrls = [];

    public Task<string?> PickBaselineFileAsync(CancellationToken cancellationToken = default)
        => PickMacDocumentAsync([MacDataDocumentType], cancellationToken);

    public Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken = default)
        => PickMacDocumentAsync([MacFolderDocumentType], cancellationToken);

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
}
#pragma warning restore CA1422
