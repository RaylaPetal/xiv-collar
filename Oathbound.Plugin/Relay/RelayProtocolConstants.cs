namespace Oathbound.Plugin.Relay;

/// Mirrors protocol/constants.json's `sizeAndExpiryLimits` - kept as plain C# constants rather than reading
/// the JSON file at runtime (the Worker can import it directly via its bundler; a Dalamud plugin has no
/// equivalent build-time asset pipeline for a path outside its own project). protocol/vectors and the
/// Worker's own enforcement are the source of truth; these must be kept numerically in sync with them.
public static class RelayProtocolConstants
{
    public const int InvitationExpirySeconds = 900;
    public const int CatalogRequestExpirySeconds = 900;
    public const int CatalogObjectExpirySeconds = 900;
    public const int RevocationRetentionSecondsMax = 604800;
    public const int CatalogPlaintextMaxBytes = 2097152;
    public const int CatalogCiphertextMaxBytes = 786432;
    public const int CatalogCooldownSeconds = 14400;
    public const int RevocationPollMinIntervalSeconds = 21600;
}
