namespace SuperTutty.UI;

public partial class MainPage : ContentPage
{
	const int WindowWidth = 900;
	const int WindowHeight = 750;

	public MainPage()
	{
		Microsoft.Maui.Handlers.WindowHandler.Mapper.AppendToMapping(nameof(IWindow), (handler, view) =>
	   {
#if WINDOWS

		   var mauiWindow = handler.VirtualView;
		   var nativeWindow = handler.PlatformView;
		   nativeWindow.Activate();
		   IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
		   var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
		   var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
		   appWindow.Resize(new Windows.Graphics.SizeInt32(WindowWidth, WindowHeight));
#endif

	   });

		InitializeComponent();
	}
}
