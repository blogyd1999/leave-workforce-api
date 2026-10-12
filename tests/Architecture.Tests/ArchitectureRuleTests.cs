using Xunit;

namespace LeaveWorkforce.Architecture.Tests;

public class RealSolutionTests
{
    private static ArchUnitNET.Domain.Architecture Architecture => RealSolution.Architecture.Value;

    [Fact]
    public void Project_references_match_the_spine()
    {
        var outcome = ArchitectureRules.ProjectReferences(ProjectReferenceReader.ReadSrc(), RealSolution.ExpectedReferences);
        Assert.True(outcome.Passed, outcome.ToString());
    }

    [Fact]
    public void Every_src_assembly_is_loaded_with_types()
    {
        foreach (var module in RealSolution.Set.All)
            Assert.True(ArchitectureRules.CountOwnedTypes(Architecture, RealSolution.Set, module) > 0, $"{module.AssemblyName} has no types.");
    }

    [Fact]
    public void Dependencies_point_downwards_and_through_Contracts()
    {
        var outcome = ArchitectureRules.DependencyDirection(Architecture, RealSolution.Set);
        Assert.True(outcome.Passed, outcome.ToString());
    }

    [Fact]
    public void Only_Contracts_is_public_in_a_module()
    {
        var outcome = ArchitectureRules.PublicSurface(Architecture, RealSolution.Set);
        Assert.True(outcome.Passed, outcome.ToString());
    }

    [Fact]
    public void DbContexts_are_contained_in_their_module()
    {
        var outcome = ArchitectureRules.DbContextContainment(Architecture, RealSolution.Set);
        Assert.True(outcome.Passed, outcome.ToString());
    }

    [Fact]
    public void Assemblies_and_namespaces_are_named_after_the_project()
    {
        var outcome = ArchitectureRules.Naming(Architecture, RealSolution.Set);
        Assert.True(outcome.Passed, outcome.ToString());
    }
}

public class RuleFixtureTests
{
    private static ArchUnitNET.Domain.Architecture Architecture => FixtureSets.Architecture.Value;

    private static void AssertFails(RuleOutcome outcome, params string[] offenders)
    {
        Assert.False(outcome.Passed, "The rule should have failed.");
        foreach (var offender in offenders)
            Assert.Contains(offender, outcome.ToString());
    }

    [Fact]
    public void Forbidden_project_reference_fails_and_names_the_edge()
    {
        var bad = RealSolution.ExpectedReferences.ToDictionary(p => p.Key, p => p.Value);
        bad["Leave"] = new HashSet<string> { "Audit", "BuildingBlocks", "People" };

        AssertFails(ArchitectureRules.ProjectReferences(bad, RealSolution.ExpectedReferences), "Leave -> People");
    }

    [Fact]
    public void Missing_project_reference_fails_and_names_the_edge()
    {
        var bad = RealSolution.ExpectedReferences.ToDictionary(p => p.Key, p => p.Value);
        bad["Audit"] = new HashSet<string>();

        AssertFails(ArchitectureRules.ProjectReferences(bad, RealSolution.ExpectedReferences), "Audit -> BuildingBlocks");
    }

    [Fact]
    public void Allowed_dependencies_pass()
    {
        var set = FixtureSets.Scenario("Good", withShared: true);
        Assert.True(ArchitectureRules.DependencyDirection(Architecture, set).Passed);
        Assert.True(ArchitectureRules.PublicSurface(Architecture, set).Passed);
        Assert.True(ArchitectureRules.DbContextContainment(Architecture, set).Passed);
    }

    [Fact]
    public void Lower_module_using_a_higher_module_fails()
    {
        var outcome = ArchitectureRules.DependencyDirection(Architecture, FixtureSets.Scenario("BadDirection"));
        AssertFails(outcome, "LowReachesUp", "higher module");
    }

    [Fact]
    public void Reaching_into_another_modules_non_Contracts_type_fails()
    {
        var outcome = ArchitectureRules.DependencyDirection(Architecture, FixtureSets.Scenario("BadInternals"));
        AssertFails(outcome, "HighReachesIntoLow", "LowInternal", "outside Contracts");
    }

    [Fact]
    public void BuildingBlocks_depending_on_a_module_fails()
    {
        var outcome = ArchitectureRules.DependencyDirection(Architecture, FixtureSets.Scenario("BadShared", withShared: true));
        AssertFails(outcome, "SharedReachesModule", "BuildingBlocks");
    }

    [Fact]
    public void Public_type_outside_Contracts_fails()
    {
        var outcome = ArchitectureRules.PublicSurface(Architecture, FixtureSets.Scenario("BadSurface"));
        AssertFails(outcome, "PublicLeak");
    }

    [Fact]
    public void Public_DbContext_fails()
    {
        var outcome = ArchitectureRules.DbContextContainment(Architecture, FixtureSets.Scenario("BadDbPublic"));
        AssertFails(outcome, "PublicDbContext", "not internal");
    }

    [Fact]
    public void DbContext_used_by_another_module_fails()
    {
        var outcome = ArchitectureRules.DbContextContainment(Architecture, FixtureSets.Scenario("BadDbUsed"));
        AssertFails(outcome, "HighDbContext", "LowUsesHighDb");
    }

    [Fact]
    public void DbContext_outside_every_module_fails()
    {
        var outcome = ArchitectureRules.DbContextContainment(Architecture, FixtureSets.DbOutside());
        AssertFails(outcome, "StrayDbContext", "does not live in a module assembly");
    }

    [Fact]
    public void Namespace_not_starting_with_the_assembly_name_fails()
    {
        var outcome = ArchitectureRules.Naming(Architecture, FixtureSets.Naming("NamingBad", "NamingBad.Expected"));
        AssertFails(outcome, "WrongNamespace");
    }

    [Fact]
    public void Well_named_namespace_passes()
    {
        Assert.True(ArchitectureRules.Naming(Architecture, FixtureSets.Naming("NamingGood", "NamingGood")).Passed);
    }

    [Fact]
    public void Assembly_not_named_after_the_project_fails()
    {
        AssertFails(ArchitectureRules.Naming(Architecture, FixtureSets.Scenario("Good")), "should be named LeaveWorkforce.High");
    }
}
