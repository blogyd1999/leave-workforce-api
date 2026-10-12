using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Conditions;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace LeaveWorkforce.Architecture.Tests;

/// <summary>Violations found by a rule; empty means the rule passed.</summary>
public sealed record RuleOutcome(IReadOnlyList<string> Violations)
{
    public bool Passed => Violations.Count == 0;

    public override string ToString() => string.Join(Environment.NewLine, Violations);
}

/// <summary>
/// The AD-1 and AD-2 rules, each a function over an architecture and a <see cref="ModuleSet"/>
/// so the real solution and the fixtures run the same code.
/// </summary>
public static class ArchitectureRules
{
    private const string DbContextName = "Microsoft.EntityFrameworkCore.DbContext";

    // Rule 1: project reference graph (pure function over a parsed map).
    public static RuleOutcome ProjectReferences(
        IReadOnlyDictionary<string, IReadOnlySet<string>> actual,
        IReadOnlyDictionary<string, IReadOnlySet<string>> expected)
    {
        var violations = new List<string>();
        foreach (var (project, allowed) in expected)
        {
            if (!actual.TryGetValue(project, out var references))
            {
                violations.Add($"Project {project} was not found.");
                continue;
            }

            violations.AddRange(references.Except(allowed).OrderBy(r => r)
                .Select(r => $"Forbidden project reference: {project} -> {r}."));
            violations.AddRange(allowed.Except(references).OrderBy(r => r)
                .Select(r => $"Missing project reference: {project} -> {r}."));
        }

        violations.AddRange(actual.Keys.Except(expected.Keys).OrderBy(p => p)
            .Select(p => $"Unexpected project {p} is not in the spine."));
        return new RuleOutcome(violations);
    }

    // Rule 2: type-level dependency direction and Contracts-only reach.
    public static RuleOutcome DependencyDirection(ArchUnitNET.Domain.Architecture architecture, ModuleSet set) =>
        Evaluate(architecture, OwnedTypes(architecture, set, set.Modules.Concat(OptionalShared(set))),
            "depend only downwards, through Contracts", type =>
            {
                var module = set.ModuleOf(type.Namespace.FullName);
                var problems = new List<string>();
                foreach (var target in type.Dependencies.Select(d => d.Target).Distinct())
                {
                    var targetNs = target.Namespace.FullName;
                    var targetModule = set.ModuleOf(targetNs);
                    if (targetModule is null || targetModule == module)
                        continue;

                    if (module is null)
                        problems.Add($"{type.FullName} (BuildingBlocks) depends on module {targetModule.Name}: {target.FullName}");
                    else if (set.RankOf(targetModule) < set.RankOf(module))
                        problems.Add($"{type.FullName} ({module.Name}) depends on higher module {targetModule.Name}: {target.FullName}");
                    else if (!targetModule.IsContracts(targetNs))
                        problems.Add($"{type.FullName} ({module.Name}) reaches into {targetModule.Name} outside Contracts: {target.FullName}");
                }

                return problems;
            });

    // Rule 3: only Contracts is public in a module.
    public static RuleOutcome PublicSurface(ArchUnitNET.Domain.Architecture architecture, ModuleSet set) =>
        Evaluate(architecture, OwnedTypes(architecture, set, set.Modules), "be internal unless in Contracts", type =>
        {
            var module = set.ModuleOf(type.Namespace.FullName)!;
            return type.Visibility == Visibility.Public && !type.IsNested && !module.IsContracts(type.Namespace.FullName)
                ? [$"{type.FullName} is public in module {module.Name} but is not in {module.ContractsNamespace}"]
                : [];
        });

