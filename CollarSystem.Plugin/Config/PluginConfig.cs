using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.Game.ClientState.Keys;

namespace CollarSystem.Plugin.Config;

public enum PluginRole
{
    Owner,
    Sub,
}

[Serializable]
public class PairingState
{
    public string? PairingId { get; set; }
    public string? PeerName { get; set; }
    public bool Confirmed { get; set; }

    public bool IsPaired => PairingId != null && Confirmed;
}

[Serializable]
public class PermissionSet
{
    public bool Title { get; set; }
    public bool Outfit { get; set; }
    public bool Gesture { get; set; }

    // Separate, higher-risk opt-in per collar/follow's spec - never implied by the other three.
    public bool Follow { get; set; }
}

[Serializable]
public class PluginConfig : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public PluginRole Role { get; set; } = PluginRole.Sub;
    public string RelayUrl { get; set; } = "ws://localhost:5099/collar";

    public PairingState Pairing { get; set; } = new();
    public PermissionSet Permissions { get; set; } = new();
    public GestureMapping GestureMapping { get; set; } = new();

    /// The always-available local panic hotkey (collar/pairing). NO_KEY means "not bound" - the
    /// /collarpanic command always works regardless of this setting.
    public VirtualKey PanicHotkey { get; set; } = VirtualKey.NO_KEY;

    /// Sub-side: mod folders (Penumbra "Approved for <role>"-style paths) that gesture scanning is scoped to.
    /// Empty means "no folders selected" - gesture scanning finds nothing until the Sub opts folders in.
    public List<string> GestureFolderAllowlist { get; set; } = new();

    /// Gate per collar/gesture and collar/follow's ToS-disclosure requirement: the Sub must acknowledge
    /// the automation-risk caveat before either permission can be enabled.
    public bool TosAcknowledged { get; set; }

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
