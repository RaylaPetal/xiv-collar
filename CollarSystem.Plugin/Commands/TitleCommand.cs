using CollarSystem.Plugin.Config;
using CollarSystem.Plugin.Ipc;
using CollarSystem.Plugin.Safety;

namespace CollarSystem.Plugin.Commands;

/// collar/title: alias-triggered title changes applied via Honorific on the Sub's own client, plus the
/// Owner's "joker" override (ForceApply/ForceClear - see ChatCommandListener's reserved-keyword grammar).
/// A force-applied title locks out the Sub's own alias-triggered Apply/Clear until the matching
/// ForceClear (or panic) releases it - the Sub set up their aliases, but a forced title always wins over
/// them while it's in effect.
public sealed class TitleCommand
{
    private readonly HonorificIpc honorific;
    private readonly SubRuntimeState runtimeState;

    public TitleCommand(HonorificIpc honorific, SubRuntimeState runtimeState)
    {
        this.honorific = honorific;
        this.runtimeState = runtimeState;
    }

    public void Apply(TitleAliasDefinition alias)
    {
        if (runtimeState.TitleForceLocked)
            return;

        honorific.SetTitle(new HonorificTitleData
        {
            Title = alias.Text,
            IsPrefix = alias.IsPrefix,
            Color = alias.Color,
        });
        runtimeState.TitleApplied = true;
    }

    public void Clear()
    {
        if (runtimeState.TitleForceLocked)
            return;

        honorific.ClearTitle();
        runtimeState.TitleApplied = false;
    }

    /// The Owner's direct override: applies immediately and locks out the Sub's own aliases regardless of
    /// what they're set to. No prefix/color control yet - defaults to a plain white suffix, same as
    /// Honorific's own default.
    public void ForceApply(string text)
    {
        honorific.SetTitle(new HonorificTitleData { Title = text, IsPrefix = false, Color = new(1, 1, 1) });
        runtimeState.TitleApplied = true;
        runtimeState.TitleForceLocked = true;
    }

    /// The only thing that can release a force-applied title besides panic.
    public void ForceClear()
    {
        honorific.ClearTitle();
        runtimeState.TitleApplied = false;
        runtimeState.TitleForceLocked = false;
    }
}
