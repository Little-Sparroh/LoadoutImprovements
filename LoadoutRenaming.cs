using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

[HarmonyPatch]
public static class PlayerDataPatches
{
    private const int MAX_LOADOUT_SLOTS = 9;

    private static readonly Type LoadoutType = typeof(PlayerData).GetNestedType("Loadout", BindingFlags.NonPublic);
    // UpgradeEquipData is a public nested struct — NonPublic alone returns null.
    private static readonly Type UpgradeEquipDataType = typeof(PlayerData.UpgradeEquipData);

    private static readonly FieldInfo LoadoutUpgradesField =
        LoadoutType?.GetField("upgrades", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo LoadoutIconIndexField =
        LoadoutType?.GetField("iconIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

    [HarmonyPatch(typeof(PlayerData.GearData), "SaveLoadout")]
    [HarmonyPrefix]
    public static bool SaveLoadoutPrefix(ref PlayerData.GearData __instance, int index, ref object ___loadouts)
    {

        Array loadoutsArray = ___loadouts as Array;
        if (loadoutsArray == null)
        {
            loadoutsArray = Array.CreateInstance(LoadoutType, Mathf.Max(index + 1, MAX_LOADOUT_SLOTS));
            ___loadouts = loadoutsArray;
        }
        if (index >= loadoutsArray.Length)
        {
            Array newArray = Array.CreateInstance(LoadoutType, Mathf.Max(index + 1, MAX_LOADOUT_SLOTS));
            loadoutsArray.CopyTo(newArray, 0);
            ___loadouts = newArray;
        }
        return true;
    }

    [HarmonyPatch(typeof(PlayerData.GearData), "EquipLoadout")]
    [HarmonyPrefix]
    public static bool EquipLoadoutPrefix(ref PlayerData.GearData __instance, int index, ref object ___loadouts, ref bool __result)
    {
        Array loadoutsArray = ___loadouts as Array;
        if (loadoutsArray == null)
        {
            loadoutsArray = Array.CreateInstance(LoadoutType, Mathf.Max(index + 1, MAX_LOADOUT_SLOTS));
            ___loadouts = loadoutsArray;
        }
        if (index >= loadoutsArray.Length)
        {
            Array newArray = Array.CreateInstance(LoadoutType, Mathf.Max(index + 1, MAX_LOADOUT_SLOTS));
            loadoutsArray.CopyTo(newArray, 0);
            ___loadouts = newArray;
        }
        if (index < 0 || index >= loadoutsArray.Length)
        {
            __result = false;
            return false;
        }
        return true;
    }

    /// <summary>
    /// Replaces vanilla icon cycling so indices cover equipped upgrade icons first,
    /// then the default Global.LoadoutIcons. Priority.Last so LoadoutExpander's
    /// page-offset prefix adjusts <paramref name="index"/> first.
    /// </summary>
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

            Array loadoutsArray = ___loadouts as Array;
            if (loadoutsArray == null)
            {
                loadoutsArray = Array.CreateInstance(LoadoutType, Mathf.Max(index + 1, MAX_LOADOUT_SLOTS));
                ___loadouts = loadoutsArray;
            }

            if (index < 0)
            {
                __result = false;
                return false;
            }

            if (index >= loadoutsArray.Length)
            {
                Array newArray = Array.CreateInstance(LoadoutType, Mathf.Max(index + 1, MAX_LOADOUT_SLOTS));
                loadoutsArray.CopyTo(newArray, 0);
                ___loadouts = newArray;
                loadoutsArray = newArray;
            }

            // Loadout is a struct — GetValue boxes a copy; must SetValue after mutation.
            object loadout = loadoutsArray.GetValue(index);

            var upgrades = LoadoutUpgradesField?.GetValue(loadout) as System.Collections.IList;
            if (upgrades == null)
            {
                upgrades = Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(UpgradeEquipDataType), 8) as System.Collections.IList;
                LoadoutUpgradesField?.SetValue(loadout, upgrades);
            }

            int upgradeCount = upgrades.Count;
            int defaultIconCount = Global.Instance != null && Global.Instance.LoadoutIcons != null
                ? Global.Instance.LoadoutIcons.Length
                : 0;
            int totalIcons = Mathf.Max(1, upgradeCount + defaultIconCount);

            int currentIconIndex = LoadoutIconIndexField.GetValue(loadout) is int value ? value : 0;
            int newIconIndex = (currentIconIndex + 1) % totalIcons;
            LoadoutIconIndexField.SetValue(loadout, newIconIndex);

            // Critical: write the boxed struct back into the array.
            loadoutsArray.SetValue(loadout, index);

            __result = true;
            return false; // skip vanilla (which only cycles default icons)
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"IncrementLoadoutIcon failed: {ex}");
            __result = false;
            return false;
        }
    }

    /// <summary>
    /// When iconIndex is within the loadout's upgrade list, return that upgrade's icon.
    /// Otherwise map into the default Global.LoadoutIcons range.
    /// </summary>
    [HarmonyPatch(typeof(PlayerData.GearData), "GetLoadoutIcon")]
    [HarmonyPostfix]
    public static void GetLoadoutIconPostfix(int index, ref object ___loadouts, ref Sprite __result)
    {
        try
        {
            if (LoadoutType == null || LoadoutUpgradesField == null || LoadoutIconIndexField == null)
                return;

            Array loadoutsArray = ___loadouts as Array;
            if (loadoutsArray == null || index < 0 || index >= loadoutsArray.Length)
                return;

            object loadout = loadoutsArray.GetValue(index);
            var upgrades = LoadoutUpgradesField.GetValue(loadout) as System.Collections.IList;
            if (upgrades == null || upgrades.Count == 0)
                return;

            int upgradeCount = upgrades.Count;
            int iconIndex = LoadoutIconIndexField.GetValue(loadout) is int value ? value : 0;

            if (iconIndex < upgradeCount)
            {
                Sprite upgradeIcon = GetUpgradeIconFromEquipData(upgrades[iconIndex]);
                if (upgradeIcon != null)
                {
                    __result = upgradeIcon;
                    return;
                }
            }

            // Default icons occupy indices [upgradeCount, upgradeCount + defaultCount).
            if (Global.Instance?.LoadoutIcons != null)
            {
                int defaultIconIndex = iconIndex - upgradeCount;
                if (defaultIconIndex < 0)
                    defaultIconIndex = 0;

                var loadoutIcons = Global.Instance.LoadoutIcons;
                if (defaultIconIndex >= 0 && defaultIconIndex < loadoutIcons.Length)
                {
                    __result = loadoutIcons[defaultIconIndex];
                }
                else if (loadoutIcons.Length > 0)
                {
                    __result = loadoutIcons[Mathf.Min(iconIndex, loadoutIcons.Length - 1)];
                }
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
            // Prefer the public typed path when the list boxes UpgradeEquipData.
            if (equipData is PlayerData.UpgradeEquipData typed)
            {
                UpgradeInstance instance = typed.GetUpgrade();
                return instance?.Upgrade?.Icon;
            }

            // Fallback via reflection if the runtime type differs.
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


[HarmonyPatch]
public static class GearDetailsWindowPatches
{
    private const int NEW_LOADOUT_COUNT = 9;

    private static FieldInfo loadoutButtonsField;

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

            var getGearDataMethod = playerDataType.GetMethod("GetGearData", new Type[] { typeof(IUpgradable) });
            var gearData = getGearDataMethod?.Invoke(playerDataInstance, new object[] { upgradable });

            if (gearData != null)
            {
                var gearDataType = gearData.GetType();
                var loadoutsField = gearDataType.GetField("loadouts", BindingFlags.NonPublic | BindingFlags.Instance);
                if (loadoutsField != null)
                {
                    Array currentLoadouts = loadoutsField.GetValue(gearData) as Array;
                    var loadoutType = playerDataType.GetNestedType("Loadout", BindingFlags.NonPublic);

                    if (currentLoadouts == null || currentLoadouts.Length < NEW_LOADOUT_COUNT)
                    {
                        Array newLoadouts = Array.CreateInstance(loadoutType, NEW_LOADOUT_COUNT);
                        if (currentLoadouts != null)
                            currentLoadouts.CopyTo(newLoadouts, 0);
                        loadoutsField.SetValue(gearData, newLoadouts);
                    }
                }
                else
                {
                }
            }
            else
            {
            }
        }
        catch (Exception ex)
        {
        }

        var loadoutButtonsField = __instance.GetType().GetField("loadoutButtons", BindingFlags.NonPublic | BindingFlags.Instance);
        if (loadoutButtonsField != null)
        {
            Array loadoutButtons = loadoutButtonsField.GetValue(__instance) as Array;

            if (loadoutButtons != null)
            {

                int existingButtons = loadoutButtons.Length;
                for (int l = 0; l < Mathf.Min(NEW_LOADOUT_COUNT, existingButtons); l++)
                {
                    var button = loadoutButtons.GetValue(l);
                    if (button != null)
                    {
                        var gameObject = button.GetType().GetProperty("gameObject")?.GetValue(button);
                        if (gameObject != null)
                        {
                            gameObject.GetType().GetMethod("SetActive")?.Invoke(gameObject, new object[] { true });
                        }
                        else
                        {
                        }
                    }
                    else
                    {
                    }
                }

                if (NEW_LOADOUT_COUNT > existingButtons)
                {
                }
            }
            else
            {
            }
        }
        else
        {
        }

    try
    {
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
                    if (!LoadoutHoverInfoPatches.windowLoadoutNames.TryGetValue(__instance, out var windowNamesDict))
                    {
                        windowNamesDict = new System.Collections.Generic.Dictionary<int, string>();
                        LoadoutHoverInfoPatches.windowLoadoutNames[__instance] = windowNamesDict;
                    }

                    for (int i = 0; i < NEW_LOADOUT_COUNT; i++)
                    {
                        string key = $"{gear.Info.ID}_{i}";
                        string savedName = PlayerPrefs.GetString("LoadoutName_" + key, "");
                        if (!string.IsNullOrEmpty(savedName))
                        {
                            windowNamesDict[i] = savedName;
                        }
                    }
                }
            }
        }
    }
    catch (Exception ex)
    {
    }
    }

    private static void UpdateLoadoutIcon(GearDetailsWindow instance, LoadoutHoverInfo button, int index)
    {
        var method = typeof(GearDetailsWindow).GetMethod("UpdateLoadoutIcon", BindingFlags.NonPublic | BindingFlags.Instance);
        method?.Invoke(instance, new object[] { button, index });
    }
}