    // Rule 4: a DbContext is internal, lives in a module assembly and is used by nobody else.
    public static RuleOutcome DbContextContainment(ArchUnitNET.Domain.Architecture architecture, ModuleSet set) =>
        Evaluate(architecture,
            architecture.Types.Where(t => set.InScope(t.Namespace.FullName) && !IsGenerated(t) && IsDbContext(t)).ToList(),
            "be contained in its module", type =>
            {
                var problems = new List<string>();
                var ns = type.Namespace.FullName;
                var module = set.ModuleOf(ns);
                if (module is null || type.Assembly.Name != module.AssemblyName)
                {
                    problems.Add($"{type.FullName} derives from DbContext but does not live in a module assembly");
                    return problems;
                }

                if (type.Visibility != Visibility.Internal)
                    problems.Add($"{type.FullName} derives from DbContext but is {type.Visibility}, not internal");

                problems.AddRange(type.BackwardsDependencies
                    .Select(d => d.Origin)
                    .Where(o => !module.OwnsNamespace(o.Namespace.FullName))
                    .Select(o => o.FullName).Distinct().OrderBy(n => n)
                    .Select(o => $"{type.FullName} ({module.Name} DbContext) is used outside its module by {o}"));
                return problems;
            });

    // Rule 5: assembly and namespace naming.
    public static RuleOutcome Naming(ArchUnitNET.Domain.Architecture architecture, ModuleSet set)
    {
        var violations = new List<string>();
        foreach (var definition in set.All)
        {
            var expectedAssembly = "LeaveWorkforce." + definition.Name;
            if (definition.AssemblyName != expectedAssembly)
                violations.Add($"Assembly {definition.AssemblyName} should be named {expectedAssembly}.");

            var owned = architecture.Types
                .Where(t => t.Assembly.Name == definition.AssemblyName && !IsGenerated(t) && !IsTopLevelProgram(t)
                    && (definition.ScopePrefix is null || ModuleDefinition.IsUnder(t.Namespace.FullName, definition.ScopePrefix)))
                .ToList();
            var outcome = Evaluate(architecture, owned, "have a namespace starting with the assembly name", type =>
                definition.OwnsNamespace(type.Namespace.FullName)
                    ? []
                    : [$"{type.FullName} in assembly {definition.AssemblyName} has namespace '{type.Namespace.FullName}', expected it to start with {definition.NamespacePrefix}"]);
            violations.AddRange(outcome.Violations);
        }

        return new RuleOutcome(violations);
    }

    public static int CountOwnedTypes(ArchUnitNET.Domain.Architecture architecture, ModuleSet set, ModuleDefinition module) =>
        architecture.Types.Count(t => t.Assembly.Name == module.AssemblyName && !IsGenerated(t) && set.InScope(t.Namespace.FullName));

    private static IEnumerable<ModuleDefinition> OptionalShared(ModuleSet set) => set.Shared is null ? [] : [set.Shared];

    private static List<IType> OwnedTypes(ArchUnitNET.Domain.Architecture architecture, ModuleSet set, IEnumerable<ModuleDefinition> owners)
    {
        var list = owners.ToList();
        return architecture.Types
            .Where(t => !IsGenerated(t) && set.InScope(t.Namespace.FullName)
                && list.Any(o => o.AssemblyName == t.Assembly.Name && o.OwnsNamespace(t.Namespace.FullName)))
            .ToList();
    }

    private static bool IsGenerated(IType type) => type.Name.StartsWith('<') || type.FullName.StartsWith('<');

    // The compiler puts a host's top-level-statements entry point in the global namespace; it cannot be renamed.
    private static bool IsTopLevelProgram(IType type) => type.Name == "Program" && type.Namespace.FullName.Length == 0;

    private static bool IsDbContext(IType type)
    {
        for (var baseType = (type as Class)?.BaseClass; baseType is not null; baseType = baseType.BaseClass)
            if (baseType.FullName == DbContextName)
                return true;
        return false;
    }

    // Each rule is an ArchUnitNET rule: types that are, should follow a custom condition.
    private static RuleOutcome Evaluate(
        ArchUnitNET.Domain.Architecture architecture,
        IReadOnlyList<IType> types,
        string description,
        Func<IType, IReadOnlyList<string>> check)
    {
        var rule = Types().That().Are(types).Should().FollowCustomCondition(type =>
        {
            var problems = check(type);
            return problems.Count == 0
                ? new ConditionResult(type, true)
                : new ConditionResult(type, false, string.Join("; ", problems));
        }, description);

        if (types.Count == 0)
            return new RuleOutcome([]);

        var violations = rule.Evaluate(architecture)
            .Where(r => !r.Passed)
            .Select(r => r.Description)
            .ToList();
        return new RuleOutcome(violations);
    }
}
