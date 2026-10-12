namespace LeaveWorkforce.BuildingBlocks.UnitOfWork;

/// <summary>
/// Named advisory lock keys of AD-4. The numeric value is the fixed acquisition order:
/// Administrator before Tree, never backwards.
/// </summary>
public enum AdvisoryLockKey
{
    Administrator = 1,
    Tree = 2,
}

internal static class AdvisoryLockKeys
{
    private const long Namespace = 0x4C57_0000_0000_0000; // "LW" prefix keeps ids apart from other users of advisory locks

    public static long ToDatabaseKey(AdvisoryLockKey key) => key switch
    {
        AdvisoryLockKey.Administrator or AdvisoryLockKey.Tree => Namespace | (long)key,
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown advisory lock key."),
    };
}
