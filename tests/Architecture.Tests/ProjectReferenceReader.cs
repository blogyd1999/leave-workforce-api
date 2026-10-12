using System.Xml.Linq;

namespace LeaveWorkforce.Architecture.Tests;

public static class ProjectReferenceReader
{
    /// <summary>Reads the direct project references of every <c>src/*/*.csproj</c>, keyed by project name.</summary>
    public static IReadOnlyDictionary<string, IReadOnlySet<string>> ReadSrc()
    {
        var src = Path.Combine(FindRepositoryRoot(), "src");
        var map = new Dictionary<string, IReadOnlySet<string>>();
        foreach (var project in Directory.GetFiles(src, "*.csproj", SearchOption.AllDirectories))
        {
            var references = XDocument.Load(project)
                .Descendants("ProjectReference")
                .Select(e => (string?)e.Attribute("Include"))
                .Where(include => include is not null)
                .Select(include => ProjectName(include!))
                .ToHashSet();
            map[ProjectName(project)] = references;
        }

        return map;
    }

    private static string ProjectName(string path)
    {
        var file = Path.GetFileNameWithoutExtension(path.Replace('\\', '/').Split('/').Last());
        const string prefix = "LeaveWorkforce.";
        return file.StartsWith(prefix, StringComparison.Ordinal) ? file[prefix.Length..] : file;
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LeaveWorkforce.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("LeaveWorkforce.slnx not found above the test output directory.");
    }
}
