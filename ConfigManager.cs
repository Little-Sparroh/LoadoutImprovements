using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.InputSystem;

public static class ConfigManager
{
    private const float DebounceSeconds = 0.25f;

    private static ConfigFile config;
    private static ManualLogSource logger;
    private static FileSystemWatcher configWatcher;
    private static volatile bool reloadPending;
    private static float lastReloadTime;
    public static ConfigEntry<Key> ScrollLeftKey { get; private set; }
    public static ConfigEntry<Key> ScrollRightKey { get; private set; }
    public static ConfigEntry<Key> RenameKey { get; private set; }
    public static ConfigEntry<bool> EnableTextMode { get; private set; }

    public static void Initialize(ConfigFile configFile, ManualLogSource log)
    {
        config = configFile;
        logger = log;

        ScrollLeftKey = config.Bind(
            "Keybinds",
            "Scroll Loadout Left",
            Key.Comma,
            "Key to scroll to the left loadout page");

        ScrollRightKey = config.Bind(
            "Keybinds",
            "Scroll Loadout Right",
            Key.Period,
            "Key to scroll to the right loadout page");

        RenameKey = config.Bind(
            "Keybinds",
            "Rename Loadout",
            Key.R,
            "Key to rename the hovered loadout");

        EnableTextMode = config.Bind(
            "General",
            "Loadout Preview",
            true,
            "If true, show upgrade list on hover");

        ApplyToMods();

        ScrollLeftKey.SettingChanged += OnSettingChanged;
        ScrollRightKey.SettingChanged += OnSettingChanged;
        RenameKey.SettingChanged += OnSettingChanged;
        EnableTextMode.SettingChanged += OnTextModeChanged;

        try
        {
            SetupFileWatcher();
        }
        catch (Exception ex)
        {
            logger.LogError($"Error setting up config file watcher: {ex.Message}");
        }
    }


    public static void Tick()
    {
        if (!reloadPending)
            return;

        if (Time.unscaledTime - lastReloadTime < DebounceSeconds)
            return;

        reloadPending = false;
        lastReloadTime = Time.unscaledTime;

        try
        {
            config.Reload();
            ApplyToMods();
            LoadoutPreviewMod.UpdatePreviewMode();
            logger.LogInfo("Config reloaded from disk.");
        }
        catch (Exception ex)
        {
            logger.LogError($"Error reloading config: {ex.Message}");
        }
    }

    public static void Dispose()
    {
        if (ScrollLeftKey != null)
            ScrollLeftKey.SettingChanged -= OnSettingChanged;
        if (ScrollRightKey != null)
            ScrollRightKey.SettingChanged -= OnSettingChanged;
        if (RenameKey != null)
            RenameKey.SettingChanged -= OnSettingChanged;
        if (EnableTextMode != null)
            EnableTextMode.SettingChanged -= OnTextModeChanged;

        if (configWatcher != null)
        {
            configWatcher.EnableRaisingEvents = false;
            configWatcher.Changed -= OnConfigFileChanged;
            configWatcher.Created -= OnConfigFileChanged;
            configWatcher.Renamed -= OnConfigFileChanged;
            configWatcher.Dispose();
            configWatcher = null;
        }
    }

    private static void ApplyToMods()
    {
        if (ScrollLeftKey != null)
            LoadoutExpanderMod.ScrollLeftKey = ScrollLeftKey.Value;
        if (ScrollRightKey != null)
            LoadoutExpanderMod.ScrollRightKey = ScrollRightKey.Value;
        if (RenameKey != null)
            LoadoutHoverInfoPatches.RenameKey = RenameKey.Value;


        if (EnableTextMode != null)
            LoadoutPreviewMod.enableTextMode = EnableTextMode;
    }

    private static void SetupFileWatcher()
    {
        configWatcher = new FileSystemWatcher(Paths.ConfigPath, $"{SparrohPlugin.PluginGUID}.cfg");
        configWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName;
        configWatcher.Changed += OnConfigFileChanged;
        configWatcher.Created += OnConfigFileChanged;
        configWatcher.Renamed += OnConfigFileChanged;
        configWatcher.EnableRaisingEvents = true;
        logger.LogInfo($"Config hot reload enabled for {SparrohPlugin.PluginGUID}.cfg");
    }

    private static void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        reloadPending = true;
    }

    private static void OnSettingChanged(object sender, EventArgs e)
    {
        ApplyToMods();
    }

    private static void OnTextModeChanged(object sender, EventArgs e)
    {
        ApplyToMods();
        LoadoutPreviewMod.OnConfigChanged();
    }
}