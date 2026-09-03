using System;
using System.Threading.Tasks;
using CollarSystem.Plugin.Commands;
using CollarSystem.Plugin.Config;
using CollarSystem.Plugin.Ipc;
using CollarSystem.Plugin.Safety;
using CollarSystem.Plugin.UI;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace CollarSystem.Plugin;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IKeyState KeyState { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static INotificationManager NotificationManager { get; private set; } = null!;

    private const string CommandName = "/collar";
    private const string PanicCommandName = "/collarpanic";
    private const string SettingsCommandName = "/collarsettings";

    public PluginConfig Configuration { get; }

    public readonly WindowSystem WindowSystem = new("CollarSystem");
    private CollarWindow CollarWindow { get; }
    private SettingsWindow SettingsWindow { get; }
    public AnimationPickerWindow AnimationPickerWindow { get; }

    public SubRuntimeState RuntimeState { get; } = new();

    public GlamourerIpc GlamourerIpc { get; }
    public HonorificIpc HonorificIpc { get; }
    public PenumbraIpc PenumbraIpc { get; }
    public MoodlesIpc MoodlesIpc { get; }
    public MovementLockService MovementLockService { get; }

    public PairingCommand PairingCommand { get; }
    public TitleCommand TitleCommand { get; }
    public OutfitCommand OutfitCommand { get; }
    public GestureCommand GestureCommand { get; }
    public FollowCommand FollowCommand { get; }
    public CollarCommand CollarCommand { get; }
    public MoodlesCommand MoodlesCommand { get; }
    public ChatComposer ChatComposer { get; }
    public ChatSender ChatSender { get; }
    public ChatCommandListener ChatCommandListener { get; }

    public PanicHandler PanicHandler { get; }

    private bool panicHotkeyWasPressed;

    public Plugin()
    {
        ECommons.ECommonsMain.Init(PluginInterface, this);

        Configuration = PluginInterface.GetPluginConfig() as PluginConfig ?? new PluginConfig();

        GlamourerIpc = new GlamourerIpc();
        HonorificIpc = new HonorificIpc();
        PenumbraIpc = new PenumbraIpc();
        MoodlesIpc = new MoodlesIpc();
        MovementLockService = new MovementLockService();

        PairingCommand = new PairingCommand(Configuration);
        TitleCommand = new TitleCommand(HonorificIpc, RuntimeState);
        OutfitCommand = new OutfitCommand(Configuration, GlamourerIpc, RuntimeState);
        GestureCommand = new GestureCommand(Configuration, PenumbraIpc);
        FollowCommand = new FollowCommand(MovementLockService, RuntimeState);
        CollarCommand = new CollarCommand(Configuration, GlamourerIpc, RuntimeState);
        MoodlesCommand = new MoodlesCommand(Configuration, MoodlesIpc);
        ChatComposer = new ChatComposer(Configuration);
        ChatSender = new ChatSender();
        ChatCommandListener = new ChatCommandListener(Configuration, PairingCommand, TitleCommand, OutfitCommand, GestureCommand, FollowCommand, CollarCommand, MoodlesCommand);

        PanicHandler = new PanicHandler(PairingCommand, GlamourerIpc, HonorificIpc, MovementLockService, RuntimeState);

        CollarWindow = new CollarWindow(this);
        SettingsWindow = new SettingsWindow(this);
        AnimationPickerWindow = new AnimationPickerWindow(this);
        WindowSystem.AddWindow(CollarWindow);
        WindowSystem.AddWindow(SettingsWindow);
        WindowSystem.AddWindow(AnimationPickerWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the Collar System window.",
        });
        CommandManager.AddHandler(PanicCommandName, new CommandInfo(OnPanicCommand)
        {
            HelpMessage = "Immediately unpair and revert all collar state. Append your safeword if one is configured in Settings, e.g. /collarpanic red.",
        });
        CommandManager.AddHandler(SettingsCommandName, new CommandInfo(OnSettingsCommand)
        {
            HelpMessage = "Open Collar System settings (role, pairing, aliases).",
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += SettingsWindow.Toggle;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Framework.Update += OnFrameworkUpdate;

        Log.Information("Collar System loaded.");
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;

        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= SettingsWindow.Toggle;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();
        CollarWindow.Dispose();
        SettingsWindow.Dispose();
        AnimationPickerWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(PanicCommandName);
        CommandManager.RemoveHandler(SettingsCommandName);

        ChatCommandListener.Dispose();
        MovementLockService.Dispose();

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
            Log.Information("/collarpanic word didn't match the configured safeword - ignored.");
            return;
        }

        PanicHandler.Panic();
    }

    private void OnSettingsCommand(string command, string args) => SettingsWindow.Toggle();

    public void ToggleSettingsUi() => SettingsWindow.Toggle();

    private void ToggleMainUi() => CollarWindow.Toggle();

    private void OnFrameworkUpdate(IFramework framework)
    {
        // The panic hotkey is a plain edge-detected key check - deliberately simple so it keeps working
        // even if everything else about the plugin (chat parsing, IPC) is broken.
        if (Configuration.PanicHotkey == VirtualKey.NO_KEY)
            return;

        var isPressed = KeyState[Configuration.PanicHotkey];
        if (isPressed && !panicHotkeyWasPressed)
            PanicHandler.Panic();
        panicHotkeyWasPressed = isPressed;
    }

    internal static void FireAndForget(Task task) =>
        _ = task.ContinueWith(t =>
        {
            if (t.Exception is { } ex)
                Log.Error(ex, "Unhandled error in a collar command.");
        }, TaskScheduler.Default);
}
