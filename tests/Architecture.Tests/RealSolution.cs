using ArchUnitNET.Domain;
using Assembly = System.Reflection.Assembly;
using ArchUnitNET.Loader;

namespace LeaveWorkforce.Architecture.Tests;

/// <summary>The six <c>src</c> assemblies and the spine's project reference graph (AD-1, AD-2).</summary>
public static class RealSolution
{
    public static readonly ModuleDefinition People = Define("People");
    public static readonly ModuleDefinition Leave = Define("Leave");
    public static readonly ModuleDefinition Audit = Define("Audit");
    public static readonly ModuleDefinition BuildingBlocks = Define("BuildingBlocks");
    public static readonly ModuleDefinition Api = Define("Api");
    public static readonly ModuleDefinition Operator = Define("Operator");

    public static readonly ModuleSet Set = new([People, Leave, Audit], BuildingBlocks, [Api, Operator]);

    /// <summary>Allowed direct project references, by project name, exactly as the spine fixes them.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> ExpectedReferences =
        new Dictionary<string, IReadOnlySet<string>>
        {
            ["People"] = new HashSet<string> { "Leave", "Audit", "BuildingBlocks" },
            ["Leave"] = new HashSet<string> { "Audit", "BuildingBlocks" },
            ["Audit"] = new HashSet<string> { "BuildingBlocks" },
            ["BuildingBlocks"] = new HashSet<string>(),
            ["Api"] = new HashSet<string> { "People", "Leave", "Audit", "BuildingBlocks" },
            ["Operator"] = new HashSet<string> { "People", "Leave", "Audit", "BuildingBlocks" },
        };

    public static readonly Lazy<ArchUnitNET.Domain.Architecture> Architecture = new(Load);

    private static ModuleDefinition Define(string name) =>
        new(name, "LeaveWorkforce." + name, "LeaveWorkforce." + name);

    private static ArchUnitNET.Domain.Architecture Load()
    {
        var assemblies = Set.All.Select(m => Assembly.Load(m.AssemblyName)).ToList();
        // EF Core is loaded so a DbContext subclass resolves to the real base type.
        assemblies.Add(Assembly.Load("Microsoft.EntityFrameworkCore"));
        return new ArchLoader().LoadAssemblies(assemblies.ToArray()).Build();
    }
}
