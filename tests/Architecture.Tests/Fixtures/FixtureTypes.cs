#pragma warning disable CA1812 // fixtures are never instantiated; they only exist to be analysed
using Microsoft.EntityFrameworkCore;

// Fixture types stand in for modules. Each scenario has its own namespace root so a bad fixture
// cannot affect another scenario. Nothing here is ever executed; rules read the compiled assembly.

namespace LeaveWorkforce.Architecture.Tests.Fixtures.Good.High.Contracts
{
    public interface IHighApi;
}

namespace LeaveWorkforce.Architecture.Tests.Fixtures.Good.High
{
    internal sealed class HighService(Low.Contracts.ILowApi low, Shared.SharedThing shared, HighDbContext db)
    {
        public object[] Parts => [low, shared, db];
    }

    internal sealed class HighDbContext : DbContext;
}

namespace LeaveWorkforce.Architecture.Tests.Fixtures.Good.Low.Contracts
{
    public interface ILowApi;
}

namespace LeaveWorkforce.Architecture.Tests.Fixtures.Good.Low
{
    internal sealed class LowInternal;

    internal sealed class LowService(LowInternal own, Shared.SharedThing shared)
    {
        public object[] Parts => [own, shared];
    }
}

namespace LeaveWorkforce.Architecture.Tests.Fixtures.Good.Shared
{
    public sealed class SharedThing;
}

namespace LeaveWorkforce.Architecture.Tests.Fixtures.BadDirection.High.Contracts
{
    public interface IHighApi;
}

namespace LeaveWorkforce.Architecture.Tests.Fixtures.BadDirection.Low
{
    internal sealed class LowReachesUp(High.Contracts.IHighApi high)
    {
        public object Part => high;
    }
}

namespace LeaveWorkforce.Architecture.Tests.Fixtures.BadInternals.High
{
    internal sealed class HighReachesIntoLow(Low.LowInternal low)
    {
        public object Part => low;
    }
}

namespace LeaveWorkforce.Architecture.Tests.Fixtures.BadInternals.Low
{
    internal sealed class LowInternal;
}

namespace LeaveWorkforce.Architecture.Tests.Fixtures.BadShared.Low.Contracts
{
    public interface ILowApi;
}

namespace LeaveWorkforce.Architecture.Tests.Fixtures.BadShared.Shared
{
    internal sealed class SharedReachesModule(Low.Contracts.ILowApi low)
    {
        public object Part => low;
    }
}

namespace LeaveWorkforce.Architecture.Tests.Fixtures.BadSurface.High
{
    public sealed class PublicLeak;
}

namespace LeaveWorkforce.Architecture.Tests.Fixtures.BadDbPublic.High
{
    public sealed class PublicDbContext : DbContext;
}

namespace LeaveWorkforce.Architecture.Tests.Fixtures.BadDbUsed.High
{
    internal sealed class HighDbContext : DbContext;
}

namespace LeaveWorkforce.Architecture.Tests.Fixtures.BadDbUsed.Low
{
    internal sealed class LowUsesHighDb(High.HighDbContext db)
    {
        public object Part => db;
    }
}

namespace LeaveWorkforce.Architecture.Tests.Fixtures.BadDbOutside.High
{
    internal sealed class Placeholder;
}

namespace LeaveWorkforce.Architecture.Tests.Fixtures.BadDbOutside.Stray
{
    internal sealed class StrayDbContext : DbContext;
}

namespace LeaveWorkforce.Architecture.Tests.Fixtures.NamingGood
{
    internal sealed class WellNamed;
}

namespace LeaveWorkforce.Architecture.Tests.Fixtures.NamingBad.Other
{
    internal sealed class WrongNamespace;
}
