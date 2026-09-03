using System.Numerics;
using Dalamud.Plugin.Ipc;
using Newtonsoft.Json;

namespace CollarSystem.Plugin.Ipc;

/// Honorific title payload, matching Honorific's own `TitleData` shape (Caraxi/Honorific, CustomTitle.cs)
/// field-for-field so `JsonConvert.DeserializeObject&lt;TitleData&gt;` on Honorific's side round-trips it.
/// Honorific ships no NuGet API package, so this is a hand-rolled mirror of its documented IPC contract
/// rather than a shared type - see design.md's "Honorific IPC" (not "Honorific.Api") framing.
public sealed class HonorificTitleData
{
    public string Title { get; set; } = "";
    public bool IsPrefix { get; set; }
    public Vector3? Color { get; set; }
    public Vector3? Glow { get; set; }
}

/// Thin wrapper around Honorific's IPC surface (`Honorific.SetCharacterTitle` / `ClearCharacterTitle`),
/// always targeting the local player (objectIndex 0) - same "own client only" constraint as GlamourerIpc.
public sealed class HonorificIpc
{
    private const int LocalPlayerObjectIndex = 0;

    private readonly ICallGateSubscriber<int, string, object> setCharacterTitle;
    private readonly ICallGateSubscriber<int, object> clearCharacterTitle;

    public HonorificIpc()
    {
        setCharacterTitle = Plugin.PluginInterface.GetIpcSubscriber<int, string, object>("Honorific.SetCharacterTitle");
        clearCharacterTitle = Plugin.PluginInterface.GetIpcSubscriber<int, object>("Honorific.ClearCharacterTitle");
    }

    public void SetTitle(HonorificTitleData title) =>
        setCharacterTitle.InvokeAction(LocalPlayerObjectIndex, JsonConvert.SerializeObject(title));

    public void ClearTitle() => clearCharacterTitle.InvokeAction(LocalPlayerObjectIndex);
}
