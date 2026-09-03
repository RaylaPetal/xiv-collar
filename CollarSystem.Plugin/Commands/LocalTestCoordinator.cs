using CollarSystem.Plugin.Config;

namespace CollarSystem.Plugin.Commands;

public readonly record struct LocalTestResult(bool Success, string Message)
{
    public static LocalTestResult Ok(string message) => new(true, message);
    public static LocalTestResult Fail(string message) => new(false, message);
}

/// collar/ui-organization's local pre-pair testing: dispatches through the exact same local action
/// methods an accepted Owner's trigger tell (or override) would use - Apply/Clear/Unlock/Engage/Release
/// for the alias-driven categories, ForceApply/ForceUnlock/ForceClear for Collar/Moodles where that *is*
/// the only real Owner path. Never fabricates a pairing identity, composes a message, or touches
/// ChatCommandListener/ChatSender - pairing state and chat transport are the only things bypassed. Every
/// test still enforces the action's normal category permission plus, for Gesture/Follow, the existing
/// automation-risk acknowledgement.
public sealed class LocalTestCoordinator
{
    private readonly PluginConfig config;
    private readonly TitleCommand title;
    private readonly OutfitCommand outfit;
    private readonly GestureCommand gesture;
    private readonly FollowCommand follow;
    private readonly CollarCommand collar;
    private readonly MoodlesCommand moodles;

    public LocalTestCoordinator(PluginConfig config, TitleCommand title, OutfitCommand outfit, GestureCommand gesture, FollowCommand follow, CollarCommand collar, MoodlesCommand moodles)
    {
        this.config = config;
        this.title = title;
        this.outfit = outfit;
        this.gesture = gesture;
        this.follow = follow;
        this.collar = collar;
        this.moodles = moodles;
    }

    public LocalTestResult TestTitleApply(TitleAliasDefinition alias)
    {
        if (!RequirePermission(config.Permissions.Title, "Title", out var denied))
            return denied;

        title.Apply(alias);
        return LocalTestResult.Ok($"Applied title \"{alias.Text}\".");
    }

    public LocalTestResult TestTitleClear()
    {
        if (!RequirePermission(config.Permissions.Title, "Title", out var denied))
            return denied;

        title.Clear();
        return LocalTestResult.Ok("Cleared title.");
    }

    public LocalTestResult TestOutfitApply(OutfitAliasDefinition alias)
    {
        if (!RequirePermission(config.Permissions.Outfit, "Outfit", out var denied))
            return denied;

        return outfit.Apply(alias)
            ? LocalTestResult.Ok($"Applied outfit \"{alias.DesignName}\".")
            : LocalTestResult.Fail($"Outfit \"{alias.DesignName}\" failed to apply - Glamourer may be unavailable, the design missing, or the outfit is force-locked.");
    }

    public LocalTestResult TestOutfitUnlock()
    {
        if (!RequirePermission(config.Permissions.Outfit, "Outfit", out var denied))
            return denied;

        return outfit.Unlock()
            ? LocalTestResult.Ok("Outfit unlocked.")
            : LocalTestResult.Fail("Outfit unlock failed - nothing locked, Glamourer unavailable, or the outfit is force-locked.");
    }

    public LocalTestResult TestGesturePlay(GestureAliasDefinition alias)
    {
        if (!RequireGestureGates(out var denied))
            return denied;

        var displayName = alias.AnimationName.Length > 0 ? alias.AnimationName : alias.EmoteName;
        return gesture.Apply(alias)
            ? LocalTestResult.Ok($"Played gesture \"{displayName}\".")
            : LocalTestResult.Fail($"Gesture \"{displayName}\" failed to play - rescan may be required, or Penumbra is unavailable.");
    }

    public LocalTestResult TestCollarLock()
    {
        if (!RequirePermission(config.Permissions.Collar, "Collar", out var denied))
            return denied;

        return collar.ForceApply()
            ? LocalTestResult.Ok("Collar applied and locked.")
            : LocalTestResult.Fail("Collar apply failed - configure a collar item first, or Glamourer is unavailable.");
    }

    public LocalTestResult TestCollarUnlock()
    {
        if (!RequirePermission(config.Permissions.Collar, "Collar", out var denied))
            return denied;

        return collar.ForceUnlock()
            ? LocalTestResult.Ok("Collar unlocked.")
            : LocalTestResult.Fail("Collar unlock failed - nothing locked, or Glamourer is unavailable.");
    }

    public LocalTestResult TestMoodlesApply(MoodlesStatusEntry status)
    {
        if (!RequirePermission(config.Permissions.Moodles, "Moodles", out var denied))
            return denied;

        return moodles.ForceApply(status.Name)
            ? LocalTestResult.Ok($"Applied Moodle \"{status.Name}\".")
            : LocalTestResult.Fail($"Moodle \"{status.Name}\" failed to apply - Moodles may be unavailable.");
    }

    public LocalTestResult TestMoodlesClear()
    {
        if (!RequirePermission(config.Permissions.Moodles, "Moodles", out var denied))
            return denied;

        return moodles.ForceClear()
            ? LocalTestResult.Ok("Cleared Moodle status.")
            : LocalTestResult.Fail("Moodle clear failed - Moodles may be unavailable.");
    }

    public LocalTestResult TestLeashEngage()
    {
        if (!RequireFollowGates(out var denied))
            return denied;

        return follow.Engage()
            ? LocalTestResult.Ok("Movement lock engaged.")
            : LocalTestResult.Fail("Leash failed to engage - the movement-lock service is unavailable.");
    }

    public LocalTestResult TestLeashRelease()
    {
        if (!RequireFollowGates(out var denied))
            return denied;

        follow.Release();
        return LocalTestResult.Ok("Movement lock released.");
    }

    private bool RequirePermission(bool granted, string categoryLabel, out LocalTestResult denied)
    {
        if (!granted)
        {
            denied = LocalTestResult.Fail($"{categoryLabel} permission must be enabled (Permissions tab) before testing.");
            return false;
        }

        denied = default;
        return true;
    }

    private bool RequireGestureGates(out LocalTestResult denied)
    {
        if (!RequirePermission(config.Permissions.Gesture, "Gesture", out denied))
            return false;

        if (!config.TosAcknowledged)
        {
            denied = LocalTestResult.Fail("The automation-risk acknowledgement (Settings) is required before testing Gesture.");
            return false;
        }

        return true;
    }

    private bool RequireFollowGates(out LocalTestResult denied)
    {
        if (!RequirePermission(config.Permissions.Follow, "Follow / Leash", out denied))
            return false;

        if (!config.TosAcknowledged)
        {
            denied = LocalTestResult.Fail("The automation-risk acknowledgement (Settings) is required before testing Leash.");
            return false;
        }

        return true;
    }
}
