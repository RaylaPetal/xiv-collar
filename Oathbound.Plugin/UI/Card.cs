using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Oathbound.Plugin.UI;

/// A rounded, tinted child region - the plugin's one "grouped panel" primitive, used for the status bar,
/// the pairing/profile card, and each settings section. `using var card = Card.Begin(id, size);`
/// Uses raw ImGui Push/Begin/End calls rather than ImRaii, since ImRaii's child/style scopes are ref
/// structs and can't be composed into one boxed IDisposable.
public sealed class CardScope : IDisposable
{
    private bool disposed;

    internal CardScope(string id, Vector2 size, ImGuiWindowFlags flags)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, Theme.CardRounding);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Theme.CardBg);
        ImGui.BeginChild(id, size, true, flags);
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;

        ImGui.EndChild();
        ImGui.PopStyleColor();
        ImGui.PopStyleVar();
    }
}

public static class Card
{
    /// `noScroll`: for pure-chrome cards (status bar, nav bar) that should never show a scrollbar even
    /// if the fixed height is a few pixels tight - a hard guarantee instead of hoping the height guess
    /// (content + the child's own padding) was exactly right.
    public static CardScope Begin(string id, Vector2 size = default, bool noScroll = false)
    {
        var flags = noScroll ? ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse : ImGuiWindowFlags.None;
        return new CardScope(id, size, flags);
    }
}
