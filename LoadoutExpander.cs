using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

public static class LoadoutExpanderMod
{
    public static int PageOffset;

    internal static FieldInfo _loadoutButtonsField;
    internal static FieldInfo _upgradableField;
    internal static MethodInfo _updateIconMethod;

    public static Key ScrollLeftKey;
    public static Key ScrollRightKey;

    public static void TogglePage()
    {
        PageOffset += 3;
        if (PageOffset > 6) PageOffset = 0;

        RefreshCurrentWindow();
    }

    public static void ScrollRight()
    {
        PageOffset += 3;
        if (PageOffset > 6) PageOffset = 0;

        RefreshCurrentWindow();
    }

    public static void ScrollLeft()
    {
        PageOffset -= 3;
        if (PageOffset < 0) PageOffset = 6;

        RefreshCurrentWindow();
    }

    public static void RefreshCurrentWindow()
    {
        try
        {
            var windows = Resources.FindObjectsOfTypeAll<GearDetailsWindow>();
            foreach (var window in windows)
                if (window.gameObject.activeInHierarchy && _loadoutButtonsField != null && _updateIconMethod != null)
                {
                    var buttons = _loadoutButtonsField.GetValue(window) as Array;
                    if (buttons != null)
                    {
                        var count = Mathf.Min(buttons.Length, 3);
                        for (var i = 0; i < count; i++)
                        {
                            var btn = buttons.GetValue(i);
                            _updateIconMethod.Invoke(window, new[] { btn, i });
                        }
                    }
                }
        }
        catch (Exception e)
        {
            SparrohPlugin.Logger.LogError("Error refreshing window: " + e.Message);
        }
    }
}

[HarmonyPatch]
public static class GearDetailsWindowPatches
{
    [HarmonyPatch(typeof(GearDetailsWindow), "Setup")]
    [HarmonyPostfix]
    public static void SetupPostfix(ref GearDetailsWindow __instance, IUpgradable upgradable)
    {
        LoadoutExpanderMod.PageOffset = 0;
        LoadoutExpanderMod.RefreshCurrentWindow();

        try
        {
            var playerDataType = typeof(PlayerData);
            var instanceProp = playerDataType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            var playerDataInstance = instanceProp?.GetValue(null);

            var getGearDataMethod = playerDataType.GetMethod("GetGearData", new[] { typeof(IUpgradable) });
            var gearData = getGearDataMethod?.Invoke(playerDataInstance, new object[] { upgradable });

            if (gearData != null)
            {
                var gearDataType = gearData.GetType();
                var loadoutsField = gearDataType.GetField("loadouts", BindingFlags.NonPublic | BindingFlags.Instance);
                if (loadoutsField != null)
                {
                    var currentLoadouts = loadoutsField.GetValue(gearData) as Array;
                    var loadoutType = playerDataType.GetNestedType("Loadout", BindingFlags.NonPublic);

                    if (currentLoadouts == null || currentLoadouts.Length < LoadoutSlotsPatches.MaxLoadoutSlots)
                    {
                        var newLoadouts = Array.CreateInstance(loadoutType, LoadoutSlotsPatches.MaxLoadoutSlots);
                        if (currentLoadouts != null)
                            currentLoadouts.CopyTo(newLoadouts, 0);
                        loadoutsField.SetValue(gearData, newLoadouts);
                    }
                }
            }
        }
        catch (Exception)
        {
        }

        var loadoutButtonsField = __instance.GetType()
            .GetField("loadoutButtons", BindingFlags.NonPublic | BindingFlags.Instance);
        if (loadoutButtonsField != null)
        {
            var loadoutButtons = loadoutButtonsField.GetValue(__instance) as Array;

            if (loadoutButtons != null)
            {
                var existingButtons = loadoutButtons.Length;
                for (var l = 0; l < Mathf.Min(LoadoutSlotsPatches.MaxLoadoutSlots, existingButtons); l++)
                {
                    var button = loadoutButtons.GetValue(l);
                    if (button != null)
                    {
                        var gameObject = button.GetType().GetProperty("gameObject")?.GetValue(button);
                        if (gameObject != null)
                            gameObject.GetType().GetMethod("SetActive")?.Invoke(gameObject, new object[] { true });
                    }
                }
            }
        }

        try
        {
            if (upgradable != null)
            {
                var playerDataType = typeof(PlayerData);
                var instanceProp = playerDataType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                var playerDataInstance = instanceProp?.GetValue(null);

                var getGearDataMethod = playerDataType.GetMethod("GetGearData", new[] { typeof(IUpgradable) });
                var gearData = getGearDataMethod?.Invoke(playerDataInstance, new object[] { upgradable });

                if (gearData != null)
                {
                    var gear = gearData.GetType().GetField("gear", BindingFlags.NonPublic | BindingFlags.Instance)
                        ?.GetValue(gearData) as IUpgradable;
                    if (gear != null)
                    {
                        if (!LoadoutHoverInfoPatches.windowLoadoutNames.TryGetValue(__instance,
                                out var windowNamesDict))
                        {
                            windowNamesDict = new Dictionary<int, string>();
                            LoadoutHoverInfoPatches.windowLoadoutNames[__instance] = windowNamesDict;
                        }

                        for (var i = 0; i < LoadoutSlotsPatches.MaxLoadoutSlots; i++)
                        {
                            var key = $"{gear.Info.ID}_{i}";
                            var savedName = PlayerPrefs.GetString("LoadoutName_" + key, "");
                            if (!string.IsNullOrEmpty(savedName)) windowNamesDict[i] = savedName;
                        }
                    }
                }
            }
        }
        catch (Exception)
        {
        }
    }
}

[HarmonyPatch(typeof(GearDetailsWindow), "UpdateLoadoutIcon")]
public static class UpdateIconPatch
{
    [HarmonyPrefix]
    public static void Prefix(ref int index)
    {
        if (index < 3) index += LoadoutExpanderMod.PageOffset;
    }
}

[HarmonyPatch(typeof(PlayerData.GearData), "EquipLoadout")]
public static class EquipLoadoutPatch
{
    [HarmonyPrefix]
    public static void Prefix(ref int index)
    {
        if (index < 3) index += LoadoutExpanderMod.PageOffset;
    }
}

[HarmonyPatch(typeof(PlayerData.GearData), "IncrementLoadoutIcon")]
public static class IncrementIconPatch
{
    [HarmonyPrefix]
    public static void Prefix(ref int index)
    {
        if (index < 3) index += LoadoutExpanderMod.PageOffset;
    }
}

[HarmonyPatch(typeof(PlayerData.GearData), "SaveLoadout")]
public static class SaveLoadoutPatch
{
    [HarmonyPrefix]
    public static void Prefix(ref int index)
    {
        if (index < 3) index += LoadoutExpanderMod.PageOffset;
    }
}