using CollarSystem.Plugin.Safety;

namespace CollarSystem.Plugin.Commands;

/// collar/follow: movement-lock (leash) enforcement, gated behind its own "Follow" permission which
/// ChatCommandListener checks before Engage/Release ever runs - the same dedicated opt-in the spec
/// requires, kept separate from the other three categories by construction.
public sealed class FollowCommand
{
    private readonly MovementLockService movementLock;
    private readonly SubRuntimeState runtimeState;

    public FollowCommand(MovementLockService movementLock, SubRuntimeState runtimeState)
    {
        this.movementLock = movementLock;
        this.runtimeState = runtimeState;
    }

    public bool Engage()
    {
        if (!movementLock.IsAvailable)
            return false;

        movementLock.Engage();
        runtimeState.MovementLockActive = true;
        return true;
    }

    public void Release()
    {
        movementLock.Release();
        runtimeState.MovementLockActive = false;
    }
}
