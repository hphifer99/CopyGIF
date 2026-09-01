namespace CopyGIF.Testing;

public static class RepositoryRootLocator
{
    public static string Find()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string v1SolutionPath = Path.Combine(
                directory.FullName,
                "CopyGIF.slnx");

            string v2SolutionPath = Path.Combine(
                directory.FullName,
                "CopyGIF.V2.slnx");

            if (File.Exists(v1SolutionPath) &&
                File.Exists(v2SolutionPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "The CopyGIF repository root could not be located.");
    }
}