using FluentAssertions;
using NexusWorks.Guardian.Models;
using NexusWorks.Guardian.Preferences;
using NexusWorks.Guardian.Tests.TestSupport;

namespace NexusWorks.Guardian.Tests;

public class RecentPathStoreTests
{
    [Fact]
    public void Recent_path_store_should_keep_newest_first_without_duplicates()
    {
        using var artifacts = new TestArtifactFactory();
        var store = new FileSystemRecentPathStore(artifacts.CreateDirectory("state"));

        store.Remember(RecentPathKind.CurrentRoot, artifacts.CreateDirectory("paths/current-a"), maxCount: 3);
        store.Remember(RecentPathKind.CurrentRoot, artifacts.CreateDirectory("paths/current-b"), maxCount: 3);
        store.Remember(RecentPathKind.CurrentRoot, artifacts.CreateDirectory("paths/current-a"), maxCount: 3);
        store.Remember(RecentPathKind.CurrentRoot, artifacts.CreateDirectory("paths/current-c"), maxCount: 3);
        store.Remember(RecentPathKind.CurrentRoot, artifacts.CreateDirectory("paths/current-d"), maxCount: 3);

        var recent = store.List(RecentPathKind.CurrentRoot, maxCount: 3);

        recent.Should().HaveCount(3);
        recent[0].Should().EndWith(Path.Combine("paths", "current-d"));
        recent[1].Should().EndWith(Path.Combine("paths", "current-c"));
        recent[2].Should().EndWith(Path.Combine("paths", "current-a"));
    }

    [Fact]
    public void Recent_path_store_should_isolate_each_path_kind()
    {
        using var artifacts = new TestArtifactFactory();
        var store = new FileSystemRecentPathStore(artifacts.CreateDirectory("state"));

        var currentRoot = artifacts.CreateDirectory("roots/current");
        var patchRoot = artifacts.CreateDirectory("roots/patch");
        var baselineFile = artifacts.WriteTextFile("baseline.xlsx", "placeholder");

        store.Remember(RecentPathKind.CurrentRoot, currentRoot);
        store.Remember(RecentPathKind.PatchRoot, patchRoot);
        store.Remember(RecentPathKind.BaselineFile, baselineFile);

        store.List(RecentPathKind.CurrentRoot).Should().ContainSingle().Which.Should().Be(Path.GetFullPath(currentRoot));
        store.List(RecentPathKind.PatchRoot).Should().ContainSingle().Which.Should().Be(Path.GetFullPath(patchRoot));
        store.List(RecentPathKind.BaselineFile).Should().ContainSingle().Which.Should().Be(Path.GetFullPath(baselineFile));
        store.List(RecentPathKind.OutputRoot).Should().BeEmpty();
    }
}
