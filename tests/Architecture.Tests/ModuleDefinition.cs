namespace LeaveWorkforce.Architecture.Tests;

/// <summary>
/// One project of the spine. <paramref name="ScopePrefix"/> narrows which types of the assembly belong to it;
/// it is null for the real assemblies and set for fixtures that share the test assembly.
/// </summary>
public sealed record ModuleDefinition(string Name, string AssemblyName, string NamespacePrefix, string? ScopePrefix = null)
{
    public string ContractsNamespace => NamespacePrefix + ".Contracts";

    public static bool IsUnder(string? ns, string prefix) =>
        ns is not null && (ns == prefix || ns.StartsWith(prefix + ".", StringComparison.Ordinal));

    public bool OwnsNamespace(string? ns) => IsUnder(ns, NamespacePrefix);

    public bool IsContracts(string? ns) => IsUnder(ns, ContractsNamespace);
}

/// <summary>
/// The shape the rules run against. <paramref name="Modules"/> is ordered from the highest module to the lowest
/// (People, Leave, Audit); <paramref name="Shared"/> sits below all of them.
/// </summary>
public sealed record ModuleSet(
    IReadOnlyList<ModuleDefinition> Modules,
    ModuleDefinition? Shared,
    IReadOnlyList<ModuleDefinition> Hosts,
    string? Scope = null)
{
    public IEnumerable<ModuleDefinition> All =>
        Modules.Concat(Shared is null ? [] : [Shared]).Concat(Hosts);

    public bool InScope(string? ns) => Scope is null || ModuleDefinition.IsUnder(ns, Scope);

    public ModuleDefinition? ModuleOf(string? ns) => Modules.FirstOrDefault(m => m.OwnsNamespace(ns));

    public int RankOf(ModuleDefinition module) => Modules.ToList().IndexOf(module);
}
