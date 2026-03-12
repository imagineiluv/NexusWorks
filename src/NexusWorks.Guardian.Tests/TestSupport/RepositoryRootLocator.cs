namespace NexusWorks.Guardian.Tests.TestSupport;

internal static class RepositoryRootLocator
{
    public static string Find()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src"))
                && Directory.Exists(Path.Combine(directory.FullName, "docs"))
                && Directory.Exists(Path.Combine(directory.FullName, "sample")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Failed to locate the NexusWorks repository root from the current test output path.");
    }
}
