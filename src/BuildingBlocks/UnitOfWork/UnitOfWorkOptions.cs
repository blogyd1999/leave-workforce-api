namespace LeaveWorkforce.BuildingBlocks.UnitOfWork;

public sealed class UnitOfWorkOptions
{
    /// <summary>Applied as SET LOCAL lock_timeout in every transaction (AD-4).</summary>
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
