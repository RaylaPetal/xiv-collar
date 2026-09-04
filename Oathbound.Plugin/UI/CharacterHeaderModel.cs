using ECommons.GameHelpers;

namespace Oathbound.Plugin.UI;

/// A display-only snapshot rebuilt for every rendered frame. Nothing here is persisted or cached, so
/// logout, zoning, and character changes cannot leak the previous character into the header.
internal readonly record struct CharacterHeaderModel(string? Name, string? HomeWorld, string? FreeCompany)
{
    public bool IsAvailable => !string.IsNullOrWhiteSpace(Name);

    public static CharacterHeaderModel Current()
    {
        var player = Player.Object;
        if (player is null)
            return default;

        var name = player.Name.TextValue;
        var world = player.HomeWorld.IsValid ? player.HomeWorld.Value.Name.ExtractText() : null;
        var company = player.CompanyTag.TextValue;
        return new CharacterHeaderModel(
            string.IsNullOrWhiteSpace(name) ? null : name,
            string.IsNullOrWhiteSpace(world) ? null : world,
            string.IsNullOrWhiteSpace(company) ? null : company);
    }
}
