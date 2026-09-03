using System;
using System.Threading.Tasks;
using CollarSystem.Plugin.Commands;
using CollarSystem.Plugin.Config;
using CollarSystem.Plugin.Ipc;
using CollarSystem.Plugin.Relay;
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
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IKeyState KeyState { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/collar";
    private const string PanicCommandName = "/collarpanic";
    private const string SettingsCommandName = "/collarsettings";

    public PluginConfig Configuration { get; }

    public readonly WindowSystem WindowSystem = new("CollarSystem");
    private DomWindow DomWindow { get; }
    private SubWindow SubWindow { get; }
    private SettingsWindow SettingsWindow { get; }

    public RelayClient Relay { get; }
    public SubRuntimeState RuntimeState { get; } = new();

    public GlamourerIpc GlamourerIpc { get; }
    public HonorificIpc HonorificIpc { get; }
    public PenumbraIpc PenumbraIpc { get; }
    public MovementLockService MovementLockService { get; }

    public PairingCommand PairingCommand { get; }
    public TitleCommand TitleCommand { get; }
    public OutfitCommand OutfitCommand { get; }
    public GestureCommand GestureCommand { get; }
    public FollowCommand FollowCommand { get; }
    private CommandDispatcher Dispatcher { get; }

    public PanicHandler PanicHandler { get; }

    public string? IncomingPairingRequestFrom { get; set; }

    private bool panicHotkeyWasPressed;

    public Plugin()
    {
        ECommons.ECommonsMain.Init(PluginInterface, this);

        Configuration = PluginInterface.GetPluginConfig() as PluginConfig ?? new PluginConfig();

        Relay = new RelayClient();

        GlamourerIpc = new GlamourerIpc();
        HonorificIpc = new HonorificIpc();
        PenumbraIpc = new PenumbraIpc();
        MovementLockService = new MovementLockService();

        PairingCommand = new PairingCommand(Configuration, Relay);
        TitleCommand = new TitleCommand(Configuration, Relay, HonorificIpc, RuntimeState);
        OutfitCommand = new OutfitCommand(Configuration, Relay, GlamourerIpc, RuntimeState);
        GestureCommand = new GestureCommand(Configuration, Relay, PenumbraIpc);
        FollowCommand = new FollowCommand(Configuration, Relay, MovementLockService, RuntimeState);
        Dispatcher = new CommandDispatcher(Configuration, Relay, PairingCommand, TitleCommand, OutfitCommand, GestureCommand, FollowCommand);

        PanicHandler = new PanicHandler(PairingCommand, GlamourerIpc, HonorificIpc, MovementLockService, RuntimeState);

        PairingCommand.IncomingPairingRequest += peerName => IncomingPairingRequestFrom = peerName;
        PairingCommand.PairingConfirmed += () => IncomingPairingRequestFrom = null;
        PairingCommand.PairingEnded += () => IncomingPairingRequestFrom = null;

        DomWindow = new DomWindow(this);
        SubWindow = new SubWindow(this);
        SettingsWindow = new SettingsWindow(this);
        WindowSystem.AddWindow(DomWindow);
        WindowSystem.AddWindow(SubWindow);
        WindowSystem.AddWindow(SettingsWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the Collar System window for your configured role (Owner/Sub).",
        });
        CommandManager.AddHandler(PanicCommandName, new CommandInfo(OnPanicCommand)
        {
            HelpMessage = "Immediately unpair and revert all collar state.",
        });
        CommandManager.AddHandler(SettingsCommandName, new CommandInfo(OnSettingsCommand)
        {
            HelpMessage = "Open Collar System settings (role, relay URL).",
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
        DomWindow.Dispose();
        SubWindow.Dispose();
        SettingsWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(PanicCommandName);
        CommandManager.RemoveHandler(SettingsCommandName);

        Dispatcher.Dispose();
        MovementLockService.Dispose();
        Relay.Dispose();

        ECommons.ECommonsMain.Dispose();
    }

    private void OnCommand(string command, string args) => ToggleMainUi();

    private void OnPanicCommand(string command, string args) => PanicHandler.Panic();

    private void OnSettingsCommand(string command, string args) => SettingsWindow.Toggle();

    public void ToggleSettingsUi() => SettingsWindow.Toggle();

    private void ToggleMainUi()
    {
        if (Configuration.Role == PluginRole.Owner)
            DomWindow.Toggle();
        else
            SubWindow.Toggle();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        // The panic hotkey is a plain edge-detected key check - deliberately simple so it keeps working
        // even if everything else about the plugin (relay, IPC) is broken.
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
