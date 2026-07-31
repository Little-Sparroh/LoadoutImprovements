using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

[HarmonyPatch]
public static class LoadoutIconsPatches
{
    private static readonly Type LoadoutType =
        typeof(PlayerData).GetNestedType("Loadout", BindingFlags.NonPublic);

    private static readonly Type UpgradeEquipDataType = typeof(PlayerData.UpgradeEquipData);

    private static readonly FieldInfo LoadoutUpgradesField =
        LoadoutType?.GetField("upgrades", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo LoadoutIconIndexField =
        LoadoutType?.GetField("iconIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

    [HarmonyPatch(typeof(PlayerData.GearData), "IncrementLoadoutIcon")]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    public static bool IncrementLoadoutIconPrefix(int index, ref object ___loadouts, ref bool __result)
    {
        try
        {
            if (LoadoutType == null || LoadoutIconIndexField == null)
            {
                SparrohPlugin.Logger.LogError("IncrementLoadoutIcon: Loadout type/fields not resolved.");
                __result = false;
                return false;
            }

            LoadoutSlotsPatches.EnsureLoadoutsCapacity(ref ___loadouts, index);

            var loadoutsArray = ___loadouts as Array;
            if (index < 0 || loadoutsArray == null)
            {
                __result = false;
                return false;
            }

            var loadout = loadoutsArray.GetValue(index);

            var upgrades = LoadoutUpgradesField?.GetValue(loadout) as IList;
            if (upgrades == null)
            {
                upgrades = Activator.CreateInstance(typeof(List<>).MakeGenericType(UpgradeEquipDataType), 8) as IList;
                LoadoutUpgradesField?.SetValue(loadout, upgrades);
            }

            var upgradeCount = upgrades.Count;
            var defaultIconCount = Global.Instance != null && Global.Instance.LoadoutIcons != null
                ? Global.Instance.LoadoutIcons.Length
                : 0;
            var totalIcons = Mathf.Max(1, upgradeCount + defaultIconCount);

            var currentIconIndex = LoadoutIconIndexField.GetValue(loadout) is int value ? value : 0;
            var newIconIndex = (currentIconIndex + 1) % totalIcons;
            LoadoutIconIndexField.SetValue(loadout, newIconIndex);

            loadoutsArray.SetValue(loadout, index);

            __result = true;
            return false;
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"IncrementLoadoutIcon failed: {ex}");
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerData.GearData), "GetLoadoutIcon")]
    [HarmonyPostfix]
    public static void GetLoadoutIconPostfix(int index, ref object ___loadouts, ref Sprite __result)
    {
        try
        {
            if (LoadoutType == null || LoadoutUpgradesField == null || LoadoutIconIndexField == null)
                return;

            var loadoutsArray = ___loadouts as Array;
            if (loadoutsArray == null || index < 0 || index >= loadoutsArray.Length)
                return;

            var loadout = loadoutsArray.GetValue(index);
            var upgrades = LoadoutUpgradesField.GetValue(loadout) as IList;
            if (upgrades == null || upgrades.Count == 0)
                return;

            var upgradeCount = upgrades.Count;
            var iconIndex = LoadoutIconIndexField.GetValue(loadout) is int value ? value : 0;

            if (iconIndex < upgradeCount)
            {
                var upgradeIcon = GetUpgradeIconFromEquipData(upgrades[iconIndex]);
                if (upgradeIcon != null)
                {
                    __result = upgradeIcon;
                    return;
                }
            }

            if (Global.Instance?.LoadoutIcons != null)
            {
                var defaultIconIndex = iconIndex - upgradeCount;
                if (defaultIconIndex < 0)
                    defaultIconIndex = 0;

                var loadoutIcons = Global.Instance.LoadoutIcons;
                if (defaultIconIndex >= 0 && defaultIconIndex < loadoutIcons.Length)
                    __result = loadoutIcons[defaultIconIndex];
                else if (loadoutIcons.Length > 0)
                    __result = loadoutIcons[Mathf.Min(iconIndex, loadoutIcons.Length - 1)];
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"GetLoadoutIcon postfix failed: {ex}");
        }
    }

    private static Sprite GetUpgradeIconFromEquipData(object equipData)
    {
        if (equipData == null)
            return null;

        try
        {
            if (equipData is PlayerData.UpgradeEquipData typed)
            {
                var instance = typed.GetUpgrade();
                return instance?.Upgrade?.Icon;
            }

            var getUpgrade = UpgradeEquipDataType?.GetMethod("GetUpgrade", BindingFlags.Public | BindingFlags.Instance);
            var upgradeInstance = getUpgrade?.Invoke(equipData, null) as UpgradeInstance;
            return upgradeInstance?.Upgrade?.Icon;
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Failed to resolve upgrade icon: {ex.Message}");
            return null;
        }
    }
}