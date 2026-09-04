using CollarSystem.Plugin.Commands;

namespace CollarSystem.Plugin.Safety;

/// Adapts MovementLockService's owner-token Engage/Release to RestrictionRuleManager's IRestrictionEnforcer
/// contract - a forced-pose or full-body-cuffed restraint device suppresses movement input the same way
/// Follow's leash does, but must claim its own token so releasing one never prematurely lifts another's
/// suppression (see MovementLockService's engagedBy set). ForcedPose and FullBodyCuffed each get their own
/// instance with a distinct token (rather than sharing one) - RestrictionRuleManager refcounts each rule
/// kind independently, so a shared token would let one kind's release accidentally drop the other's claim
/// while it's still active.
public sealed class MovementLockEnforcer : IRestrictionEnforcer
{
    private readonly MovementLockService movementLock;
    private readonly string ownerToken;

    public MovementLockEnforcer(MovementLockService movementLock, string ownerToken)
    {
        this.movementLock = movementLock;
        this.ownerToken = ownerToken;
    }

    public void Engage() => movementLock.Engage(ownerToken);
    public void Release() => movementLock.Release(ownerToken);
}
