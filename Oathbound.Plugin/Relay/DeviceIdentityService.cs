using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Oathbound.Plugin.Config;

namespace Oathbound.Plugin.Relay;

/// collar/pairing "Device-key lifecycle is recoverable and explicit". Generates and protects this
/// installation's persistent ECDSA P-256 signing identity. See protocol/docs/threat-model.md for why DPAPI
/// provides no real guarantee under Wine - this class still calls it unconditionally (it costs nothing and
/// helps on native Windows), but never claims the key is "protected" in any user-facing text; that
/// disclosure lives in Settings, not here.
public sealed class DeviceIdentityService
{
    private readonly PluginConfig config;
    private RelayEcKeyPair? cachedKey;

    public DeviceIdentityService(PluginConfig config)
    {
        this.config = config;
    }

    public string? DeviceKeyId => config.DeviceIdentity.DeviceKeyId;
    public bool HasIdentity => config.DeviceIdentity.HasIdentity;

    /// Generates a fresh identity if none exists yet; a no-op otherwise. Called once at plugin startup.
    public void EnsureIdentity()
    {
        if (config.DeviceIdentity.HasIdentity) return;
        GenerateAndPersist();
    }

    /// collar/pairing "User resets the device identity": generates a brand-new identity, invalidating every
    /// relay-assisted pairing this side held (the old device key id no longer matches anything server-side
    /// or in the peer's own PeerDeviceKeyId). Callers are responsible for locally ending any active pairing
    /// as part of the same user-confirmed action - this method only replaces the key.
    public void ResetIdentity()
    {
        cachedKey?.Dispose();
        cachedKey = null;
        GenerateAndPersist();
    }

    /// Returns the live signing key, importing the protected private scalar on first use. Throws if no
    /// identity exists yet (callers must EnsureIdentity() at startup) or if the protected blob cannot be
    /// unprotected (e.g. it was written on a different Windows user profile) - in that case the caller
    /// should surface a reset prompt, never silently regenerate out from under an existing pairing.
    public RelayEcKeyPair GetSigningKey()
    {
        if (cachedKey is not null) return cachedKey;

        var identity = config.DeviceIdentity;
        if (!identity.HasIdentity)
            throw new InvalidOperationException("No device identity exists yet; call EnsureIdentity() first.");

        var privateD = Unprotect(identity.ProtectedPrivateKey!);
        var publicKeyJwk = new EcPublicKeyJwk { Kty = "EC", Crv = "P-256", X = identity.PublicKeyX!, Y = identity.PublicKeyY! };
        cachedKey = RelayCrypto.ImportSigningPrivateKey(publicKeyJwk, privateD);
        return cachedKey;
    }

    public EcPublicKeyJwk GetPublicKeyJwk()
    {
        var identity = config.DeviceIdentity;
        if (!identity.HasIdentity)
            throw new InvalidOperationException("No device identity exists yet; call EnsureIdentity() first.");
        return new EcPublicKeyJwk { Kty = "EC", Crv = "P-256", X = identity.PublicKeyX!, Y = identity.PublicKeyY! };
    }

    private void GenerateAndPersist()
    {
        using var key = RelayCrypto.GenerateSigningKeyPair();
        var publicKeyJwk = RelayCrypto.ExportPublicKeyJwk(key);
        var privateD = RelayCrypto.ExportPrivateD(key);

        config.DeviceIdentity.PublicKeyX = publicKeyJwk.X;
        config.DeviceIdentity.PublicKeyY = publicKeyJwk.Y;
        config.DeviceIdentity.ProtectedPrivateKey = Protect(privateD);
        config.DeviceIdentity.DeviceKeyId = RelayCrypto.DeviceKeyId(publicKeyJwk);
        config.Save();

        Array.Clear(privateD);
        cachedKey?.Dispose();
        cachedKey = null;
    }

    private static byte[] Protect(byte[] plaintext)
    {
        if (!OperatingSystem.IsWindows()) return plaintext;
        try
        {
            return ProtectedData.Protect(plaintext, s_entropy, DataProtectionScope.CurrentUser);
        }
        catch (Exception ex) when (ex is CryptographicException or PlatformNotSupportedException)
        {
            // Best-available protection only: under Wine, or if the profile's DPAPI master key is
            // unavailable, fall back to storing the plain scalar rather than failing to create an identity
            // at all. See protocol/docs/threat-model.md - this is documented, not a silent weakening.
            Plugin.Log.Warning(ex, "DPAPI protection unavailable; storing the device private key without OS-level protection.");
            return plaintext;
        }
    }

    private static byte[] Unprotect(byte[] stored)
    {
        if (!OperatingSystem.IsWindows()) return stored;
        try
        {
            return ProtectedData.Unprotect(stored, s_entropy, DataProtectionScope.CurrentUser);
        }
        catch (CryptographicException)
        {
            // Was stored unprotected (Protect() fell back above) - return as-is.
            return stored;
        }
    }

    private static readonly byte[] s_entropy = "oathbound-device-identity-v1"u8.ToArray();
}
