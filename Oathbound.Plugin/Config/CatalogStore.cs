using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Oathbound.Plugin.Config;

/// collar/config-performance "Catalogs live outside the hot-saved config file" (split-catalog-storage-from-
/// config): Penumbra/gesture/moodle scan results and imported peer catalogs used to live inline on
/// PluginConfig, so every config.Save() call anywhere in the plugin - including switching the Active Pairing
/// dropdown - re-serialized and rewrote them even though nothing about them changed. This owns their own
/// file instead, written only by the scan/import call sites that actually mutate a catalog. See this
/// change's design.md for the full rationale, including the [JsonExtensionData]-based migration below.
public sealed class CatalogStore
{
    private const string FileName = "catalogs.json";

    private static string FilePath => Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, FileName);

    /// Populates `config`'s catalog properties from disk, migrating a pre-upgrade install's inline catalogs
    /// (captured by GetPluginConfig() into each mapping's LegacyExtensionData) into the sidecar file the
    /// first time this runs. A no-op read-through on every later launch. Never throws - worst case on a
    /// missing/corrupt/unmigratable file is empty catalogs, which costs the user a rescan, not a failed load.
    public void LoadOrMigrate(PluginConfig config)
    {
        if (!File.Exists(FilePath))
        {
            MigrateLegacy(config);
            return;
        }

        LoadInto(config);
    }

    /// Snapshots every mapping's current in-memory catalog off `config` and writes it, via a temp-file-then-
    /// replace so a crash mid-write can never leave a truncated/corrupt catalogs.json behind.
    public void Save(PluginConfig config)
    {
        var data = new CatalogStoreData
        {
            GestureLocal = config.GestureMapping.LocalCatalog,
            GesturePeer = config.GestureMapping.ImportedPeerCatalog,
            RestraintLocal = config.RestraintMapping.LocalCatalog,
            RestraintPeer = config.RestraintMapping.ImportedPeerCatalog,
            MoodlesLocal = config.MoodlesMapping.LocalCatalog,
        };

        var directory = Plugin.PluginInterface.ConfigDirectory;
        if (!directory.Exists) directory.Create();

        var tempPath = FilePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(data));
        if (File.Exists(FilePath))
            File.Replace(tempPath, FilePath, null);
        else
            File.Move(tempPath, FilePath);
    }

    private void LoadInto(PluginConfig config)
    {
        CatalogStoreData data;
        try
        {
            var json = File.ReadAllText(FilePath);
            data = JsonSerializer.Deserialize<CatalogStoreData>(json) ?? new CatalogStoreData();
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Failed to read catalogs.json; starting with empty catalogs (a rescan will repopulate them).");
            data = new CatalogStoreData();
        }

        config.GestureMapping.LocalCatalog = data.GestureLocal;
        config.GestureMapping.ImportedPeerCatalog = data.GesturePeer;
        config.RestraintMapping.LocalCatalog = data.RestraintLocal;
        config.RestraintMapping.ImportedPeerCatalog = data.RestraintPeer;
        config.MoodlesMapping.LocalCatalog = data.MoodlesLocal;
    }

    private void MigrateLegacy(PluginConfig config)
    {
        TryAssign<GestureCatalogEntry>(config.GestureMapping.LegacyExtensionData, "LocalCatalog", v => config.GestureMapping.LocalCatalog = v);
        TryAssign<GestureExportEntry>(config.GestureMapping.LegacyExtensionData, "ImportedPeerCatalog", v => config.GestureMapping.ImportedPeerCatalog = v);
        TryAssign<RestraintCatalogEntry>(config.RestraintMapping.LegacyExtensionData, "LocalCatalog", v => config.RestraintMapping.LocalCatalog = v);
        TryAssign<RestraintCatalogExportEntry>(config.RestraintMapping.LegacyExtensionData, "ImportedPeerCatalog", v => config.RestraintMapping.ImportedPeerCatalog = v);
        TryAssign<MoodlesStatusEntry>(config.MoodlesMapping.LegacyExtensionData, "LocalCatalog", v => config.MoodlesMapping.LocalCatalog = v);

        // Never let the legacy inline data round-trip back into the main config on a later config.Save().
        config.GestureMapping.LegacyExtensionData = null;
        config.RestraintMapping.LegacyExtensionData = null;
        config.MoodlesMapping.LegacyExtensionData = null;

        Save(config);
    }

    private static void TryAssign<T>(Dictionary<string, JsonElement>? extensionData, string propertyName, Action<Dictionary<string, T>> assign)
    {
        if (extensionData is null) return;
        var key = extensionData.Keys.FirstOrDefault(k => string.Equals(k, propertyName, StringComparison.OrdinalIgnoreCase));
        if (key is null) return;

        try
        {
            var value = JsonSerializer.Deserialize<Dictionary<string, T>>(extensionData[key]);
            if (value is not null) assign(value);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, $"Failed to migrate legacy '{propertyName}' catalog; it will need a rescan.");
        }
    }

    private sealed class CatalogStoreData
    {
        public Dictionary<string, GestureCatalogEntry> GestureLocal { get; set; } = new();
        public Dictionary<string, GestureExportEntry> GesturePeer { get; set; } = new();
        public Dictionary<string, RestraintCatalogEntry> RestraintLocal { get; set; } = new();
        public Dictionary<string, RestraintCatalogExportEntry> RestraintPeer { get; set; } = new();
        public Dictionary<string, MoodlesStatusEntry> MoodlesLocal { get; set; } = new();
    }
}
