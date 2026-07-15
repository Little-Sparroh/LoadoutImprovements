using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Highlights loadout slots whose saved contents match the gear's currently equipped upgrades
/// by inverting icon/fill colors: fill uses the player's UI color (Global.UIColor) and the
/// icon turns black. Matching is content-based, so it persists across sessions via the game's
/// own save data.
///
/// Colors are re-applied every frame while the window is open so hover/idle ColorButton-style
/// color rewrites cannot permanently clear the highlight.
/// </summary>
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

    // Image/Graphic instanceID -> original color (captured once before first highlight).
    private static readonly Dictionary<int, Color> OriginalIconColors = new Dictionary<int, Color>();
    private static readonly Dictionary<int, Color> OriginalFillColors = new Dictionary<int, Color>();

    // Cached match state per visual button slot (0..2) for the active window.
    // Recomputed on equip/save/setup/page refresh; applied every Update.
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
            // index already includes PageOffset from LoadoutExpander's prefix
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

    /// <summary>
    /// Re-apply icon/fill colors every frame so hover/idle UI color controllers cannot stick.
    /// Match state is recomputed only when page/equipped/loadout data changes.
    /// </summary>
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
        for (int i = 0; i < CachedMatch.Length; i++)
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
        int page = LoadoutExpanderMod.PageOffset;
        int equippedHash = 0;
        int loadoutHash = 0;

        try
        {
            IUpgradable upgradable = window.UpgradablePrefab;
            if (upgradable != null)
            {
                PlayerData.GearData gearData = PlayerData.GetGearData(upgradable);
                equippedHash = ComputeEquippedHash(gearData);
                loadoutHash = ComputeLoadoutsHash(gearData);
            }
        }
        catch
        {
            // fall through and recompute matches
        }

        bool needsRecompute = CachedWindow != window
                              || CachedPageOffset != page
                              || CachedEquippedHash != equippedHash
                              || CachedLoadoutHash != loadoutHash;

        if (!needsRecompute)
            return;

        CachedWindow = window;
        CachedPageOffset = page;
        CachedEquippedHash = equippedHash;
        CachedLoadoutHash = loadoutHash;

        Array buttons = GetLoadoutButtons(window);
        if (buttons == null)
        {
            for (int i = 0; i < CachedMatch.Length; i++)
                CachedMatch[i] = false;
            return;
        }

        IUpgradable gear = window.UpgradablePrefab;
        PlayerData.GearData data = gear != null ? PlayerData.GetGearData(gear) : null;

        int count = Mathf.Min(buttons.Length, 3);
        for (int i = 0; i < CachedMatch.Length; i++)
        {
            if (i < count && buttons.GetValue(i) is LoadoutHoverInfo)
            {
                int realIndex = i + page;
                CachedMatch[i] = LoadoutMatchesEquipped(data, realIndex);
            }
            else
            {
                CachedMatch[i] = false;
            }
        }
    }

    private static void ApplyCachedColors(GearDetailsWindow window)
    {
        Array buttons = GetLoadoutButtons(window);
        if (buttons == null)
            return;

        Color uiColor = GetPlayerUIColor();
        int count = Mathf.Min(buttons.Length, 3);
        for (int i = 0; i < count; i++)
        {
            if (!(buttons.GetValue(i) is LoadoutHoverInfo button) || button == null)
                continue;

            Image icon = GetIconImage(button);
            Graphic fill = GetFillGraphic(button, icon);

            bool matched = CachedMatch[i];

            if (icon != null && icon.gameObject.activeSelf)
            {
                int iconId = icon.GetInstanceID();
                if (!OriginalIconColors.ContainsKey(iconId))
                {
                    // Capture a non-highlight baseline so we don't lock black as "original".
                    Color current = icon.color;
                    OriginalIconColors[iconId] = IsMatchedIconColor(current)
                        ? DefaultIconColor
                        : (current.a > 0f ? current : DefaultIconColor);
                }

                Color iconTarget = matched ? MatchedIconColor : OriginalIconColors[iconId];
                if (!ColorsApproximatelyEqual(icon.color, iconTarget))
                    icon.color = iconTarget;
            }

            if (fill != null)
            {
                int fillId = fill.GetInstanceID();
                if (!OriginalFillColors.ContainsKey(fillId))
                {
                    Color current = fill.color;
                    // Avoid capturing a previously applied UI-color highlight as the baseline.
                    OriginalFillColors[fillId] = IsLikelyHighlightFill(current, uiColor)
                        ? Color.white
                        : (current.a > 0f ? current : Color.white);
                }

                Color fillTarget = matched ? uiColor : OriginalFillColors[fillId];
                // Preserve alpha from the original fill when applying UI color so transparency stays correct.
                if (matched)
                {
                    Color original = OriginalFillColors[fillId];
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

    /// <summary>
    /// Vanilla UpdateLoadoutIcon uses button.transform.GetChild(1) for the icon Image.
    /// </summary>
    private static Image GetIconImage(LoadoutHoverInfo button)
    {
        Transform t = button.transform;
        if (t.childCount > 1)
        {
            Image icon = t.GetChild(1).GetComponent<Image>();
            if (icon != null)
                return icon;
        }

        Image[] images = button.GetComponentsInChildren<Image>(true);
        if (images != null && images.Length > 1)
            return images[1];
        if (images != null && images.Length == 1)
            return images[0];

        return null;
    }

    /// <summary>
    /// Fill/background is typically the button root Graphic or child 0 Image (not the icon).
    /// </summary>
    private static Graphic GetFillGraphic(LoadoutHoverInfo button, Image icon)
    {
        if (button == null)
            return null;

        Transform t = button.transform;

        // Prefer child 0 when present and distinct from the icon.
        if (t.childCount > 0)
        {
            Transform child0 = t.GetChild(0);
            Graphic g = child0.GetComponent<Graphic>();
            if (g != null && g != icon)
                return g;
        }

        // Root graphic on the button itself (common for ColorButton-style fills).
        Graphic root = button.GetComponent<Graphic>();
        if (root != null && root != icon)
            return root;

        // Fallback: first Image under the button that is not the icon.
        Image[] images = button.GetComponentsInChildren<Image>(true);
        if (images != null)
        {
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i] != icon)
                    return images[i];
            }
        }

        return null;
    }

    private static Color GetPlayerUIColor()
    {
        try
        {
            // Global.UIColor is the player's custom UI color when set,
            // otherwise the selected character's UI color.
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
        // If fill already matches UI color RGB, treat it as a previous highlight pass.
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

        IList equipped = EquippedUpgradesField.GetValue(gearData) as IList;
        if (equipped == null || equipped.Count == 0)
            return 0;

        unchecked
        {
            int hash = 17;
            hash = hash * 31 + equipped.Count;
            List<EquipKey> keys = ToSortedKeys(equipped);
            for (int i = 0; i < keys.Count; i++)
                hash = hash * 31 + keys[i].GetHashCode();
            return hash;
        }
    }

    private static int ComputeLoadoutsHash(PlayerData.GearData gearData)
    {
        if (gearData == null || LoadoutsField == null || LoadoutUpgradesField == null)
            return 0;

        Array loadouts = LoadoutsField.GetValue(gearData) as Array;
        if (loadouts == null)
            return 0;

        unchecked
        {
            int hash = 17;
            hash = hash * 31 + loadouts.Length;
            int max = Mathf.Min(loadouts.Length, 9);
            for (int i = 0; i < max; i++)
            {
                object loadout = loadouts.GetValue(i);
                if (loadout == null)
                {
                    hash = hash * 31;
                    continue;
                }

                IList upgrades = LoadoutUpgradesField.GetValue(loadout) as IList;
                if (upgrades == null)
                {
                    hash = hash * 31;
                    continue;
                }

                hash = hash * 31 + upgrades.Count;
                List<EquipKey> keys = ToSortedKeys(upgrades);
                for (int k = 0; k < keys.Count; k++)
                    hash = hash * 31 + keys[k].GetHashCode();
            }

            return hash;
        }
    }

    private static bool LoadoutMatchesEquipped(PlayerData.GearData gearData, int loadoutIndex)
    {
        if (gearData == null || LoadoutsField == null || EquippedUpgradesField == null || LoadoutUpgradesField == null)
            return false;

        IList equipped = EquippedUpgradesField.GetValue(gearData) as IList;
        if (equipped == null || equipped.Count == 0)
            return false;

        Array loadouts = LoadoutsField.GetValue(gearData) as Array;
        if (loadouts == null || loadoutIndex < 0 || loadoutIndex >= loadouts.Length)
            return false;

        object loadout = loadouts.GetValue(loadoutIndex);
        if (loadout == null)
            return false;

        IList loadoutUpgrades = LoadoutUpgradesField.GetValue(loadout) as IList;
        if (loadoutUpgrades == null || loadoutUpgrades.Count == 0)
            return false;

        if (loadoutUpgrades.Count != equipped.Count)
            return false;

        List<EquipKey> equippedKeys = ToSortedKeys(equipped);
        List<EquipKey> loadoutKeys = ToSortedKeys(loadoutUpgrades);
        if (equippedKeys.Count != loadoutKeys.Count || equippedKeys.Count == 0)
            return false;

        for (int i = 0; i < equippedKeys.Count; i++)
        {
            if (!equippedKeys[i].Equals(loadoutKeys[i]))
                return false;
        }

        return true;
    }

    private static List<EquipKey> ToSortedKeys(IList list)
    {
        var keys = new List<EquipKey>(list.Count);
        for (int i = 0; i < list.Count; i++)
        {
            object item = list[i];
            if (item == null)
                continue;

            if (item is PlayerData.UpgradeEquipData data)
            {
                keys.Add(new EquipKey(data.upgradeID, data.x, data.y, data.rotation));
                continue;
            }

            try
            {
                Type t = item.GetType();
                int id = Convert.ToInt32(AccessTools.Field(t, "upgradeID")?.GetValue(item) ?? 0);
                sbyte x = Convert.ToSByte(AccessTools.Field(t, "x")?.GetValue(item) ?? (sbyte)0);
                sbyte y = Convert.ToSByte(AccessTools.Field(t, "y")?.GetValue(item) ?? (sbyte)0);
                byte rot = Convert.ToByte(AccessTools.Field(t, "rotation")?.GetValue(item) ?? (byte)0);
                keys.Add(new EquipKey(id, x, y, rot));
            }
            catch
            {
                // skip unreadable entry
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
            int c = UpgradeId.CompareTo(other.UpgradeId);
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

        public override bool Equals(object obj) => obj is EquipKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = UpgradeId;
                hash = (hash * 397) ^ X;
                hash = (hash * 397) ^ Y;
                hash = (hash * 397) ^ Rotation;
                return hash;
            }
        }
    }
}
