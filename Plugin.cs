using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
[BepInDependency("sparroh.uilibrary")]
[MycoMod(null, ModFlags.IsClientSide)]
public class SparrohPlugin : BaseUnityPlugin

{
    public const string PluginGUID = "sparroh.loadoutimprovements";
    public const string PluginName = "LoadoutImprovements";
    public const string PluginVersion = "1.2.1";


    private ConfigEntry<Key> ScrollLeftKey;
    private ConfigEntry<Key> ScrollRightKey;
    private ConfigEntry<Key> LoadoutRenameKey;

    internal static ManualLogSource Logger;
    public static SparrohPlugin Instance;

    private FileSystemWatcher _configWatcher;
    private volatile bool _configDirty;
    private float _configReloadAt = -1f;
    private const float ConfigReloadDebounce = 0.25f;

    private void Awake()
    {
        try
        {
            Logger = base.Logger;
            Instance = this;

            var harmony = new Harmony(PluginGUID);

            try
            {
                LoadoutExpanderMod._loadoutButtonsField = AccessTools.Field(typeof(GearDetailsWindow), "loadoutButtons");
                LoadoutExpanderMod._updateIconMethod = AccessTools.Method(typeof(GearDetailsWindow), "UpdateLoadoutIcon");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to setup LoadoutExpander reflection: {ex.Message}");
            }

            try
            {
                ScrollLeftKey = Config.Bind("Keybinds", "Scroll Loadout Left", Key.Comma, "Key to scroll to the left loadout page");
                LoadoutExpanderMod.ScrollLeftKey = ScrollLeftKey.Value;
                ScrollLeftKey.SettingChanged += (sender, args) => { LoadoutExpanderMod.ScrollLeftKey = ScrollLeftKey.Value; };

                ScrollRightKey = Config.Bind("Keybinds", "Scroll Loadout Right", Key.Period, "Key to scroll to the right loadout page");
                LoadoutExpanderMod.ScrollRightKey = ScrollRightKey.Value;
                ScrollRightKey.SettingChanged += (sender, args) => { LoadoutExpanderMod.ScrollRightKey = ScrollRightKey.Value; };

                LoadoutRenameKey = Config.Bind("Keybinds", "Rename Loadout", Key.R, "Key to rename the hovered loadout");
                LoadoutHoverInfoPatches.RenameKey = LoadoutRenameKey.Value;
                LoadoutRenameKey.SettingChanged += (sender, args) => { LoadoutHoverInfoPatches.RenameKey = LoadoutRenameKey.Value; };

                LoadoutPreviewMod.enableTextMode = Config.Bind("General", "Loadout Preview", true, "If true, show upgrade list on hover");
                LoadoutPreviewMod.enableTextMode.SettingChanged += (sender, args) => LoadoutPreviewMod.OnConfigChanged();
                LoadoutPreviewMod.UpdatePreviewMode();

                SetupConfigHotReload();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to setup configuration bindings: {ex.Message}");
            }

            try
            {
                LoadoutPreviewMod.ApplyPatches(harmony);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to apply LoadoutPreviewMod patches: {ex.Message}");
            }

            try
            {
                harmony.PatchAll();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to apply Harmony patches: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Critical error during mod initialization: {ex.Message}\n{ex.StackTrace}");
        }

        Logger.LogInfo($"{PluginName} loaded successfully.");
    }

    private void SetupConfigHotReload()
    {
        try
        {
            string configPath = Config.ConfigFilePath;
            string directory = Path.GetDirectoryName(configPath);
            string fileName = Path.GetFileName(configPath);

            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
            {
                Logger.LogWarning("Could not resolve config path for hot reload.");
                return;
            }

            _configWatcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };
            _configWatcher.Changed += OnConfigFileChanged;
            _configWatcher.Created += OnConfigFileChanged;
            _configWatcher.Renamed += OnConfigFileChanged;

            Logger.LogInfo($"Config hot reload enabled for {fileName}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to setup config hot reload: {ex.Message}");
        }
    }

    private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        // FileSystemWatcher callbacks are not on the Unity main thread.
        _configDirty = true;
    }

    private void ProcessPendingConfigReload()
    {
        if (_configDirty)
        {
            _configDirty = false;
            _configReloadAt = Time.unscaledTime + ConfigReloadDebounce;
        }

        if (_configReloadAt < 0f || Time.unscaledTime < _configReloadAt)
            return;

        _configReloadAt = -1f;

        try
        {
            Config.Reload();

            // Ensure static consumers stay in sync even if SettingChanged is skipped.
            if (ScrollLeftKey != null)
                LoadoutExpanderMod.ScrollLeftKey = ScrollLeftKey.Value;
            if (ScrollRightKey != null)
                LoadoutExpanderMod.ScrollRightKey = ScrollRightKey.Value;
            if (LoadoutRenameKey != null)
                LoadoutHoverInfoPatches.RenameKey = LoadoutRenameKey.Value;
            if (LoadoutPreviewMod.enableTextMode != null)
                LoadoutPreviewMod.UpdatePreviewMode();

            Logger.LogInfo("Config reloaded from disk.");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to reload config: {ex.Message}");
        }
    }

    private void OnDestroy()
    {
        try
        {
            if (_configWatcher != null)
            {
                _configWatcher.EnableRaisingEvents = false;
                _configWatcher.Changed -= OnConfigFileChanged;
                _configWatcher.Created -= OnConfigFileChanged;
                _configWatcher.Renamed -= OnConfigFileChanged;
                _configWatcher.Dispose();
                _configWatcher = null;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to dispose config watcher: {ex.Message}");
        }

        try
        {
            LoadoutPreviewMod.Destroy();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to destroy LoadoutPreviewMod: {ex.Message}");
        }
    }

    private void Update()
    {
        try
        {
            ProcessPendingConfigReload();

            if (Keyboard.current == null)
                return;

            if (Keyboard.current[LoadoutExpanderMod.ScrollRightKey].wasPressedThisFrame)
            {
                LoadoutExpanderMod.ScrollRight();
            }
            else if (Keyboard.current[LoadoutExpanderMod.ScrollLeftKey].wasPressedThisFrame)
            {
                LoadoutExpanderMod.ScrollLeft();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Critical error in Update(): {ex.Message}");
        }
    }
}

