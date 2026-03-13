namespace NexusWorks.Guardian.UI;

public partial class App : Application
{
    private const double DefaultWindowWidth = 1440;
    private const double DefaultWindowHeight = 960;
    private const double MinimumWindowWidth = 1100;
    private const double MinimumWindowHeight = 760;

	public App()
	{
		InitializeComponent();
	}

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage())
        {
            Title = "NexusWorks.Guardian",
            Width = DefaultWindowWidth,
            Height = DefaultWindowHeight,
            MinimumWidth = MinimumWindowWidth,
            MinimumHeight = MinimumWindowHeight,
        };
    }
}
