using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine.InputSystem;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
[BepInDependency("sparroh.uilibrary")]
[MycoMod(null, ModFlags.IsClientSide)]
public class SparrohPlugin : BaseUnityPlugin
{
    public const string PluginGUID = "sparroh.loadoutimprovements";
    public const string PluginName = "LoadoutImprovements";
    public const string PluginVersion = "1.2.2";

    internal static ManualLogSource Logger;
    public static SparrohPlugin Instance;

    private void Awake()
    {
        try
        {
            Logger = base.Logger;
            Instance = this;

            var harmony = new Harmony(PluginGUID);

            try
            {
                LoadoutExpanderMod._loadoutButtonsField =
                    AccessTools.Field(typeof(GearDetailsWindow), "loadoutButtons");
                LoadoutExpanderMod._updateIconMethod =
                    AccessTools.Method(typeof(GearDetailsWindow), "UpdateLoadoutIcon");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to setup LoadoutExpander reflection: {ex.Message}");
            }

            try
            {
                ConfigManager.Initialize(Config, Logger);
                LoadoutPreviewMod.UpdatePreviewMode();
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

    private void Update()
    {
        try
        {
            ConfigManager.Tick();

            if (Keyboard.current == null)
                return;

            if (Keyboard.current[LoadoutExpanderMod.ScrollRightKey].wasPressedThisFrame)
                LoadoutExpanderMod.ScrollRight();
            else if (Keyboard.current[LoadoutExpanderMod.ScrollLeftKey].wasPressedThisFrame)
                LoadoutExpanderMod.ScrollLeft();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Critical error in Update(): {ex.Message}");
        }
    }

    private void OnDestroy()
    {
        try
        {
            ConfigManager.Dispose();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to dispose config manager: {ex.Message}");
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
}