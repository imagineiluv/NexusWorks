using Microsoft.Extensions.Logging;
using NexusWorks.Guardian.Acquisition;
using NexusWorks.Guardian.Baseline;
using NexusWorks.Guardian.Comparison;
using NexusWorks.Guardian.Evaluation;
using NexusWorks.Guardian.Inventory;
using NexusWorks.Guardian.Orchestration;
using NexusWorks.Guardian.Preferences;
using NexusWorks.Guardian.Reporting;
using NexusWorks.Guardian.RuleResolution;
using NexusWorks.Guardian.UI.Services;

namespace NexusWorks.Guardian.UI;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(_ => { });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddSingleton<IBaselineReader, ClosedXmlBaselineReader>();
        builder.Services.AddSingleton<IBaselineValidator, BaselineValidator>();
        builder.Services.AddSingleton<IBaselinePreviewService, BaselinePreviewService>();
        builder.Services.AddSingleton<IHashProvider, Sha256HashProvider>();
        builder.Services.AddSingleton<IInventoryScanner, FileSystemInventoryScanner>();
        builder.Services.AddSingleton<IRuleResolver, BaselineRuleResolver>();
        builder.Services.AddSingleton<IJarComparer, JarComparer>();
        builder.Services.AddSingleton<IXmlComparer, XmlComparer>();
        builder.Services.AddSingleton<IYamlComparer, YamlComparer>();
        builder.Services.AddSingleton<IStatusEvaluator, StatusEvaluator>();
        builder.Services.AddSingleton<IFileComparer, GuardianFileComparer>();
        builder.Services.AddSingleton<IResultAggregator, ResultAggregator>();
        builder.Services.AddSingleton<IHtmlReportWriter, StaticHtmlReportWriter>();
        builder.Services.AddSingleton<IExcelReportWriter, ClosedXmlExcelReportWriter>();
        builder.Services.AddSingleton<IExecutionHistoryStore, FileSystemExecutionHistoryStore>();
        builder.Services.AddSingleton<IRecentPathStore>(_ =>
            new FileSystemRecentPathStore(Path.Combine(FileSystem.AppDataDirectory, "state")));
        builder.Services.AddSingleton<ISftpDownloadService, SftpDownloadService>();
        builder.Services.AddSingleton<ISftpSecretStore, MauiSftpSecretStore>();
        builder.Services.AddSingleton<IInputPreparationService, InputPreparationService>();
        builder.Services.AddSingleton<IPathSelectionService>(
#if WINDOWS
            _ => new Guardian.UI.Platforms.Windows.WindowsPathSelectionService()
#elif MACCATALYST
            _ => new Guardian.UI.Platforms.MacCatalyst.MacCatalystPathSelectionService()
#else
            _ => throw new PlatformNotSupportedException("No path selection service is available for this platform.")
#endif
        );
        builder.Services.AddSingleton<GuardianComparisonEngine>();
        builder.Services.AddSingleton<GuardianReportService>();
        builder.Services.AddSingleton<GuardianExecutionRunner>();
        builder.Services.AddSingleton<GuardianRunCoordinator>();
        builder.Services.AddSingleton<GuardianWorkbenchService>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
