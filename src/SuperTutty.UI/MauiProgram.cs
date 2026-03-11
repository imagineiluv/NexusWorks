using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Maui.LifecycleEvents;
using SuperTutty.Analyzers;
using SuperTutty.Services;
using SuperTutty.Services.AI;
using SuperTutty.Services.Drain;
using SuperTutty.Services.Tasks;
using SuperTutty.UI.Services;

namespace SuperTutty.UI;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("InterVariable.ttf", "Inter");
				fonts.AddFont("JetBrainsMono-Variable.ttf", "JetBrainsMono");
			});

		builder.Services.AddMauiBlazorWebView();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		// Services
		builder.Services.AddSingleton<ISessionStorage, PreferencesSessionStorage>();
		builder.Services.AddSingleton<SelectedSessionStore>();
		builder.Services.AddSingleton<SessionManager>();
		builder.Services.AddSingleton<ISessionManager>(sp => sp.GetRequiredService<SessionManager>());
		builder.Services.AddSingleton<ILogPersistence, LogDatabase>();
		builder.Services.AddSingleton<LogDatabase>();
		builder.Services.AddSingleton<ILlmService, LocalLlmService>();
		builder.Services.AddSingleton<SshLogStreamConnector>();
		builder.Services.AddSingleton<MacLocalLogStreamConnector>(sp => new MacLocalLogStreamConnector(sp.GetRequiredService<SshLogStreamConnector>()));
		builder.Services.AddSingleton<ILogStreamConnector>(sp => new WindowsLocalLogStreamConnector(sp.GetRequiredService<MacLocalLogStreamConnector>()));
		builder.Services.AddSingleton(Options.Create(new LogStreamOptions()));
		builder.Services.AddSingleton<IRemoteShellService, RemoteShellService>();

		// Tasks & Task Manager
		builder.Services.AddSingleton<ITaskManager, TaskManager>();
		builder.Services.AddSingleton<IStreamTaskFactory, StreamTaskFactory>();

		// Analyzers & Drain
		builder.Services.AddSingleton<TransactionAnalyzer>();
		builder.Services.AddSingleton<EquipmentAnalyzer>();
		builder.Services.AddSingleton<DrainLogParser>(sp => new DrainLogParser(depth: 4, similarityThreshold: 0.5));

		builder.Services.AddTransient<ILogStreamService, LogStreamService>();

		// Process Monitor
		builder.Services.AddSingleton<IProcessMonitorService, ProcessMonitorService>();

		// Command Catalog
		builder.Services.AddSingleton<ICommandCatalog, CommandCatalog>();

		return builder.Build();
	}
}
