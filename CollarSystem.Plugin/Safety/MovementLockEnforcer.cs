using CollarSystem.Plugin.Commands;

namespace CollarSystem.Plugin.Safety;

/// Adapts MovementLockService's owner-token Engage/Release to RestrictionRuleManager's IRestrictionEnforcer
/// contract for the ForcedPose rule kind - a forced-pose restraint device suppresses movement input the
/// same way Follow's leash does, but must claim its own token ("Restraints") so releasing one never
/// prematurely lifts the other's suppression (see MovementLockService's engagedBy set).
public sealed class MovementLockEnforcer : IRestrictionEnforcer
{
    private const string Owner = "Restraints";

    private readonly MovementLockService movementLock;

    public MovementLockEnforcer(MovementLockService movementLock) => this.movementLock = movementLock;

    public void Engage() => movementLock.Engage(Owner);
    public void Release() => movementLock.Release(Owner);
}
