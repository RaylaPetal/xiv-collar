using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Oathbound.Plugin.Commands;
using Oathbound.Plugin.Config;
using Oathbound.Plugin.Ipc;
using Oathbound.Plugin.Relay;
using Oathbound.Plugin.Safety;
using Oathbound.Plugin.UI;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Oathbound.Plugin;

public sealed class Plugin : IDalamudPlugin
{
    private int pendingRestraintCleanup;
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IKeyState KeyState { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static INotificationManager NotificationManager { get; private set; } = null!;

    private const string CommandName = "/oathbound";
    private const string PanicCommandName = "/oathboundpanic";
    private const string SettingsCommandName = "/oathboundsettings";

    /// Backward-compatible aliases (collar/pairing, collar/ui-organization delta specs) - each points at
    /// the exact same handler delegate as its primary command above, so existing macros/keybinds on the
    /// old names keep working identically after the rename, with no behavior duplicated between them.
    /// `ShorthandCommandName` only aliases the main window toggle, not panic/settings - those already have
    /// full mnemonic names and are lower-frequency, so a shorthand adds more accidental-trigger risk than
    /// convenience there.
    private const string LegacyCommandName = "/collar";
    private const string LegacyPanicCommandName = "/collarpanic";
    private const string LegacySettingsCommandName = "/collarsettings";
    private const string ShorthandCommandName = "/ob";

    public PluginConfig Configuration { get; }

    public readonly WindowSystem WindowSystem = new("Oathbound");

    /// collar/catalog-sync: the one native save/open dialog instance backing Export/"Import commands" -
    /// owned here rather than per-window since both Settings (export) and CollarWindow (import) need it,
    /// same "shared, drawn every frame from the UiBuilder.Draw hook" shape WindowSystem already has.
    public readonly FileDialogManager FileDialogManager = new();
    private CollarWindow CollarWindow { get; }
    private SettingsWindow SettingsWindow { get; }
    public AnimationPickerWindow AnimationPickerWindow { get; }
    public ItemPickerWindow ItemPickerWindow { get; }
    public FavoritesBarButton FavoritesBarButton { get; }

    public SubRuntimeState RuntimeState { get; }

    public GlamourerIpc GlamourerIpc { get; }
    public SlotLockManager SlotLockManager { get; }
    public HonorificIpc HonorificIpc { get; }
    public PenumbraIpc PenumbraIpc { get; }
    public MoodlesIpc MoodlesIpc { get; }
    public MovementLockService MovementLockService { get; }
    public WalkOnlyService WalkOnlyService { get; }
    public ActionBlockService ActionBlockService { get; }
    public ChatGagService ChatGagService { get; }
    public RestrictionRuleManager RestrictionRuleManager { get; }

    public DeviceIdentityService DeviceIdentityService { get; }
    public RelayClient RelayClient { get; }
    public PairingService PairingService { get; }
    public RevocationService RevocationService { get; }
    public TitleCommand TitleCommand { get; }
    public OutfitCommand OutfitCommand { get; }
    public GestureCommand GestureCommand { get; }
    public FollowCommand FollowCommand { get; }
    public CollarCommand CollarCommand { get; }
    public MoodlesCommand MoodlesCommand { get; }
    public RestraintCommand RestraintCommand { get; }
    public CustomTriggerCommand CustomTriggerCommand { get; }
    public CatalogSyncService CatalogSyncService { get; }
    public CatalogSyncRelayService CatalogSyncRelayService { get; }
    public ChatComposer ChatComposer { get; }
    public ChatSender ChatSender { get; }
    public ChatCommandListener ChatCommandListener { get; }

    public PanicHandler PanicHandler { get; }

    private bool panicHotkeyWasPressed;
    private DateTime nextRevocationOutboxRetryUtc = DateTime.MinValue;
    private DateTime nextRevocationCheckUtc = DateTime.MinValue;

    /// collar/relay-service "requests stop on logout/disposal": recurring background relay work (the
    /// outbox retry and the missed-revocation check) is cancelled and restarted on every logout, and
    /// cancelled for good in Dispose(). One-shot user-triggered relay actions (Send Invitation, Accept,
    /// panic's revocation publish) are intentionally not tied to this - they're already bounded by
    /// RelayClient's own HTTP timeout, and cancelling a button click the user just made because they
    /// happened to log out mid-request would be a worse experience than just letting it time out normally.
    private CancellationTokenSource relayBackgroundWorkCts = new();

    public Plugin()
    {
        ECommons.ECommonsMain.Init(PluginInterface, this);

        Configuration = PluginInterface.GetPluginConfig() as PluginConfig ?? new PluginConfig();
        MigrateConfiguration();

        RuntimeState = new SubRuntimeState(Configuration);

        GlamourerIpc = new GlamourerIpc();
        SlotLockManager = new SlotLockManager(Configuration, GlamourerIpc);
        HonorificIpc = new HonorificIpc();
        PenumbraIpc = new PenumbraIpc();
        MoodlesIpc = new MoodlesIpc();
        MovementLockService = new MovementLockService();
        WalkOnlyService = new WalkOnlyService();
        ActionBlockService = new ActionBlockService(WalkOnlyService);
        WalkOnlyService.SprintInterceptorAvailable = ActionBlockService.IsAvailable;
        ChatGagService = new ChatGagService();
        RestrictionRuleManager = new RestrictionRuleManager();
        RestrictionRuleManager.RegisterEnforcer(RestraintRuleKind.ForcedPose, new MovementLockEnforcer(MovementLockService, "Restraints"));
        RestrictionRuleManager.RegisterEnforcer(RestraintRuleKind.WalkOnly, WalkOnlyService);
        RestrictionRuleManager.RegisterEnforcer(RestraintRuleKind.ActionBlock, ActionBlockService);
        RestrictionRuleManager.RegisterEnforcer(RestraintRuleKind.GagChat, ChatGagService);
        RestrictionRuleManager.RegisterEnforcer(RestraintRuleKind.FullBodyCuffed, new MovementLockEnforcer(MovementLockService, "RestraintsFullBody"));

        DeviceIdentityService = new DeviceIdentityService(Configuration);
        DeviceIdentityService.EnsureIdentity();
        RelayClient = new RelayClient(Configuration, DeviceIdentityService);
        RevocationService = new RevocationService(Configuration, RelayClient, DeviceIdentityService);

        TitleCommand = new TitleCommand(HonorificIpc, RuntimeState);
        OutfitCommand = new OutfitCommand(Configuration, GlamourerIpc, SlotLockManager, RuntimeState);
        var temporaryModSettings = new TemporaryModSettingsCoordinator(PenumbraIpc);
        GestureCommand = new GestureCommand(Configuration, PenumbraIpc, temporaryModSettings);
        FollowCommand = new FollowCommand(Configuration, MovementLockService, RuntimeState);
        MoodlesCommand = new MoodlesCommand(Configuration, MoodlesIpc);
        CollarCommand = new CollarCommand(Configuration, SlotLockManager, RuntimeState, MoodlesCommand);
        RestraintCommand = new RestraintCommand(Configuration, GlamourerIpc, PenumbraIpc, SlotLockManager, RestrictionRuleManager, RuntimeState, temporaryModSettings);
        CustomTriggerCommand = new CustomTriggerCommand(Configuration, TitleCommand, OutfitCommand, GestureCommand, MoodlesCommand, RestraintCommand);
        CatalogSyncService = new CatalogSyncService(Configuration, OutfitCommand, GestureCommand, MoodlesCommand, RestraintCommand);
        ChatComposer = new ChatComposer(Configuration);
        ChatSender = new ChatSender();
        PairingService = new PairingService(Configuration, RelayClient, DeviceIdentityService, ChatComposer, ChatSender, CollarCommand, RevocationService);
        PairingService.PairingEnded += QueueRestraintCleanup;
        RevocationService.PairingRevoked += QueueRestraintCleanup;
        CatalogSyncRelayService = new CatalogSyncRelayService(Configuration, RelayClient, DeviceIdentityService, ChatComposer, ChatSender, CatalogSyncService);
        ChatCommandListener = new ChatCommandListener(Configuration, PairingService, CatalogSyncRelayService, TitleCommand, OutfitCommand, GestureCommand, FollowCommand, CollarCommand, MoodlesCommand, RestraintCommand, CustomTriggerCommand);

        PanicHandler = new PanicHandler(PairingService, RevocationService, Configuration, ChatComposer, ChatSender, GlamourerIpc, SlotLockManager, HonorificIpc, MovementLockService, RestrictionRuleManager, RestraintCommand, RuntimeState, CollarCommand);

        CollarWindow = new CollarWindow(this);
        SettingsWindow = new SettingsWindow(this);
        AnimationPickerWindow = new AnimationPickerWindow(this);
        ItemPickerWindow = new ItemPickerWindow(this);
        FavoritesBarButton = new FavoritesBarButton(this);
        WindowSystem.AddWindow(CollarWindow);
        WindowSystem.AddWindow(SettingsWindow);
        WindowSystem.AddWindow(AnimationPickerWindow);
        WindowSystem.AddWindow(ItemPickerWindow);
        WindowSystem.AddWindow(FavoritesBarButton);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the Oathbound window.",
        });
        CommandManager.AddHandler(ShorthandCommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Shorthand for /oathbound - opens the Oathbound window.",
        });
        CommandManager.AddHandler(LegacyCommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Alias for /oathbound - opens the Oathbound window.",
        });
        CommandManager.AddHandler(PanicCommandName, new CommandInfo(OnPanicCommand)
        {
            HelpMessage = "Immediately unpair and revert all collar state. Append your safeword if one is configured in Settings, e.g. /oathboundpanic red.",
        });
        CommandManager.AddHandler(LegacyPanicCommandName, new CommandInfo(OnPanicCommand)
        {
            HelpMessage = "Alias for /oathboundpanic.",
        });
        CommandManager.AddHandler(SettingsCommandName, new CommandInfo(OnSettingsCommand)
        {
            HelpMessage = "Open Oathbound settings (role, pairing, aliases).",
        });
        CommandManager.AddHandler(LegacySettingsCommandName, new CommandInfo(OnSettingsCommand)
        {
            HelpMessage = "Alias for /oathboundsettings.",
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.Draw += FileDialogManager.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += SettingsWindow.Toggle;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Framework.Update += OnFrameworkUpdate;
        ClientState.Login += OnLogin;
        ClientState.Logout += OnLogout;

        // collar/pairing "check the relay at login...": one check at startup, independent of the
        // low-frequency periodic schedule below (which is seeded past its own interval so it doesn't
        // immediately double up on this one).
        nextRevocationCheckUtc = DateTime.UtcNow.AddHours(6);
        FireAndForget(RevocationService.CheckForMissedRevocationAsync(relayBackgroundWorkCts.Token));

        Log.Information("Oathbound loaded.");
    }

    /// collar/pairing "check the relay at login": a fresh login is exactly the "at login" moment the spec
    /// means, distinct from the plugin merely loading (which can happen mid-session on a reload).
    private void OnLogin()
    {
        nextRevocationCheckUtc = DateTime.UtcNow.AddHours(6);
        FireAndForget(RevocationService.CheckForMissedRevocationAsync(relayBackgroundWorkCts.Token));
    }

    /// collar/relay-service "requests stop on logout": cancels any in-flight recurring relay background
    /// work and starts a fresh token source so work resumed after the next login isn't pre-cancelled.
    private void OnLogout(int type, int code)
    {
        RestraintCommand.ReleaseAllBoundAnimationsForPanic();
        RestrictionRuleManager.ReleaseAllForPanic();
        RelayClient.CancelPendingRequests();
        relayBackgroundWorkCts.Cancel();
        relayBackgroundWorkCts.Dispose();
        relayBackgroundWorkCts = new CancellationTokenSource();
    }

    public void Dispose()
    {
        PairingService.PairingEnded -= QueueRestraintCleanup;
        RevocationService.PairingRevoked -= QueueRestraintCleanup;
        RestraintCommand.ReleaseAllBoundAnimationsForPanic();
        Framework.Update -= OnFrameworkUpdate;
        ClientState.Login -= OnLogin;
        ClientState.Logout -= OnLogout;
        relayBackgroundWorkCts.Cancel();
        relayBackgroundWorkCts.Dispose();

        RelayClient.Dispose();

        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.Draw -= FileDialogManager.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= SettingsWindow.Toggle;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        FileDialogManager.Reset();
        WindowSystem.RemoveAllWindows();
        CollarWindow.Dispose();
        SettingsWindow.Dispose();
        AnimationPickerWindow.Dispose();
        ItemPickerWindow.Dispose();
        FavoritesBarButton.Dispose();

        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(ShorthandCommandName);
        CommandManager.RemoveHandler(LegacyCommandName);
        CommandManager.RemoveHandler(PanicCommandName);
        CommandManager.RemoveHandler(LegacyPanicCommandName);
        CommandManager.RemoveHandler(SettingsCommandName);
        CommandManager.RemoveHandler(LegacySettingsCommandName);

        ChatCommandListener.Dispose();
        MovementLockService.Dispose();
        ActionBlockService.Dispose();
        ChatGagService.Dispose();
        SlotLockManager.Dispose();
        GlamourerIpc.Dispose();

        ECommons.ECommonsMain.Dispose();
    }

    private void OnCommand(string command, string args) => ToggleMainUi();

    /// The safeword mechanic (collar/pairing): with no PanicSafeword configured, panic still triggers
    /// unconditionally - an unconfigured safeword must never be the reason panic stops working. With one
    /// configured, the typed word has to match (case-insensitive) - there's no visible button anymore, see
    /// CollarWindow's header.
    private void OnPanicCommand(string command, string args)
    {
        var safeword = Configuration.PanicSafeword;
        if (!string.IsNullOrWhiteSpace(safeword) && !string.Equals(args.Trim(), safeword.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            Log.Information("Panic word didn't match the configured safeword - ignored.");
            return;
        }

        PanicHandler.Panic();
    }

    private void OnSettingsCommand(string command, string args) => SettingsWindow.Toggle();

    public void ToggleSettingsUi() => SettingsWindow.Toggle();

    private void ToggleMainUi() => CollarWindow.Toggle();

    /// collar/ui-organization: lets QuickAccessMenu (or anything else outside CollarWindow) bring the main
    /// window forward already on the Owner tab, without making CollarWindow itself public.
    public void OpenOwnerCommands() => CollarWindow.OpenOwnerTab();

    /// collar/ui-organization "A movable on-screen button opens the quick-access favorites menu": the
    /// menu's "Open main window" control.
    public void OpenMainWindow() => CollarWindow.OpenMainWindow();

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (Interlocked.Exchange(ref pendingRestraintCleanup, 0) != 0)
            RestraintCommand.ForceUnlock();
        // The panic hotkey is a plain edge-detected key check - deliberately simple so it keeps working
        // even if everything else about the plugin (chat parsing, IPC) is broken.
        if (Configuration.PanicHotkey != VirtualKey.NO_KEY)
        {
            var isPressed = KeyState[Configuration.PanicHotkey];
            if (isPressed && !panicHotkeyWasPressed)
                PanicHandler.Panic();
            panicHotkeyWasPressed = isPressed;
        }

        GestureCommand.OnFrameworkUpdate();
        RestraintCommand.OnFrameworkUpdate();
        MovementLockService.OnFrameworkUpdate();
        FollowCommand.OnFrameworkUpdate();
        WalkOnlyService.OnFrameworkUpdate();
        CollarCommand.OnFrameworkUpdate();

        var utcNow = DateTime.UtcNow;
        if (utcNow >= nextRevocationOutboxRetryUtc)
        {
            nextRevocationOutboxRetryUtc = utcNow.AddSeconds(30);
            FireAndForget(RevocationService.RetryOutboxAsync(relayBackgroundWorkCts.Token));
        }
        // collar/pairing "check... on a low-frequency bounded schedule": no more often than every six
        // hours, with jitter so many clients don't all poll on the same clock edge.
        if (utcNow >= nextRevocationCheckUtc)
        {
            nextRevocationCheckUtc = utcNow.AddHours(6).AddSeconds(Random.Shared.Next(0, 1800));
            FireAndForget(RevocationService.CheckForMissedRevocationAsync(relayBackgroundWorkCts.Token));
        }
    }

    private void QueueRestraintCleanup() => Interlocked.Exchange(ref pendingRestraintCleanup, 1);

    private void MigrateConfiguration()
    {
        var follow = Configuration.Aliases.Follow;
        var changed = false;
        if (Configuration.Version < 2)
        {
            Configuration.Version = 2;
            changed = true;
        }
        if (Configuration.Version < 3)
        {
            Configuration.MigrateFolderScopes();
            Configuration.Version = 3;
            changed = true;
        }
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Configuration.PendingRelayOperations.RemoveAll(o => o.ExpiresAt <= now) > 0)
            changed = true;
        if (string.Equals(follow.EngageAlias, "leash-on", StringComparison.Ordinal) &&
            string.Equals(follow.ReleaseAlias, "leash-off", StringComparison.Ordinal))
        {
            follow.EngageAlias = "leash";
            follow.ReleaseAlias = "unleash";
            changed = true;
        }

        foreach (var cmd in Configuration.QuickCommands.Gestures)
        {
            if (cmd.Target is null || !Configuration.GestureMapping.ImportedPeerCatalog.TryGetValue(cmd.Target, out var entry)) continue;
            var readable = $"gesture {CommandSelector.Quote(CommandSelector.GestureSelector(entry, Configuration.GestureMapping.ImportedPeerCatalog.Values))}";
            if (cmd.Command == readable) continue;
            cmd.Command = readable;
            changed = true;
        }
        foreach (var cmd in Configuration.QuickCommands.Moodles)
        {
            if (cmd.Target is null || !cmd.Command.StartsWith("moodle apply ", StringComparison.OrdinalIgnoreCase)) continue;
            var readable = $"moodle apply {CommandSelector.Quote(MoodlesTextFormat.StripMarkup(cmd.Target))}";
            if (cmd.Command == readable) continue;
            cmd.Command = readable;
            changed = true;
        }
        foreach (var cmd in Configuration.QuickCommands.Restraints.Where(c => c.RestraintRules is { Count: > 0 }))
        {
            foreach (var rule in cmd.RestraintRules!.Where(r => r.AnimationId is not null && r.AnimationLabel is null))
                if (Configuration.GestureMapping.ImportedPeerCatalog.TryGetValue(rule.AnimationId!, out var entry))
                    rule.AnimationLabel = CommandSelector.GestureSelector(entry, Configuration.GestureMapping.ImportedPeerCatalog.Values);
            var readable = cmd.RestraintCatalogId is { } catalogId && cmd.RestraintItemId is { } itemId
                ? RestraintCommand.BuildCatalogLockCommand(catalogId, cmd.Label, itemId, cmd.RestraintRules!)
                : RestraintCommand.BuildLockCommand(cmd.Label, cmd.RestraintRules!);
            if (cmd.Command == readable) continue;
            cmd.Command = readable;
            changed = true;
        }
        if (changed) Configuration.Save();
    }

    internal static void FireAndForget(Task task) =>
        _ = task.ContinueWith(t =>
        {
            if (t.Exception is { } ex)
                Log.Error(ex, "Unhandled error in a collar command.");
        }, TaskScheduler.Default);
}
