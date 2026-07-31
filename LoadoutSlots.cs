using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

[HarmonyPatch]
public static class LoadoutSlotsPatches
{
    internal const int MaxLoadoutSlots = 9;

    private static readonly Type LoadoutType =
        typeof(PlayerData).GetNestedType("Loadout", BindingFlags.NonPublic);

    [HarmonyPatch(typeof(PlayerData.GearData), "SaveLoadout")]
    [HarmonyPrefix]
    public static bool SaveLoadoutPrefix(ref PlayerData.GearData __instance, int index, ref object ___loadouts)
    {
        EnsureLoadoutsCapacity(ref ___loadouts, index);
        return true;
    }

    [HarmonyPatch(typeof(PlayerData.GearData), "EquipLoadout")]
    [HarmonyPrefix]
    public static bool EquipLoadoutPrefix(ref PlayerData.GearData __instance, int index, ref object ___loadouts,
        ref bool __result)
    {
        EnsureLoadoutsCapacity(ref ___loadouts, index);

        var loadoutsArray = ___loadouts as Array;
        if (index < 0 || loadoutsArray == null || index >= loadoutsArray.Length)
        {
            __result = false;
            return false;
        }

        return true;
    }

    internal static void EnsureLoadoutsCapacity(ref object loadouts, int index)
    {
        if (LoadoutType == null)
            return;

        var loadoutsArray = loadouts as Array;
        if (loadoutsArray == null)
        {
            loadoutsArray = Array.CreateInstance(LoadoutType, Mathf.Max(index + 1, MaxLoadoutSlots));
            loadouts = loadoutsArray;
            return;
        }

        if (index >= loadoutsArray.Length)
        {
            var newArray = Array.CreateInstance(LoadoutType, Mathf.Max(index + 1, MaxLoadoutSlots));
            loadoutsArray.CopyTo(newArray, 0);
            loadouts = newArray;
        }
    }
}