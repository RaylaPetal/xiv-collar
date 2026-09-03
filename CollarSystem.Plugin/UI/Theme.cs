using System.Numerics;

namespace CollarSystem.Plugin.UI;

/// One place to tune the plugin's look - every window/widget reads colors and rounding from here instead
/// of hardcoding its own, so the dark/card-based style (loosely inspired by GagSpeak's layout: a status
/// bar, a status card, a grid of module tiles) stays consistent as more UI gets added later.
public static class Theme
{
    public static readonly Vector4 Accent = new(0.62f, 0.38f, 0.85f, 1f);
    public static readonly Vector4 AccentHover = new(0.72f, 0.48f, 0.95f, 1f);

    public static readonly Vector4 CardBg = new(0.13f, 0.13f, 0.17f, 1f);
    public static readonly Vector4 TileBg = new(0.18f, 0.18f, 0.23f, 1f);
    public static readonly Vector4 TileBgHover = new(0.27f, 0.21f, 0.34f, 1f);

    public static readonly Vector4 TextMuted = new(0.62f, 0.62f, 0.68f, 1f);
    public static readonly Vector4 Success = new(0.35f, 0.85f, 0.35f, 1f);
    public static readonly Vector4 Warning = new(0.9f, 0.72f, 0.25f, 1f);
    public static readonly Vector4 Danger = new(0.65f, 0.42f, 0.42f, 1f);

    /// The single saturated, high-alarm red. Reserved for the panic button alone (design.md's "distinct
    /// from panic-red" decision) - nothing else in the plugin uses this exact color.
    public static readonly Vector4 PanicRed = new(0.7f, 0.1f, 0.1f, 1f);
    public static readonly Vector4 PanicRedHover = new(0.85f, 0.15f, 0.15f, 1f);

    public const float CardRounding = 8f;
    public const float TileRounding = 6f;
}
