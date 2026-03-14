using FluentAssertions;
using NexusWorks.Guardian.Acquisition;
using NexusWorks.Guardian.Baseline;
using NexusWorks.Guardian.Comparison;
using NexusWorks.Guardian.Evaluation;
using NexusWorks.Guardian.Inventory;
using NexusWorks.Guardian.Models;
using NexusWorks.Guardian.Orchestration;
using NexusWorks.Guardian.Reporting;
using NexusWorks.Guardian.RuleResolution;
using NexusWorks.Guardian.Tests.TestSupport;

namespace NexusWorks.Guardian.Tests;

[Trait("Category", "Orchestration")]
public class InputPreparationAndRunCoordinatorTests
{
    [Fact]
    public async Task Preparation_should_prepare_local_and_sftp_inputs()
    {
        using var artifacts = new TestArtifactFactory();
        var currentRoot = artifacts.CreateDirectory("current");
        var patchTarget = Path.Combine(artifacts.RootPath, "patch-target");
        var outputRoot = artifacts.CreateDirectory("output");

        var service = new InputPreparationService(new FakeSftpDownloadService(request =>
        {
            Directory.CreateDirectory(request.LocalRootPath);
            File.WriteAllText(Path.Combine(request.LocalRootPath, "patch.txt"), "patched");
            return new SftpDownloadResult(1, 7, request.ClearTargetBeforeDownload, "SHA256:test", Array.Empty<string>());
        }));

        var prepared = await service.PrepareAsync(new GuardianRunRequest(
            CurrentInput: new InputSourceRequest(InputSide.Current, InputSourceMode.Local, currentRoot),
            PatchInput: new InputSourceRequest(
                InputSide.Patch,
                InputSourceMode.Sftp,
                patchTarget,
                new SftpInputRequest("host", 22, "user", "/remote/patch", "secret")),
            BaselinePath: artifacts.WriteTextFile("baseline.xlsx.placeholder", "placeholder"),
            OutputRootPath: outputRoot,
            ReportTitle: "Preparation Test"));

        prepared.Current.EffectiveLocalRootPath.Should().Be(currentRoot);
        prepared.Patch.EffectiveLocalRootPath.Should().Be(Path.GetFullPath(patchTarget));
        File.Exists(Path.Combine(patchTarget, "patch.txt")).Should().BeTrue();
        prepared.AcquisitionSummary.Sides.Should().ContainSingle(side => side.Side == InputSide.Patch && side.Mode == InputSourceMode.Sftp);
    }

    [Fact]
    public async Task Preparation_should_fail_for_non_empty_sftp_target_without_clear()
    {
        using var artifacts = new TestArtifactFactory();
        var currentRoot = artifacts.CreateDirectory("current");
        var patchTarget = artifacts.CreateDirectory("patch-target");
        var outputRoot = artifacts.CreateDirectory("output");
        File.WriteAllText(Path.Combine(patchTarget, "existing.txt"), "do not overwrite");

        var service = new InputPreparationService(new FakeSftpDownloadService(_ =>
            new SftpDownloadResult(0, 0, false, null, Array.Empty<string>())));

        var action = () => service.PrepareAsync(new GuardianRunRequest(
            CurrentInput: new InputSourceRequest(InputSide.Current, InputSourceMode.Local, currentRoot),
            PatchInput: new InputSourceRequest(
                InputSide.Patch,
                InputSourceMode.Sftp,
                patchTarget,
                new SftpInputRequest("host", 22, "user", "/remote/patch", "secret")),
            BaselinePath: artifacts.WriteTextFile("baseline.xlsx.placeholder", "placeholder"),
            OutputRootPath: outputRoot,
            ReportTitle: "Preparation Failure Test"));

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not empty*");
    }

