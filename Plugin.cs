using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
[MycoMod(null, ModFlags.IsClientSide)]
public class ImprovedLoadoutsPlugin : BaseUnityPlugin
{
    public const string PluginGUID = "sparroh.improvedloadouts";
    public const string PluginName = "ImprovedLoadouts";
    public const string PluginVersion = "1.0.0";

    internal static new ManualLogSource Logger;
    private static Harmony harmonyInstance;

    private void Awake()
    {
        Logger = base.Logger;
        harmonyInstance = new Harmony(PluginGUID);

        harmonyInstance.PatchAll(typeof(PlayerDataPatches));

        harmonyInstance.PatchAll(typeof(GearDetailsWindowPatches));

        harmonyInstance.PatchAll(typeof(LoadoutHoverInfoPatches));

        Logger.LogInfo($"{PluginName} loaded successfully.");
    }
}

[HarmonyPatch]
public static class PlayerDataPatches
{
    private const int MAX_LOADOUT_SLOTS = 9;

    private static readonly Type LoadoutType = typeof(PlayerData).GetNestedType("Loadout", BindingFlags.NonPublic);
    private static readonly Type UpgradeEquipDataType = typeof(PlayerData).GetNestedType("UpgradeEquipData", BindingFlags.NonPublic);

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
        if (loadoutsArray == null || index < 0 || index >= loadoutsArray.Length)
        {
            __result = false;
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(PlayerData.GearData), "IncrementLoadoutIcon")]
    [HarmonyPostfix]
    public static void IncrementLoadoutIconPostfix(ref PlayerData.GearData __instance, int index, ref object ___loadouts)
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
            loadoutsArray = newArray;
        }

        object loadout = loadoutsArray.GetValue(index);
        if (loadout == null)
        {
            return;
        }


        try {
            var upgradeField = LoadoutType.GetField("upgrades", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (upgradeField != null) {
                var rawValue = upgradeField.GetValue(loadout);
                var typedList = rawValue as System.Collections.IList;
                int upgradeCount = typedList?.Count ?? 0;
            }

            var rawUpgrades = upgradeField?.GetValue(loadout);
            var upgradesTypedList = rawUpgrades as System.Collections.IList;
            int upgradeCountFinal = upgradesTypedList?.Count ?? 0;

            int defaultIconCount = 0;
            var globalType = AccessTools.TypeByName("Global");
            var globalInstance = globalType.GetProperty("Instance")?.GetValue(null);
            var loadoutIcons = globalType.GetProperty("LoadoutIcons")?.GetValue(globalInstance) as Array;
            if (loadoutIcons != null)
            {
                defaultIconCount = loadoutIcons.Length;
            }

            int totalIcons = upgradeCountFinal + defaultIconCount;

            int currentIconIndex = LoadoutType.GetField("iconIndex")?.GetValue(loadout) is int value ? value : 0;
            int newIconIndex = (currentIconIndex + 1) % Mathf.Max(1, totalIcons);

            LoadoutType.GetField("iconIndex")?.SetValue(loadout, newIconIndex);


            try {

            var gearDetailsWindowType = AccessTools.TypeByName("GearDetailsWindow");

            object activeWindow = null;
            var activeProp = gearDetailsWindowType.GetProperty("Active", BindingFlags.Public | BindingFlags.Static);
            if (activeProp != null) {
                activeWindow = activeProp.GetValue(null);
            }

            if (activeWindow == null) {
                var allObjects = UnityEngine.Object.FindObjectsOfType(gearDetailsWindowType);
                if (allObjects.Length > 0) {
                    activeWindow = allObjects[0];
                }
            }

                if (activeWindow != null) {
                    var updateMethod = gearDetailsWindowType.GetMethod("UpdateLoadoutIcon", BindingFlags.NonPublic | BindingFlags.Instance);

                    if (updateMethod != null) {
                        var loadoutButtonsField = gearDetailsWindowType.GetField("loadoutButtons", BindingFlags.NonPublic | BindingFlags.Instance);
                        var loadoutButtons = loadoutButtonsField?.GetValue(activeWindow) as Array;

                        if (loadoutButtons != null && index < loadoutButtons.Length) {
                            var button = loadoutButtons.GetValue(index);
                            if (button != null) {
                                updateMethod.Invoke(activeWindow, new object[] { button, index });
                            } else {
                            }
                        } else {
                        }
                    } else {
                    }
                } else {
                }
            }
            catch (Exception uiEx) {
            }
        }
        catch (Exception ex) {
        }
    }

    [HarmonyPatch(typeof(PlayerData.GearData), "GetLoadoutIcon")]
    [HarmonyPostfix]
    public static void GetLoadoutIconPostfix(ref PlayerData.GearData __instance, int index, ref object ___loadouts, ref Sprite __result)
    {
        Array loadoutsArray = ___loadouts as Array;
        if (loadoutsArray == null || index < 0 || index >= loadoutsArray.Length)
        {
            return;
        }

        object loadout = loadoutsArray.GetValue(index);
        if (loadout == null)
        {
            return;
        }


        try {
            var upgradeField = LoadoutType.GetField("upgrades", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (upgradeField != null) {
            }

            var rawUpgrades = upgradeField?.GetValue(loadout);

            var typedList = rawUpgrades as System.Collections.IList;
            int upgradeCount = typedList?.Count ?? 0;

            if (upgradeCount == 0)
            {
                return;
            }
        }
        catch (Exception ex) {
            return;
        }

        try {
            var upgradeField = LoadoutType.GetField("upgrades", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var rawUpgradesList = upgradeField?.GetValue(loadout);
            var upgradesIList = rawUpgradesList as System.Collections.IList;

            if (upgradesIList == null || upgradesIList.Count == 0) {
                return;
            }

            int finalUpgradeCount = upgradesIList.Count;

            int iconIndexField = LoadoutType.GetField("iconIndex")?.GetValue(loadout) is int currentIconIndex ? currentIconIndex : 0;

            if (iconIndexField < finalUpgradeCount)
            {
                object upgradeEquipData = upgradesIList[iconIndexField];

                object upgrade = null;
                try {
                    var getUpgradeMethod = UpgradeEquipDataType.GetMethod("GetUpgrade", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                    if (getUpgradeMethod != null) {
                        var upgradeInstance = getUpgradeMethod.Invoke(upgradeEquipData, new object[0]);

                        if (upgradeInstance != null) {
                            var upgradeInstanceType = upgradeInstance.GetType();
                            var upgradeProperty = upgradeInstanceType.GetProperty("Upgrade", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                            if (upgradeProperty != null) {
                                upgrade = upgradeProperty.GetValue(upgradeInstance);

                                var upgradeType = AccessTools.TypeByName("Upgrade");
                                Sprite icon = upgradeType.GetProperty("Icon")?.GetValue(upgrade) as Sprite;

                            } else {
                            }
                        } else {
                        }
                    } else {
                    }
                }
                catch (Exception ex) {
                }

                if (upgrade != null)
                {
                    var upgradeType = AccessTools.TypeByName("Upgrade");
                    Sprite icon = upgradeType.GetProperty("Icon")?.GetValue(upgrade) as Sprite;
                    if (icon != null)
                    {
                        __result = icon;
                        return;
                    } else {
                    }
                } else {
                }
            } else {
            }
        }
        catch (Exception ex) {
            return;
        }

        try {
            var globalType = AccessTools.TypeByName("Global");
            var globalInstance = globalType.GetProperty("Instance")?.GetValue(null);
            var loadoutIcons = globalType.GetProperty("LoadoutIcons")?.GetValue(globalInstance) as Array;
            if (loadoutIcons != null)
            {
                int currentIconIndexFallback = LoadoutType.GetField("iconIndex")?.GetValue(loadout) is int fallbackValue ? fallbackValue : 0;
                var upgradeFieldFallback = LoadoutType.GetField("upgrades", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var rawUpgradesFallback = upgradeFieldFallback?.GetValue(loadout);
                var fallbackUpgradesList = rawUpgradesFallback as System.Collections.IList;
                int currentUpgradeCountFallback = fallbackUpgradesList?.Count ?? 0;

                int defaultIconIndex = currentIconIndexFallback - currentUpgradeCountFallback;
                if (defaultIconIndex >= 0 && defaultIconIndex < loadoutIcons.Length)
                {
                    __result = loadoutIcons.GetValue(defaultIconIndex) as Sprite;
                    return;
                }
            }
        }
        catch (Exception ex) {
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

    private static readonly System.Collections.Generic.Dictionary<GearDetailsWindow, System.Collections.Generic.Dictionary<int, string>> windowLoadoutNames =
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

                var loadoutButtonsField = window.GetType().GetField("loadoutButtons", BindingFlags.NonPublic | BindingFlags.Instance);
                var loadoutButtons = loadoutButtonsField?.GetValue(window) as Array;
                if (loadoutButtons != null && loadoutIndex < loadoutButtons.Length)
                {
                    var button = loadoutButtons.GetValue(loadoutIndex) as LoadoutHoverInfo;
                    if (button != null)
                    {
                        try
                        {
                            var hoverInfoDisplayType = AccessTools.TypeByName("HoverInfoDisplay");
                            var instanceProp = hoverInfoDisplayType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                            var hoverDisplay = instanceProp?.GetValue(null);

                            if (hoverDisplay != null)
                            {
                                var refreshMethod = hoverInfoDisplayType.GetMethod("Refresh", BindingFlags.Public | BindingFlags.Instance);
                                var deactivateMethod = hoverInfoDisplayType.GetMethod("Deactivate", BindingFlags.Public | BindingFlags.Instance);

                                if (refreshMethod != null)
                                {
                                    refreshMethod.Invoke(hoverDisplay, new object[0]);
                                }
                                else
                                {
                                    deactivateMethod?.Invoke(hoverDisplay, new object[0]);
                                }
                            }
                        }
                        catch (Exception refreshEx)
                        {
                        }
                    }
                }
            }
            else
            {
                windowNames.Remove(loadoutIndex);
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
                                title = $"{customName} [L to rename]";
                                color = Color.white;
                                __result = true;
                                return;
                            }
                            else
                            {
                                title = $"Loadout {i + 1} [L to rename]";
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
        if (keyboard != null && keyboard.lKey.wasPressedThisFrame)
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

                if (loadoutButtons.Length > 0)
                {
                    var firstButton = loadoutButtons.GetValue(0) as LoadoutHoverInfo;
                    if (firstButton != null)
                    {
                        StartRenameProcess(firstButton, 0, __instance);
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
    }
}
