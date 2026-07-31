using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[HarmonyPatch]
public static class LoadoutHoverInfoPatches
{
    private static readonly Type LoadoutHoverInfoType = AccessTools.TypeByName("LoadoutHoverInfo");
    private static bool isRenaming = false;
    private static string currentRenameValue = "";
    private static LoadoutHoverInfo currentlyRenamingButton = null;

    private static readonly Dictionary<int, string> loadoutNames = new();

    public static Key RenameKey = Key.R;

    internal static readonly Dictionary<GearDetailsWindow, Dictionary<int, string>> windowLoadoutNames =
        new();

    private static string GetLoadoutName(GearDetailsWindow window, int loadoutIndex)
    {
        try
        {
            if (windowLoadoutNames.TryGetValue(window, out var windowNames))
                if (windowNames.TryGetValue(loadoutIndex, out var name) && !string.IsNullOrEmpty(name))
                    return name;

            var upgradable =
                window.GetType().GetField("upgradable", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.GetValue(window) as IUpgradable;
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
                        var key = $"{gear.Info.ID}_{loadoutIndex}";
                        var savedName = PlayerPrefs.GetString("LoadoutName_" + key, "");
                        SparrohPlugin.Logger.LogInfo(
                            $"Retrieving name for gear {gear.Info.ID} slot {loadoutIndex}: key='{key}' name='{savedName}'");
                        if (!string.IsNullOrEmpty(savedName))
                        {
                            if (!windowLoadoutNames.TryGetValue(window, out var namesDict))
                            {
                                namesDict = new Dictionary<int, string>();
                                windowLoadoutNames[window] = namesDict;
                            }

                            namesDict[loadoutIndex] = savedName;
                            return savedName;
                        }
                    }
                }
            }
        }
        catch (Exception)
        {
        }

        return $"Loadout {loadoutIndex + 1}";
    }

    private static void SetLoadoutName(GearDetailsWindow window, int loadoutIndex, string newName)
    {
        try
        {
            if (!windowLoadoutNames.TryGetValue(window, out var windowNames))
            {
                windowNames = new Dictionary<int, string>();
                windowLoadoutNames[window] = windowNames;
            }

            if (!string.IsNullOrEmpty(newName))
                windowNames[loadoutIndex] = newName;
            else
                windowNames.Remove(loadoutIndex);

            try
            {
                var upgradable = window.UpgradablePrefab;
                if (upgradable != null)
                {
                    var playerDataType = typeof(PlayerData);
                    var instanceProp =
                        playerDataType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    var playerDataInstance = instanceProp?.GetValue(null);

                    var getGearDataMethod = playerDataType.GetMethod("GetGearData", new[] { typeof(IUpgradable) });
                    var gearData = getGearDataMethod?.Invoke(playerDataInstance, new object[] { upgradable });

                    if (gearData != null)
                    {
                        var gear = gearData.GetType().GetField("gear", BindingFlags.NonPublic | BindingFlags.Instance)
                            ?.GetValue(gearData) as IUpgradable;
                        if (gear != null)
                        {
                            var key = $"{gear.Info.ID}_{loadoutIndex}";
                            SparrohPlugin.Logger.LogInfo(
                                $"Saving name for gear {gear.Info.ID} slot {loadoutIndex}: key='{key}' name='{newName}'");
                            if (!string.IsNullOrEmpty(newName))
                            {
                                PlayerPrefs.SetString("LoadoutName_" + key, newName);
                                PlayerPrefs.Save();

                                var altKey = $"ImprovedLoadouts_Name_{gear.Info.ID}_{loadoutIndex}";
                                PlayerPrefs.SetString(altKey, newName);
                                PlayerPrefs.Save();
                            }
                            else
                            {
                                PlayerPrefs.DeleteKey("LoadoutName_" + key);
                                PlayerPrefs.Save();
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            try
            {
                var hoverInfoDisplayType = AccessTools.TypeByName("HoverInfoDisplay");
                var instanceProp =
                    hoverInfoDisplayType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                var hoverDisplay = instanceProp?.GetValue(null);

                if (hoverDisplay != null)
                {
                    var currentInfoField =
                        hoverInfoDisplayType.GetField("currentInfo", BindingFlags.NonPublic | BindingFlags.Instance);
                    var setInfoMethod =
                        hoverInfoDisplayType.GetMethod("SetInfo", BindingFlags.Public | BindingFlags.Instance);

                    if (currentInfoField != null && setInfoMethod != null)
                    {
                        var currentInfo = currentInfoField.GetValue(hoverDisplay);
                        var loadoutButtonsField = window.GetType().GetField("loadoutButtons",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        var loadoutButtons = loadoutButtonsField?.GetValue(window) as Array;
                        if (loadoutButtons != null && loadoutIndex < loadoutButtons.Length)
                        {
                            var button = loadoutButtons.GetValue(loadoutIndex) as LoadoutHoverInfo;
                            if (currentInfo != null && currentInfo == button)
                            {
                                currentInfoField.SetValue(hoverDisplay, null);
                                setInfoMethod.Invoke(hoverDisplay, new object[] { button });
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                try
                {
                    var hoverInfoDisplayType = AccessTools.TypeByName("HoverInfoDisplay");
                    var instanceProp =
                        hoverInfoDisplayType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    var hoverDisplay = instanceProp?.GetValue(null);

                    if (hoverDisplay != null)
                    {
                        var deactivateMethod = hoverInfoDisplayType.GetMethod("Deactivate",
                            BindingFlags.Public | BindingFlags.Instance);
                        deactivateMethod?.Invoke(hoverDisplay, new object[0]);
                    }
                }
                catch (Exception)
                {
                }
            }
        }
        catch (Exception)
        {
        }
    }

    private static int FindHoveredButton(GearDetailsWindow window)
    {
        try
        {
            try
            {
                var hoverInfoDisplayType = AccessTools.TypeByName("HoverInfoDisplay");
                var instanceProp =
                    hoverInfoDisplayType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                var hoverDisplay = instanceProp?.GetValue(null);

                if (hoverDisplay != null)
                {
                    var currentInfoField =
                        hoverInfoDisplayType.GetField("currentInfo", BindingFlags.NonPublic | BindingFlags.Instance);
                    var currentInfo = currentInfoField?.GetValue(hoverDisplay);

                    if (currentInfo != null)
                    {
                        var loadoutButtonsField = window.GetType().GetField("loadoutButtons",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        var loadoutButtons = loadoutButtonsField?.GetValue(window) as Array;

                        if (loadoutButtons != null)
                            for (var i = 0; i < loadoutButtons.Length; i++)
                            {
                                var button = loadoutButtons.GetValue(i) as LoadoutHoverInfo;
                                if (button != null && button == currentInfo) return i;
                            }
                    }
                }
            }
            catch (Exception)
            {
            }

            try
            {
                var eventSystemType = AccessTools.TypeByName("UnityEngine.EventSystems.EventSystem");
                var currentProp = eventSystemType.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
                var eventSystem = currentProp?.GetValue(null);

                if (eventSystem != null)
                {
                    var selectedGOProp = eventSystemType.GetProperty("currentSelectedGameObject",
                        BindingFlags.Public | BindingFlags.Instance);
                    var selectedGO = selectedGOProp?.GetValue(eventSystem) as GameObject;

                    if (selectedGO != null)
                    {
                        var loadoutHoverInfo = selectedGO.GetComponentInParent<LoadoutHoverInfo>();
                        if (loadoutHoverInfo != null)
                        {
                            var loadoutButtonsField = window.GetType().GetField("loadoutButtons",
                                BindingFlags.NonPublic | BindingFlags.Instance);
                            var loadoutButtons = loadoutButtonsField?.GetValue(window) as Array;

                            if (loadoutButtons != null)
                                for (var i = 0; i < loadoutButtons.Length; i++)
                                    if (loadoutButtons.GetValue(i) == loadoutHoverInfo)
                                        return i;
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            try
            {
                var loadoutButtonsField = window.GetType()
                    .GetField("loadoutButtons", BindingFlags.NonPublic | BindingFlags.Instance);
                var loadoutButtons = loadoutButtonsField?.GetValue(window) as Array;

                if (loadoutButtons != null)
                    for (var i = 0; i < loadoutButtons.Length; i++)
                    {
                        var button = loadoutButtons.GetValue(i) as LoadoutHoverInfo;
                        if (button != null)
                        {
                            var gameObject = button.GetType().GetProperty("gameObject")?.GetValue(button) as GameObject;
                            if (gameObject != null)
                            {
                            }
                        }
                    }
            }
            catch (Exception)
            {
            }
        }
        catch (Exception)
        {
        }

        return -1;
    }

    [HarmonyPatch(typeof(LoadoutHoverInfo), "GetTitle")]
    [HarmonyPostfix]
    public static void GetTitlePostfix(ref LoadoutHoverInfo __instance, ref bool __result, out string title,
        out Color color)
    {
        try
        {
            var gearDetailsWindow = __instance.GetComponentInParent<GearDetailsWindow>();
            if (gearDetailsWindow != null)
            {
                var loadoutButtonsField = gearDetailsWindow.GetType()
                    .GetField("loadoutButtons", BindingFlags.NonPublic | BindingFlags.Instance);
                var loadoutButtons = loadoutButtonsField?.GetValue(gearDetailsWindow) as Array;

                if (loadoutButtons != null)
                    for (var i = 0; i < loadoutButtons.Length; i++)
                    {
                        var button = loadoutButtons.GetValue(i) as LoadoutHoverInfo;
                        if (button != null && ReferenceEquals(button, __instance))
                        {
                            var customName = GetLoadoutName(gearDetailsWindow, i);
                            if (!string.IsNullOrEmpty(customName))
                            {
                                title = customName;
                                color = Color.white;
                                __result = true;
                                return;
                            }

                            title = $"Loadout {i + 1}";
                            color = Color.white;
                            __result = true;
                            return;
                        }
                    }
            }

            title = TextBlocks.GetString("loadout");
            color = Color.white;
            __result = true;
        }
        catch (Exception)
        {
            title = TextBlocks.GetString("loadout");
            color = Color.white;
            __result = true;
        }
    }

    [HarmonyPatch(typeof(GearDetailsWindow), "Update")]
    [HarmonyPostfix]
    public static void GearDetailsWindowUpdatePostfix(ref GearDetailsWindow __instance)
    {
        try
        {
            if (LoadoutRenameDialog.IsActive) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[RenameKey].wasPressedThisFrame)
            {
                Vector3 mousePos = Mouse.current.position.ReadValue();

                var loadoutButtonsField = __instance.GetType()
                    .GetField("loadoutButtons", BindingFlags.NonPublic | BindingFlags.Instance);
                var loadoutButtons = loadoutButtonsField?.GetValue(__instance) as Array;

                if (loadoutButtons != null)
                    for (var i = 0; i < loadoutButtons.Length; i++)
                    {
                        var button = loadoutButtons.GetValue(i) as LoadoutHoverInfo;
                        if (button != null && IsHovered(button))
                        {
                            StartRenameProcess(button, i, __instance);
                            return;
                        }
                    }
            }
        }
        catch (Exception)
        {
        }
    }

    private static bool IsHovered(LoadoutHoverInfo button)
    {
        try
        {
            var hoverInfoDisplayType = AccessTools.TypeByName("HoverInfoDisplay");
            var instanceProp = hoverInfoDisplayType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            var hoverDisplay = instanceProp?.GetValue(null);

            if (hoverDisplay != null)
            {
                var currentInfoField =
                    hoverInfoDisplayType.GetField("currentInfo", BindingFlags.NonPublic | BindingFlags.Instance);
                var currentInfo = currentInfoField?.GetValue(hoverDisplay);

                if (currentInfo == button) return true;
            }

            try
            {
                var camera = Camera.main;
                if (camera != null)
                {
                    var mousePos = Mouse.current.position.ReadValue();
                    var ray = camera.ScreenPointToRay(mousePos);

                    if (Physics.Raycast(ray, out var hit, Mathf.Infinity))
                    {
                        var hitObject = hit.collider?.gameObject;
                        if (hitObject != null)
                        {
                            var loadoutHoverInfo = hitObject.GetComponentInParent<LoadoutHoverInfo>();
                            if (loadoutHoverInfo == button) return true;
                        }
                    }

                    var pointerEventDataType = AccessTools.TypeByName("UnityEngine.EventSystems.PointerEventData");
                    if (pointerEventDataType != null)
                    {
                        var pointerEventData = Activator.CreateInstance(pointerEventDataType, EventSystem.current);
                        pointerEventDataType.GetField("position")?.SetValue(pointerEventData, mousePos);

                        var resultList = new List<RaycastResult>();
                        try
                        {
                            var canvasList = Object.FindObjectsOfType<Canvas>();
                            foreach (var canvas in canvasList)
                                if (canvas != null)
                                {
                                    var raycaster = canvas.GetComponent<GraphicRaycaster>();
                                    if (raycaster != null)
                                    {
                                        var raycastMethod = raycaster.GetType().GetMethod("Raycast",
                                            BindingFlags.Public | BindingFlags.Instance);
                                        if (raycastMethod != null)
                                        {
                                            resultList.Clear();
                                            raycastMethod.Invoke(raycaster, new[] { pointerEventData, resultList });

                                            foreach (var result in resultList)
                                                if (result.gameObject != null)
                                                {
                                                    var loadoutHover = result.gameObject
                                                        .GetComponentInParent<LoadoutHoverInfo>();
                                                    if (loadoutHover == button) return true;
                                                }
                                        }
                                    }
                                }
                        }
                        finally
                        {
                            resultList.Clear();
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            try
            {
                var hoverInfo = button.GetType().GetProperty("Active", BindingFlags.Public | BindingFlags.Instance)
                    ?.GetValue(button);
                if (hoverInfo is bool active && active) return true;
            }
            catch (Exception)
            {
            }

            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void StartRenameProcess(LoadoutHoverInfo button, int loadoutIndex, GearDetailsWindow window)
    {
        try
        {
            var currentName = GetLoadoutName(window, loadoutIndex);
            if (string.IsNullOrEmpty(currentName))
                currentName = $"Loadout {loadoutIndex + 1}";

            var mousePos = Mouse.current.position.ReadValue();
            LoadoutRenameDialog.Show(mousePos, currentName,
                newName => { SetLoadoutName(window, loadoutIndex, newName); },
                () => { },
                window, loadoutIndex);
        }
        catch (Exception)
        {
        }
    }

    [HarmonyPatch(typeof(PlayerData.GearData), "SaveLoadout")]
    [HarmonyPostfix]
    public static void SaveLoadoutNamesPostfix(ref PlayerData.GearData __instance, int index)
    {
        try
        {
            var gear = __instance.Gear;
            if (gear == null) return;

            var gearDetailsWindowType = AccessTools.TypeByName("GearDetailsWindow");
            var activeProp = gearDetailsWindowType.GetProperty("Active", BindingFlags.Public | BindingFlags.Static);
            object activeWindow = null;
            if (activeProp != null) activeWindow = activeProp.GetValue(null);

            if (activeWindow == null)
            {
                var allObjects = Object.FindObjectsOfType(gearDetailsWindowType);
                if (allObjects.Length > 0) activeWindow = allObjects[0];
            }

            if (activeWindow != null && windowLoadoutNames.TryGetValue((GearDetailsWindow)activeWindow, out var names))
                if (names.TryGetValue(index, out var name))
                {
                    var key = $"{gear.Info.ID}_{gear.GetHashCode()}_{index}";
                    SparrohPlugin.Logger.LogInfo(
                        $"Persisting name for gear {gear.Info.ID} slot {index}: key='{key}' name='{name}'");
                    PlayerPrefs.SetString("LoadoutName_" + key, name);
                    PlayerPrefs.Save();
                }
        }
        catch (Exception)
        {
        }
    }
}

[HarmonyPatch]
public static class RenameGetPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(LoadoutHoverInfoPatches), "GetLoadoutName");
    }

    private static void Prefix(ref int __1)
    {
        if (__1 < 3) __1 += LoadoutExpanderMod.PageOffset;
    }
}

[HarmonyPatch]
public static class RenameSetPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(LoadoutHoverInfoPatches), "SetLoadoutName");
    }

    private static void Prefix(ref int __1)
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
            var window = __instance.GetComponentInParent<GearDetailsWindow>();
            if (window == null) return;

            var visualIndex = -1;
            var buttons = LoadoutExpanderMod._loadoutButtonsField.GetValue(window) as Array;

            if (buttons != null)
                for (var i = 0; i < buttons.Length; i++)
                    if (buttons.GetValue(i) == __instance)
                    {
                        visualIndex = i;
                        break;
                    }

            if (visualIndex != -1 && visualIndex < 3)
            {
                var realIndex = visualIndex + LoadoutExpanderMod.PageOffset;

                string displayName = null;
                var upgradable =
                    window.GetType().GetField("upgradable", BindingFlags.NonPublic | BindingFlags.Instance)
                        ?.GetValue(window) as IUpgradable;

                if (upgradable != null)
                {
                    var playerDataType = typeof(PlayerData);
                    var instanceProp =
                        playerDataType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    var playerDataInstance = instanceProp?.GetValue(null);

                    var getGearDataMethod = playerDataType.GetMethod("GetGearData", new[] { typeof(IUpgradable) });
                    var gearData = getGearDataMethod?.Invoke(playerDataInstance, new object[] { upgradable });

                    if (gearData != null)
                    {
                        var gear = gearData.GetType().GetField("gear", BindingFlags.NonPublic | BindingFlags.Instance)
                            ?.GetValue(gearData) as IUpgradable;
                        if (gear != null)
                        {
                            var key = $"{gear.Info.ID}_{realIndex}";
                            displayName = PlayerPrefs.GetString("LoadoutName_" + key, "");
                        }
                    }
                }

                if (!string.IsNullOrEmpty(displayName))
                    title = displayName;
                else
                    title = string.Format("Loadout {0}", realIndex + 1);
            }
        }
        catch
        {
        }
    }
}