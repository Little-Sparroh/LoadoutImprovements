using HarmonyLib;
using System;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.InputSystem;

public static class LoadoutExpanderMod
{
    public static int PageOffset = 0;

    internal static FieldInfo _loadoutButtonsField;
    internal static FieldInfo _upgradableField;
    internal static MethodInfo _updateIconMethod;

    public static Key ScrollLeftKey;
    public static Key ScrollRightKey;

    public static void TogglePage()
    {
        PageOffset += 3;
        if (PageOffset > 6) PageOffset = 0;

        int pageNum = (PageOffset / 3) + 1;

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
            {
                if (window.gameObject.activeInHierarchy && _loadoutButtonsField != null && _updateIconMethod != null)
                {
                    Array buttons = _loadoutButtonsField.GetValue(window) as Array;
                    if (buttons != null)
                    {
                        int count = Mathf.Min(buttons.Length, 3);
                        for (int i = 0; i < count; i++)
                        {
                            object btn = buttons.GetValue(i);
                            _updateIconMethod.Invoke(window, new object[] { btn, i });
                        }
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

    [HarmonyPatch]
    public static class RenameGetPatch
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("LoadoutHoverInfoPatches"), "GetLoadoutName");
        }

        static void Prefix(ref int __1)
        {
             if (__1 < 3) __1 += LoadoutExpanderMod.PageOffset;
        }
    }

    [HarmonyPatch]
    public static class RenameSetPatch
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("LoadoutHoverInfoPatches"), "SetLoadoutName");
        }

        static void Prefix(ref int __1)
        {
             if (__1 < 3) __1 += LoadoutExpanderMod.PageOffset;
        }
    }

    [HarmonyPatch(typeof(LoadoutHoverInfo), "GetTitle")]
    public static class TooltipPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(LoadoutHoverInfo __instance, ref string title)
        {
            if (LoadoutExpanderMod.PageOffset == 0) return;

            try
            {
                GearDetailsWindow window = __instance.GetComponentInParent<GearDetailsWindow>();
                if (window == null) return;

                int visualIndex = -1;
                Array buttons = LoadoutExpanderMod._loadoutButtonsField.GetValue(window) as Array;

                if (buttons != null)
                {
                    for(int i=0; i < buttons.Length; i++)
                    {
                        if ((object)buttons.GetValue(i) == (object)__instance)
                        {
                            visualIndex = i;
                            break;
                        }
                    }
                }

                if (visualIndex != -1 && visualIndex < 3)
                {
                    int realIndex = visualIndex + LoadoutExpanderMod.PageOffset;

                    string displayName = null;
                    IUpgradable upgradable = window.GetType().GetField("upgradable", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(window) as IUpgradable;

                    if (upgradable != null)
                    {
                        var playerDataType = typeof(PlayerData);
                        var instanceProp = playerDataType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                        var playerDataInstance = instanceProp?.GetValue(null);

                        var getGearDataMethod = playerDataType.GetMethod("GetGearData", new Type[] { typeof(IUpgradable) });
                        var gearData = getGearDataMethod?.Invoke(playerDataInstance, new object[] { upgradable });

                        if (gearData != null)
                        {
                            var gear = gearData.GetType().GetField("gear", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(gearData) as IUpgradable;
                            if (gear != null)
                            {
                                string key = $"{gear.Info.ID}_{realIndex}";
                                displayName = PlayerPrefs.GetString("LoadoutName_" + key, "");
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(displayName))
                    {
                        title = displayName;
                    }
                    else
                    {
                        title = string.Format("Loadout {0}", realIndex + 1);
                    }
                }
            }
            catch {}
        }
    }