    [Fact]
    public async Task Preparation_should_forward_private_key_authentication_settings()
    {
        using var artifacts = new TestArtifactFactory();
        var currentTarget = Path.Combine(artifacts.RootPath, "current-target");
        var patchRoot = artifacts.CreateDirectory("patch");
        var outputRoot = artifacts.CreateDirectory("output");
        var baselinePath = artifacts.WriteTextFile("baseline.xlsx.placeholder", "placeholder");
        var observedRequests = new List<SftpDownloadRequest>();

        var service = new InputPreparationService(new FakeSftpDownloadService(request =>
        {
            observedRequests.Add(request);
            Directory.CreateDirectory(request.LocalRootPath);
            File.WriteAllText(Path.Combine(request.LocalRootPath, "current.txt"), "current");
            return new SftpDownloadResult(1, 7, request.ClearTargetBeforeDownload, "SHA256:test", Array.Empty<string>());
        }));

        var prepared = await service.PrepareAsync(new GuardianRunRequest(
            CurrentInput: new InputSourceRequest(
                InputSide.Current,
                InputSourceMode.Sftp,
                currentTarget,
                new SftpInputRequest(
                    "host",
                    22,
                    "user",
                    "/remote/current",
                    Password: null,
                    AuthenticationMode: SftpAuthenticationMode.PrivateKey,
                    PrivateKeyPath: "/keys/current_rsa",
                    PrivateKeyPassphrase: "passphrase")),
            PatchInput: new InputSourceRequest(InputSide.Patch, InputSourceMode.Local, patchRoot),
            BaselinePath: baselinePath,
            OutputRootPath: outputRoot,
            ReportTitle: "Private Key Preparation"));

        observedRequests.Should().ContainSingle();
        observedRequests[0].AuthenticationMode.Should().Be(SftpAuthenticationMode.PrivateKey);
        observedRequests[0].PrivateKeyPath.Should().Be("/keys/current_rsa");
        observedRequests[0].PrivateKeyPassphrase.Should().Be("passphrase");
        prepared.AcquisitionSummary.Sides.Should().ContainSingle(side =>
            side.Side == InputSide.Current
            && side.Mode == InputSourceMode.Sftp
            && side.AuthenticationMode == SftpAuthenticationMode.PrivateKey);
    }

    [Fact]
    public async Task Run_coordinator_should_attach_input_acquisition_summary_to_report()
    {
        using var artifacts = new TestArtifactFactory();
        var currentRoot = artifacts.CreateDirectory("current");
        var patchTarget = Path.Combine(artifacts.RootPath, "patch-target");
        var outputRoot = artifacts.CreateDirectory("output");

        File.WriteAllText(Path.Combine(currentRoot, "same.txt"), "same");

        var baselinePath = artifacts.WriteBaselineWorkbook(
            "baseline.xlsx",
            [
                new BaselineRule("R001", "same.txt", null, GuardianFileType.Auto, false, CompareMode.Hash, false, false, 1, null),
            ]);

        var coordinator = new GuardianRunCoordinator(
            new InputPreparationService(new FakeSftpDownloadService(request =>
            {
                Directory.CreateDirectory(request.LocalRootPath);
                File.WriteAllText(Path.Combine(request.LocalRootPath, "same.txt"), "same");
                return new SftpDownloadResult(1, 4, request.ClearTargetBeforeDownload, "SHA256:test", ["downloaded"]);
            })),
            CreateComparisonEngine(),
            CreateReportService());

        var report = await coordinator.RunAsync(new GuardianRunRequest(
            CurrentInput: new InputSourceRequest(InputSide.Current, InputSourceMode.Local, currentRoot),
            PatchInput: new InputSourceRequest(
                InputSide.Patch,
                InputSourceMode.Sftp,
                patchTarget,
                new SftpInputRequest("host", 22, "user", "/remote/patch", "secret")),
            baselinePath,
            outputRoot,
            "Coordinator Test"));

        report.InputAcquisition.Should().NotBeNull();
        report.InputAcquisition!.Sides.Should().ContainSingle(side => side.Side == InputSide.Patch && side.DownloadedFileCount == 1);
        report.Summary.StageMetrics.Should().Contain(stage => stage.StageName == "Patch Input Preparation");
        report.Result.Items.Should().ContainSingle(item => item.RelativePath == "same.txt" && item.Status == CompareStatus.Ok);
    }

    private static GuardianComparisonEngine CreateComparisonEngine()
        => new(
            new ClosedXmlBaselineReader(),
            new BaselineValidator(),
            new FileSystemInventoryScanner(new Sha256HashProvider()),
            new BaselineRuleResolver(),
            new GuardianFileComparer(new JarComparer(), new XmlComparer(), new YamlComparer(), new StatusEvaluator()));

    private static GuardianReportService CreateReportService()
        => new(
            new ResultAggregator(),
            new StaticHtmlReportWriter(),
            new ClosedXmlExcelReportWriter());

    private sealed class FakeSftpDownloadService : ISftpDownloadService
    {
        private readonly Func<SftpDownloadRequest, SftpDownloadResult> _handler;

        public FakeSftpDownloadService(Func<SftpDownloadRequest, SftpDownloadResult> handler)
        {
            _handler = handler;
        }

        public Task<SftpDownloadResult> DownloadAsync(SftpDownloadRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(_handler(request));
    }
}