[HarmonyPatch]
public static class LoadoutHoverInfoPatches
{
    private static readonly Type LoadoutHoverInfoType = AccessTools.TypeByName("LoadoutHoverInfo");
    private static bool isRenaming = false;
    private static string currentRenameValue = "";
    private static LoadoutHoverInfo currentlyRenamingButton = null;

    private static readonly System.Collections.Generic.Dictionary<int, string> loadoutNames =
        new System.Collections.Generic.Dictionary<int, string>();

    public static Key RenameKey = Key.L;

    internal static readonly System.Collections.Generic.Dictionary<GearDetailsWindow, System.Collections.Generic.Dictionary<int, string>> windowLoadoutNames =
        new System.Collections.Generic.Dictionary<GearDetailsWindow, System.Collections.Generic.Dictionary<int, string>>();

    private static string GetLoadoutName(GearDetailsWindow window, int loadoutIndex)
    {
        try
        {
            if (windowLoadoutNames.TryGetValue(window, out var windowNames))
            {
                if (windowNames.TryGetValue(loadoutIndex, out string name) && !string.IsNullOrEmpty(name))
                {
                    return name;
                }
            }

            var upgradable = window.GetType().GetField("upgradable", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(window) as IUpgradable;
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
                        string key = $"{gear.Info.ID}_{loadoutIndex}";
                        string savedName = PlayerPrefs.GetString("LoadoutName_" + key, "");
                        SparrohPlugin.Logger.LogInfo($"Retrieving name for gear {gear.Info.ID} slot {loadoutIndex}: key='{key}' name='{savedName}'");
                        if (!string.IsNullOrEmpty(savedName))
                        {
                            if (!windowLoadoutNames.TryGetValue(window, out var namesDict))
                            {
                                namesDict = new System.Collections.Generic.Dictionary<int, string>();
                                windowLoadoutNames[window] = namesDict;
                            }
                            namesDict[loadoutIndex] = savedName;
                            return savedName;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
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
                windowNames = new System.Collections.Generic.Dictionary<int, string>();
                windowLoadoutNames[window] = windowNames;
            }

            if (!string.IsNullOrEmpty(newName))
            {
                windowNames[loadoutIndex] = newName;
            }
            else
            {
                windowNames.Remove(loadoutIndex);
            }

        try
        {
            var upgradable = window.UpgradablePrefab;
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
                        string key = $"{gear.Info.ID}_{loadoutIndex}";
                        SparrohPlugin.Logger.LogInfo($"Saving name for gear {gear.Info.ID} slot {loadoutIndex}: key='{key}' name='{newName}'");
                        if (!string.IsNullOrEmpty(newName))
                        {
                            PlayerPrefs.SetString("LoadoutName_" + key, newName);
                            PlayerPrefs.Save();

                            string altKey = $"ImprovedLoadouts_Name_{gear.Info.ID}_{loadoutIndex}";
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
            catch (Exception persistenceEx)
            {
            }

            try
            {
                var hoverInfoDisplayType = AccessTools.TypeByName("HoverInfoDisplay");
                var instanceProp = hoverInfoDisplayType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                var hoverDisplay = instanceProp?.GetValue(null);

                if (hoverDisplay != null)
                {
                    var currentInfoField = hoverInfoDisplayType.GetField("currentInfo", BindingFlags.NonPublic | BindingFlags.Instance);
                    var setInfoMethod = hoverInfoDisplayType.GetMethod("SetInfo", BindingFlags.Public | BindingFlags.Instance);

                    if (currentInfoField != null && setInfoMethod != null)
                    {
                        var currentInfo = currentInfoField.GetValue(hoverDisplay);
                        var loadoutButtonsField = window.GetType().GetField("loadoutButtons", BindingFlags.NonPublic | BindingFlags.Instance);
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
            catch (Exception refreshEx)
            {
                try
                {
                    var hoverInfoDisplayType = AccessTools.TypeByName("HoverInfoDisplay");
                    var instanceProp = hoverInfoDisplayType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    var hoverDisplay = instanceProp?.GetValue(null);

                    if (hoverDisplay != null)
                    {
                        var deactivateMethod = hoverInfoDisplayType.GetMethod("Deactivate", BindingFlags.Public | BindingFlags.Instance);
                        deactivateMethod?.Invoke(hoverDisplay, new object[0]);
                    }
                }
                catch (Exception ex2)
                {
                }
            }
        }
        catch (Exception ex)
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
                var instanceProp = hoverInfoDisplayType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                var hoverDisplay = instanceProp?.GetValue(null);

                if (hoverDisplay != null)
                {
                    var currentInfoField = hoverInfoDisplayType.GetField("currentInfo", BindingFlags.NonPublic | BindingFlags.Instance);
                    var currentInfo = currentInfoField?.GetValue(hoverDisplay);

                    if (currentInfo != null)
                    {
                        var loadoutButtonsField = window.GetType().GetField("loadoutButtons", BindingFlags.NonPublic | BindingFlags.Instance);
                        var loadoutButtons = loadoutButtonsField?.GetValue(window) as Array;

                        if (loadoutButtons != null)
                        {
                            for (int i = 0; i < loadoutButtons.Length; i++)
                            {
                                var button = loadoutButtons.GetValue(i) as LoadoutHoverInfo;
                                if (button != null && button == currentInfo)
                                {
                                    return i;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception hoverEx)
            {
            }

            try
            {
                var eventSystemType = AccessTools.TypeByName("UnityEngine.EventSystems.EventSystem");
                var currentProp = eventSystemType.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
                var eventSystem = currentProp?.GetValue(null);

                if (eventSystem != null)
                {
                    var selectedGOProp = eventSystemType.GetProperty("currentSelectedGameObject", BindingFlags.Public | BindingFlags.Instance);
                    var selectedGO = selectedGOProp?.GetValue(eventSystem) as GameObject;

                    if (selectedGO != null)
                    {

                        var loadoutHoverInfo = selectedGO.GetComponentInParent<LoadoutHoverInfo>();
                        if (loadoutHoverInfo != null)
                        {
                            var loadoutButtonsField = window.GetType().GetField("loadoutButtons", BindingFlags.NonPublic | BindingFlags.Instance);
                            var loadoutButtons = loadoutButtonsField?.GetValue(window) as Array;

                            if (loadoutButtons != null)
                            {
                                for (int i = 0; i < loadoutButtons.Length; i++)
                                {
                                    if (loadoutButtons.GetValue(i) == loadoutHoverInfo)
                                    {
                                        return i;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception eventEx)
            {
            }

            try
            {
                var loadoutButtonsField = window.GetType().GetField("loadoutButtons", BindingFlags.NonPublic | BindingFlags.Instance);
                var loadoutButtons = loadoutButtonsField?.GetValue(window) as Array;

                if (loadoutButtons != null)
                {
                    for (int i = 0; i < loadoutButtons.Length; i++)
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
            }
            catch (Exception buttonEx)
            {
            }

        }
        catch (Exception ex)
        {
        }

        return -1;
    }

    [HarmonyPatch(typeof(LoadoutHoverInfo), "GetTitle")]
    [HarmonyPostfix]
    public static void GetTitlePostfix(ref LoadoutHoverInfo __instance, ref bool __result, out string title, out Color color)
    {
        try
        {
            var gearDetailsWindow = __instance.GetComponentInParent<GearDetailsWindow>();
            if (gearDetailsWindow != null)
            {
                var loadoutButtonsField = gearDetailsWindow.GetType().GetField("loadoutButtons", BindingFlags.NonPublic | BindingFlags.Instance);
                var loadoutButtons = loadoutButtonsField?.GetValue(gearDetailsWindow) as Array;


                if (loadoutButtons != null)
                {
                    for (int i = 0; i < loadoutButtons.Length; i++)
                    {
                        var button = loadoutButtons.GetValue(i) as LoadoutHoverInfo;
                        if (button != null && ReferenceEquals(button, __instance))
                        {
                            string customName = GetLoadoutName(gearDetailsWindow, i);
                            if (!string.IsNullOrEmpty(customName))
                            {
                                title = customName;
                                color = Color.white;
                                __result = true;
                                return;
                            }
                            else
                            {
                                title = $"Loadout {i + 1}";
                                color = Color.white;
                                __result = true;
                                return;
                            }
                        }
                    }
                }
                else
                {
                }
            }
            else
            {
            }

            title = TextBlocks.GetString("loadout");
            color = Color.white;
            __result = true;
        }
        catch (Exception ex)
        {
            title = TextBlocks.GetString("loadout");
            color = Color.white;
            __result = true;
        }
    }

[HarmonyPatch]
public class LoadoutRenameDialog : MonoBehaviour
{
    private static LoadoutRenameDialog Instance;

    private UnityEngine.Canvas canvas;
    private UnityEngine.UI.Image background;
    private TMPro.TMP_InputField inputField;
    private UnityEngine.UI.Button applyButton;
    private UnityEngine.UI.Button cancelButton;
    private TMPro.TextMeshProUGUI applyText;
    private TMPro.TextMeshProUGUI cancelText;

    private System.Action<string> onApplyCallback;
    private System.Action onCancelCallback;

    private GearDetailsWindow window;
    private int loadoutIndex;

    public static bool IsActive => Instance != null;

    public static void Show(Vector2 screenPosition, string currentName, System.Action<string> onApply, System.Action onCancel, GearDetailsWindow targetWindow, int targetLoadoutIndex)
    {
        if (IsActive)
        {
            Close();
        }

        var windowSystem = GameManager.Instance.WindowSystem;
        var dialogGO = new UnityEngine.GameObject("LoadoutRenameDialog");
        dialogGO.transform.SetParent(windowSystem.transform, worldPositionStays: false);

        var dialog = dialogGO.AddComponent<LoadoutRenameDialog>();
        dialog.Initialize(screenPosition, currentName, onApply, onCancel, targetWindow, targetLoadoutIndex);

        Instance = dialog;
    }

    public static void Close()
    {
        if (Instance != null)
        {
            UnityEngine.Object.Destroy(Instance.gameObject);
            Instance = null;
        }
    }

    private void Initialize(Vector2 screenPosition, string currentName, System.Action<string> onApply, System.Action onCancel, GearDetailsWindow targetWindow, int targetLoadoutIndex)
    {
        window = targetWindow;
        loadoutIndex = targetLoadoutIndex;
        onApplyCallback = onApply;
        onCancelCallback = onCancel;

        var gearDetailsWindowGO = window.GetType().GetProperty("gameObject")?.GetValue(window) as UnityEngine.GameObject;
        if (gearDetailsWindowGO != null)
        {
            canvas = gearDetailsWindowGO.GetComponentInParent<UnityEngine.Canvas>();
        }

        if (canvas == null)
        {
            canvas = UnityEngine.Object.FindObjectOfType<UnityEngine.Canvas>();
        }

        if (canvas == null)
        {
            return;
        }
        else
        {
            transform.SetParent(canvas.transform, worldPositionStays: false);
        }

        var canvasGroup = gameObject.AddComponent<UnityEngine.CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        var bgGO = new UnityEngine.GameObject("Background");
        bgGO.transform.SetParent(transform, worldPositionStays: false);
        background = bgGO.AddComponent<UnityEngine.UI.Image>();
        background.color = new UnityEngine.Color(0.2f, 0.2f, 0.2f, 0.95f);
        (background.transform as RectTransform).anchorMin = new Vector2(0.5f, 0.5f);
        (background.transform as RectTransform).anchorMax = new Vector2(0.5f, 0.5f);
        (background.transform as RectTransform).pivot = new Vector2(0.5f, 0.5f);
        (background.transform as RectTransform).sizeDelta = new Vector2(300, 120);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas.transform as RectTransform, screenPosition, canvas.worldCamera, out Vector2 localPos);
        localPos.x += 300f;
        localPos.y += 200f;
        transform.localPosition = localPos;

        var inputGO = new UnityEngine.GameObject("InputField");
        inputGO.transform.SetParent(transform, worldPositionStays: false);
        inputField = inputGO.AddComponent<TMPro.TMP_InputField>();

        var textGO = new UnityEngine.GameObject("Text");
        textGO.transform.SetParent(inputGO.transform, worldPositionStays: false);
        var tmpText = textGO.AddComponent<TMPro.TextMeshProUGUI>();
        inputField.textComponent = tmpText;
        tmpText.fontSize = 24;
        tmpText.color = Color.white;
        tmpText.rectTransform.anchorMin = Vector2.zero;
        tmpText.rectTransform.anchorMax = Vector2.one;
        tmpText.rectTransform.sizeDelta = Vector2.zero;

        inputField.text = currentName;
        inputField.caretColor = Color.white;

        var inputImage = inputGO.AddComponent<UnityEngine.UI.Image>();
        inputImage.color = new UnityEngine.Color(0.1f, 0.1f, 0.1f, 1f);

        (inputField.transform as RectTransform).anchorMin = new Vector2(0.5f, 0.5f);
        (inputField.transform as RectTransform).anchorMax = new Vector2(0.5f, 0.5f);
        (inputField.transform as RectTransform).pivot = new Vector2(0.5f, 0.5f);
        (inputField.transform as RectTransform).sizeDelta = new Vector2(250, 40);
        (inputField.transform as RectTransform).anchoredPosition = new Vector2(0, 20);

        var applyGO = new UnityEngine.GameObject("ApplyButton");
        applyGO.transform.SetParent(transform, worldPositionStays: false);
        applyButton = applyGO.AddComponent<UnityEngine.UI.Button>();
        var applyImage = applyGO.AddComponent<UnityEngine.UI.Image>();
        applyImage.color = new UnityEngine.Color(0.3f, 0.6f, 0.3f, 1f);

        (applyButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 0.5f);
        (applyButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 0.5f);
        (applyButton.transform as RectTransform).pivot = new Vector2(0.5f, 0.5f);
        (applyButton.transform as RectTransform).sizeDelta = new Vector2(80, 35);
        (applyButton.transform as RectTransform).anchoredPosition = new Vector2(-50, -35);

        var applyTextGO = new UnityEngine.GameObject("ApplyText");
        applyTextGO.transform.SetParent(applyGO.transform, worldPositionStays: false);
        applyText = applyTextGO.AddComponent<TMPro.TextMeshProUGUI>();
        applyText.text = "Apply";
        applyText.fontSize = 20;
        applyText.color = Color.white;
        applyText.alignment = TMPro.TextAlignmentOptions.Center;
        (applyText.transform as RectTransform).anchorMin = Vector2.zero;
        (applyText.transform as RectTransform).anchorMax = Vector2.one;
        (applyText.transform as RectTransform).sizeDelta = Vector2.zero;

        var cancelGO = new UnityEngine.GameObject("CancelButton");
        cancelGO.transform.SetParent(transform, worldPositionStays: false);
        cancelButton = cancelGO.AddComponent<UnityEngine.UI.Button>();
        var cancelImage = cancelGO.AddComponent<UnityEngine.UI.Image>();
        cancelImage.color = new UnityEngine.Color(0.6f, 0.3f, 0.3f, 1f);

        (cancelButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 0.5f);
        (cancelButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 0.5f);
        (cancelButton.transform as RectTransform).pivot = new Vector2(0.5f, 0.5f);
        (cancelButton.transform as RectTransform).sizeDelta = new Vector2(80, 35);
        (cancelButton.transform as RectTransform).anchoredPosition = new Vector2(50, -35);

        var cancelTextGO = new UnityEngine.GameObject("CancelText");
        cancelTextGO.transform.SetParent(cancelGO.transform, worldPositionStays: false);
        cancelText = cancelTextGO.AddComponent<TMPro.TextMeshProUGUI>();
        cancelText.text = "Cancel";
        cancelText.fontSize = 20;
        cancelText.color = Color.white;
        cancelText.alignment = TMPro.TextAlignmentOptions.Center;
        (cancelText.transform as RectTransform).anchorMin = Vector2.zero;
        (cancelText.transform as RectTransform).anchorMax = Vector2.one;
        (cancelText.transform as RectTransform).sizeDelta = Vector2.zero;

        applyButton.onClick.AddListener(OnApplyClicked);
        cancelButton.onClick.AddListener(OnCancelClicked);

        inputField.ActivateInputField();
        inputField.Select();

    }

    private void OnApplyClicked()
    {
        if (!string.IsNullOrWhiteSpace(inputField.text))
        {
            onApplyCallback?.Invoke(inputField.text);
        }
        Close();
    }

    private void OnCancelClicked()
    {
        onCancelCallback?.Invoke();
        Close();
    }

    private void Update()
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                OnApplyClicked();
            }
            else if (keyboard.escapeKey.wasPressedThisFrame)
            {
                OnCancelClicked();
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}

[HarmonyPatch(typeof(GearDetailsWindow), "Update")]
[HarmonyPostfix]
public static void GearDetailsWindowUpdatePostfix(ref GearDetailsWindow __instance)
{
    try
    {
        if (LoadoutRenameDialog.IsActive)
        {
            return;
        }

        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard[RenameKey].wasPressedThisFrame)
        {
            Vector3 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();

            var loadoutButtonsField = __instance.GetType().GetField("loadoutButtons", BindingFlags.NonPublic | BindingFlags.Instance);
            var loadoutButtons = loadoutButtonsField?.GetValue(__instance) as Array;

            if (loadoutButtons != null)
            {

                for (int i = 0; i < loadoutButtons.Length; i++)
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
    }
    catch (Exception ex)
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
                var currentInfoField = hoverInfoDisplayType.GetField("currentInfo", BindingFlags.NonPublic | BindingFlags.Instance);
                var currentInfo = currentInfoField?.GetValue(hoverDisplay);

                if (currentInfo == button)
                {
                    return true;
                }
            }

            try
            {
                var camera = UnityEngine.Camera.main;
                if (camera != null)
                {
                    var mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                    var ray = camera.ScreenPointToRay(mousePos);

                    if (UnityEngine.Physics.Raycast(ray, out var hit, Mathf.Infinity))
                    {
                        var hitObject = hit.collider?.gameObject;
                        if (hitObject != null)
                        {
                            var loadoutHoverInfo = hitObject.GetComponentInParent<LoadoutHoverInfo>();
                            if (loadoutHoverInfo == button)
                            {
                                return true;
                            }
                        }
                    }

                    var pointerEventDataType = AccessTools.TypeByName("UnityEngine.EventSystems.PointerEventData");
                    if (pointerEventDataType != null)
                    {
                        var pointerEventData = Activator.CreateInstance(pointerEventDataType, UnityEngine.EventSystems.EventSystem.current);
                        pointerEventDataType.GetField("position")?.SetValue(pointerEventData, mousePos);

                        var resultList = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                        try
                        {
                            var canvasType = typeof(UnityEngine.Canvas);
                            var canvasList = UnityEngine.Object.FindObjectsOfType<UnityEngine.Canvas>();
                            foreach (var canvas in canvasList)
                            {
                                if (canvas != null)
                                {
                                    var raycaster = canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
                                    if (raycaster != null)
                                    {
                                        var raycastMethod = raycaster.GetType().GetMethod("Raycast", BindingFlags.Public | BindingFlags.Instance);
                                        if (raycastMethod != null)
                                        {
                                            resultList.Clear();
                                            raycastMethod.Invoke(raycaster, new object[] { pointerEventData, resultList });

                                            foreach (var result in resultList)
                                            {
                                                if (result.gameObject != null)
                                                {
                                                    var loadoutHover = result.gameObject.GetComponentInParent<LoadoutHoverInfo>();
                                                    if (loadoutHover == button)
                                                    {
                                                        return true;
                                                    }
                                                }
                                            }
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
            catch (Exception raycastEx)
            {
            }

            try
            {
                var hoverInfo = button.GetType().GetProperty("Active", BindingFlags.Public | BindingFlags.Instance)?.GetValue(button);
                if (hoverInfo is bool active && active)
                {
                    return true;
                }
            }
            catch (Exception hoverEx)
            {
            }

            return false;
        }
        catch (Exception ex)
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

            var mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            LoadoutRenameDialog.Show(mousePos, currentName,
                (newName) => {
                    SetLoadoutName(window, loadoutIndex, newName);
                },
                () => {
                },
                window, loadoutIndex);

        }
        catch (Exception ex)
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
            if (activeProp != null)
            {
                activeWindow = activeProp.GetValue(null);
            }

            if (activeWindow == null)
            {
                var allObjects = UnityEngine.Object.FindObjectsOfType(gearDetailsWindowType);
                if (allObjects.Length > 0)
                {
                    activeWindow = allObjects[0];
                }
            }

            if (activeWindow != null && windowLoadoutNames.TryGetValue((GearDetailsWindow)activeWindow, out var names))
            {
                if (names.TryGetValue(index, out string name))
                {
                    string key = $"{gear.Info.ID}_{gear.GetHashCode()}_{index}";
                    SparrohPlugin.Logger.LogInfo($"Persisting name for gear {gear.Info.ID} slot {index}: key='{key}' name='{name}'");
                    PlayerPrefs.SetString("LoadoutName_" + key, name);
                    PlayerPrefs.Save();
                }
            }
        }
        catch (Exception ex)
        {
        }
    }
}
