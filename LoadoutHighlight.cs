using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

[HarmonyPatch]
public static class LoadoutHighlightMod
{
    private static readonly Color MatchedIconColor = Color.black;
    private static readonly Color DefaultIconColor = Color.white;

    private static readonly FieldInfo LoadoutsField =
        AccessTools.Field(typeof(PlayerData.GearData), "loadouts");

    private static readonly FieldInfo EquippedUpgradesField =
        AccessTools.Field(typeof(PlayerData.GearData), "equippedUpgrades");

    private static readonly Type LoadoutType =
        typeof(PlayerData).GetNestedType("Loadout", BindingFlags.NonPublic);

    private static readonly FieldInfo LoadoutUpgradesField =
        LoadoutType?.GetField("upgrades", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);


    private static readonly Dictionary<int, Color> OriginalIconColors = new();
    private static readonly Dictionary<int, Color> OriginalFillColors = new();


    private static readonly bool[] CachedMatch = new bool[3];
    private static GearDetailsWindow CachedWindow;
    private static int CachedPageOffset = int.MinValue;
    private static int CachedEquippedHash;
    private static int CachedLoadoutHash;

    [HarmonyPatch(typeof(GearDetailsWindow), "UpdateLoadoutIcon")]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    public static void UpdateLoadoutIconPostfix(GearDetailsWindow __instance, LoadoutHoverInfo button, int index)
    {
        try
        {
            InvalidateCache();
            ApplyHighlight(__instance, button, index);
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger?.LogError($"LoadoutHighlight UpdateLoadoutIcon failed: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(GearDetailsWindow), "Setup")]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    public static void SetupPostfix(GearDetailsWindow __instance)
    {
        try
        {
            InvalidateCache();
            RefreshAll(__instance);
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger?.LogError($"LoadoutHighlight Setup failed: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(GearDetailsWindow), "SetupUpgrades")]
    [HarmonyPostfix]
    public static void SetupUpgradesPostfix(GearDetailsWindow __instance)
    {
        try
        {
            InvalidateCache();
            RefreshAll(__instance);
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger?.LogError($"LoadoutHighlight SetupUpgrades failed: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(GearDetailsWindow), "EquipLoadout")]
    [HarmonyPostfix]
    public static void EquipLoadoutPostfix(GearDetailsWindow __instance)
    {
        try
        {
            InvalidateCache();
            RefreshAll(__instance);
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger?.LogError($"LoadoutHighlight EquipLoadout failed: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(GearDetailsWindow), "SaveLoadout")]
    [HarmonyPostfix]
    public static void SaveLoadoutPostfix(GearDetailsWindow __instance)
    {
        try
        {
            InvalidateCache();
            RefreshAll(__instance);
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger?.LogError($"LoadoutHighlight SaveLoadout failed: {ex.Message}");
        }
    }


    [HarmonyPatch(typeof(GearDetailsWindow), "Update")]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    public static void UpdatePostfix(GearDetailsWindow __instance)
    {
        try
        {
            if (__instance == null || !__instance.gameObject.activeInHierarchy)
                return;

            EnsureMatchCache(__instance);
            ApplyCachedColors(__instance);
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger?.LogError($"LoadoutHighlight Update failed: {ex.Message}");
        }
    }

    private static void InvalidateCache()
    {
        CachedWindow = null;
        CachedPageOffset = int.MinValue;
        CachedEquippedHash = 0;
        CachedLoadoutHash = 0;
        for (var i = 0; i < CachedMatch.Length; i++)
            CachedMatch[i] = false;
    }

    public static void RefreshAll(GearDetailsWindow window)
    {
        if (window == null)
            return;

        EnsureMatchCache(window);
        ApplyCachedColors(window);
    }

    private static void EnsureMatchCache(GearDetailsWindow window)
    {
        var page = LoadoutExpanderMod.PageOffset;
        var equippedHash = 0;
        var loadoutHash = 0;

        try
        {
            var upgradable = window.UpgradablePrefab;
            if (upgradable != null)
            {
                var gearData = PlayerData.GetGearData(upgradable);
                equippedHash = ComputeEquippedHash(gearData);
                loadoutHash = ComputeLoadoutsHash(gearData);
            }
        }
        catch
        {
        }

        var needsRecompute = CachedWindow != window
                             || CachedPageOffset != page
                             || CachedEquippedHash != equippedHash
                             || CachedLoadoutHash != loadoutHash;

        if (!needsRecompute)
            return;

        CachedWindow = window;
        CachedPageOffset = page;
        CachedEquippedHash = equippedHash;
        CachedLoadoutHash = loadoutHash;

        var buttons = GetLoadoutButtons(window);
        if (buttons == null)
        {
            for (var i = 0; i < CachedMatch.Length; i++)
                CachedMatch[i] = false;
            return;
        }

        var gear = window.UpgradablePrefab;
        var data = gear != null ? PlayerData.GetGearData(gear) : null;

        var count = Mathf.Min(buttons.Length, 3);
        for (var i = 0; i < CachedMatch.Length; i++)
            if (i < count && buttons.GetValue(i) is LoadoutHoverInfo)
            {
                var realIndex = i + page;
                CachedMatch[i] = LoadoutMatchesEquipped(data, realIndex);
            }
            else
            {
                CachedMatch[i] = false;
            }
    }

    private static void ApplyCachedColors(GearDetailsWindow window)
    {
        var buttons = GetLoadoutButtons(window);
        if (buttons == null)
            return;

        var uiColor = GetPlayerUIColor();
        var count = Mathf.Min(buttons.Length, 3);
        for (var i = 0; i < count; i++)
        {
            if (!(buttons.GetValue(i) is LoadoutHoverInfo button) || button == null)
                continue;

            var icon = GetIconImage(button);
            var fill = GetFillGraphic(button, icon);

            var matched = CachedMatch[i];

            if (icon != null && icon.gameObject.activeSelf)
            {
                var iconId = icon.GetInstanceID();
                if (!OriginalIconColors.ContainsKey(iconId))
                {
                    var current = icon.color;
                    OriginalIconColors[iconId] = IsMatchedIconColor(current)
                        ? DefaultIconColor
                        : current.a > 0f
                            ? current
                            : DefaultIconColor;
                }

                var iconTarget = matched ? MatchedIconColor : OriginalIconColors[iconId];
                if (!ColorsApproximatelyEqual(icon.color, iconTarget))
                    icon.color = iconTarget;
            }

            if (fill != null)
            {
                var fillId = fill.GetInstanceID();
                if (!OriginalFillColors.ContainsKey(fillId))
                {
                    var current = fill.color;

                    OriginalFillColors[fillId] = IsLikelyHighlightFill(current, uiColor)
                        ? Color.white
                        : current.a > 0f
                            ? current
                            : Color.white;
                }

                var fillTarget = matched ? uiColor : OriginalFillColors[fillId];

                if (matched)
                {
                    var original = OriginalFillColors[fillId];
                    fillTarget = new Color(uiColor.r, uiColor.g, uiColor.b, original.a > 0f ? original.a : 1f);
                }

                if (!ColorsApproximatelyEqual(fill.color, fillTarget))
                    fill.color = fillTarget;
            }
        }
    }

    private static void ApplyHighlight(GearDetailsWindow window, LoadoutHoverInfo button, int realIndex)
    {
        if (button == null || window == null)
            return;

        EnsureMatchCache(window);
        ApplyCachedColors(window);
    }

    private static Array GetLoadoutButtons(GearDetailsWindow window)
    {
        Array buttons = null;
        if (LoadoutExpanderMod._loadoutButtonsField != null)
            buttons = LoadoutExpanderMod._loadoutButtonsField.GetValue(window) as Array;

        if (buttons == null)
        {
            var field = AccessTools.Field(typeof(GearDetailsWindow), "loadoutButtons");
            buttons = field?.GetValue(window) as Array;
        }

        return buttons;
    }


    private static Image GetIconImage(LoadoutHoverInfo button)
    {
        var t = button.transform;
        if (t.childCount > 1)
        {
            var icon = t.GetChild(1).GetComponent<Image>();
            if (icon != null)
                return icon;
        }

        var images = button.GetComponentsInChildren<Image>(true);
        if (images != null && images.Length > 1)
            return images[1];
        if (images != null && images.Length == 1)
            return images[0];

        return null;
    }


    private static Graphic GetFillGraphic(LoadoutHoverInfo button, Image icon)
    {
        if (button == null)
            return null;

        var t = button.transform;


        if (t.childCount > 0)
        {
            var child0 = t.GetChild(0);
            var g = child0.GetComponent<Graphic>();
            if (g != null && g != icon)
                return g;
        }


        var root = button.GetComponent<Graphic>();
        if (root != null && root != icon)
            return root;


        var images = button.GetComponentsInChildren<Image>(true);
        if (images != null)
            for (var i = 0; i < images.Length; i++)
                if (images[i] != null && images[i] != icon)
                    return images[i];

        return null;
    }

    private static Color GetPlayerUIColor()
    {
        try
        {
            return Global.UIColor;
        }
        catch
        {
            return Color.white;
        }
    }


    private static bool IsMatchedIconColor(Color c)
    {
        return ColorsApproximatelyEqual(c, MatchedIconColor);
    }

    private static bool IsLikelyHighlightFill(Color current, Color uiColor)
    {
        return Mathf.Abs(current.r - uiColor.r) < 0.01f
               && Mathf.Abs(current.g - uiColor.g) < 0.01f
               && Mathf.Abs(current.b - uiColor.b) < 0.01f;
    }

    private static bool ColorsApproximatelyEqual(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.01f
               && Mathf.Abs(a.g - b.g) < 0.01f
               && Mathf.Abs(a.b - b.b) < 0.01f
               && Mathf.Abs(a.a - b.a) < 0.01f;
    }

    private static int ComputeEquippedHash(PlayerData.GearData gearData)
    {
        if (gearData == null || EquippedUpgradesField == null)
            return 0;

        var equipped = EquippedUpgradesField.GetValue(gearData) as IList;
        if (equipped == null || equipped.Count == 0)
            return 0;

        unchecked
        {
            var hash = 17;
            hash = hash * 31 + equipped.Count;
            var keys = ToSortedKeys(equipped);
            for (var i = 0; i < keys.Count; i++)
                hash = hash * 31 + keys[i].GetHashCode();
            return hash;
        }
    }

    private static int ComputeLoadoutsHash(PlayerData.GearData gearData)
    {
        if (gearData == null || LoadoutsField == null || LoadoutUpgradesField == null)
            return 0;

        var loadouts = LoadoutsField.GetValue(gearData) as Array;
        if (loadouts == null)
            return 0;

        unchecked
        {
            var hash = 17;
            hash = hash * 31 + loadouts.Length;
            var max = Mathf.Min(loadouts.Length, 9);
            for (var i = 0; i < max; i++)
            {
                var loadout = loadouts.GetValue(i);
                if (loadout == null)
                {
                    hash = hash * 31;
                    continue;
                }

                var upgrades = LoadoutUpgradesField.GetValue(loadout) as IList;
                if (upgrades == null)
                {
                    hash = hash * 31;
                    continue;
                }

                hash = hash * 31 + upgrades.Count;
                var keys = ToSortedKeys(upgrades);
                for (var k = 0; k < keys.Count; k++)
                    hash = hash * 31 + keys[k].GetHashCode();
            }

            return hash;
        }
    }

    private static bool LoadoutMatchesEquipped(PlayerData.GearData gearData, int loadoutIndex)
    {
        if (gearData == null || LoadoutsField == null || EquippedUpgradesField == null || LoadoutUpgradesField == null)
            return false;

        var equipped = EquippedUpgradesField.GetValue(gearData) as IList;
        if (equipped == null || equipped.Count == 0)
            return false;

        var loadouts = LoadoutsField.GetValue(gearData) as Array;
        if (loadouts == null || loadoutIndex < 0 || loadoutIndex >= loadouts.Length)
            return false;

        var loadout = loadouts.GetValue(loadoutIndex);
        if (loadout == null)
            return false;

        var loadoutUpgrades = LoadoutUpgradesField.GetValue(loadout) as IList;
        if (loadoutUpgrades == null || loadoutUpgrades.Count == 0)
            return false;

        if (loadoutUpgrades.Count != equipped.Count)
            return false;

        var equippedKeys = ToSortedKeys(equipped);
        var loadoutKeys = ToSortedKeys(loadoutUpgrades);
        if (equippedKeys.Count != loadoutKeys.Count || equippedKeys.Count == 0)
            return false;

        for (var i = 0; i < equippedKeys.Count; i++)
            if (!equippedKeys[i].Equals(loadoutKeys[i]))
                return false;

        return true;
    }

    private static List<EquipKey> ToSortedKeys(IList list)
    {
        var keys = new List<EquipKey>(list.Count);
        for (var i = 0; i < list.Count; i++)
        {
            var item = list[i];
            if (item == null)
                continue;

            if (item is PlayerData.UpgradeEquipData data)
            {
                keys.Add(new EquipKey(data.upgradeID, data.x, data.y, data.rotation));
                continue;
            }

            try
            {
                var t = item.GetType();
                var id = Convert.ToInt32(AccessTools.Field(t, "upgradeID")?.GetValue(item) ?? 0);
                var x = Convert.ToSByte(AccessTools.Field(t, "x")?.GetValue(item) ?? (sbyte)0);
                var y = Convert.ToSByte(AccessTools.Field(t, "y")?.GetValue(item) ?? (sbyte)0);
                var rot = Convert.ToByte(AccessTools.Field(t, "rotation")?.GetValue(item) ?? (byte)0);
                keys.Add(new EquipKey(id, x, y, rot));
            }
            catch
            {
            }
        }

        keys.Sort();
        return keys;
    }

    private readonly struct EquipKey : IComparable<EquipKey>, IEquatable<EquipKey>
    {
        public readonly int UpgradeId;
        public readonly sbyte X;
        public readonly sbyte Y;
        public readonly byte Rotation;

        public EquipKey(int upgradeId, sbyte x, sbyte y, byte rotation)
        {
            UpgradeId = upgradeId;
            X = x;
            Y = y;
            Rotation = rotation;
        }

        public int CompareTo(EquipKey other)
        {
            var c = UpgradeId.CompareTo(other.UpgradeId);
            if (c != 0) return c;
            c = X.CompareTo(other.X);
            if (c != 0) return c;
            c = Y.CompareTo(other.Y);
            if (c != 0) return c;
            return Rotation.CompareTo(other.Rotation);
        }

        public bool Equals(EquipKey other)
        {
            return UpgradeId == other.UpgradeId
                   && X == other.X
                   && Y == other.Y
                   && Rotation == other.Rotation;
        }

        public override bool Equals(object obj)
        {
            return obj is EquipKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = UpgradeId;
                hash = (hash * 397) ^ X;
                hash = (hash * 397) ^ Y;
                hash = (hash * 397) ^ Rotation;
                return hash;
            }
        }
    }
}