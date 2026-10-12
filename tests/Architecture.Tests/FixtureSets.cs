using System.Reflection;

namespace LeaveWorkforce.Architecture.Tests;

/// <summary>Module sets over the fixture namespaces in this assembly, one scenario per root.</summary>
public static class FixtureSets
{
    private const string Root = "LeaveWorkforce.Architecture.Tests.Fixtures";
    private static readonly string TestAssembly = typeof(FixtureSets).Assembly.GetName().Name!;

    public static readonly Lazy<ArchUnitNET.Domain.Architecture> Architecture = new(() =>
        new ArchUnitNET.Loader.ArchLoader()
            .LoadAssemblies(typeof(FixtureSets).Assembly, Assembly.Load("Microsoft.EntityFrameworkCore"))
            .Build());

    public static ModuleSet Scenario(string name, bool withShared = false)
    {
        var scope = $"{Root}.{name}";
        ModuleDefinition Module(string part) => new(part, TestAssembly, $"{scope}.{part}", scope);
        return new ModuleSet(
            [Module("High"), Module("Low")],
            withShared ? Module("Shared") : null,
            [],
            scope);
    }

    /// <summary>A set whose single definition expects types under <paramref name="expectedPrefix"/>.</summary>
    public static ModuleSet Naming(string scenario, string expectedPrefix)
    {
        var scope = $"{Root}.{scenario}";
        var definition = new ModuleDefinition("Architecture.Tests", TestAssembly, $"{Root}.{expectedPrefix}", scope);
        return new ModuleSet([definition], null, [], scope);
    }

    public static ModuleSet DbOutside()
    {
        var scope = $"{Root}.BadDbOutside";
        return new ModuleSet([new ModuleDefinition("High", TestAssembly, $"{scope}.High", scope)], null, [], scope);
    }
}
