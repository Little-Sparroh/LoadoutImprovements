using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

[HarmonyPatch]
public static class LoadoutPreviewMod
{
    public class UpgradePlacement
    {
        public UpgradeInstance Upgrade;
        public int X;
        public int Y;
        public byte Rotation;
    }

    private static MonoBehaviour currentPreview;
    private static int lastHoveredIndex = -1;
    private static string currentPreviewType = null;

    private static int yOffset = 0;

    internal static ConfigEntry<bool> enableTextMode;
    internal static string CurrentPreviewMode;

    public static void ApplyPatches(Harmony harmony)
    {
        try
        {
            Patch(harmony);
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Failed to apply LoadoutPreviewMod patches: {ex.Message}");
        }
    }

    public static void OnConfigChanged()
    {
        try
        {
            UpdatePreviewMode();
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Error in LoadoutPreviewMod.OnConfigChanged: {ex.Message}");
        }
    }

    public static void UpdatePreviewMode()
    {
        try
        {
            if (enableTextMode.Value)
                CurrentPreviewMode = "text";
            else
                CurrentPreviewMode = "disabled";
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Error in LoadoutPreviewMod.UpdatePreviewMode: {ex.Message}");
        }
    }

    public static void Destroy()
    {
        try
        {
            if (currentPreview != null)
            {
                ((TextPreview)currentPreview).Hide();
                currentPreview = null;
            }
            currentPreviewType = null;
            lastHoveredIndex = -1;
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Error destroying LoadoutPreviewMod: {ex.Message}");
        }
    }

    public static void Patch(Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(GearDetailsWindow), "OnOpen"),
                      postfix: new HarmonyMethod(typeof(LoadoutPreviewMod), "OnOpenPostfix"));
        harmony.Patch(AccessTools.Method(typeof(GearDetailsWindow), "OnCloseCallback"),
                      postfix: new HarmonyMethod(typeof(LoadoutPreviewMod), "OnCloseCallbackPostfix"));
        harmony.Patch(AccessTools.Method(typeof(GearDetailsWindow), "Update"),
                      postfix: new HarmonyMethod(typeof(LoadoutPreviewMod), "UpdatePostfix"));
    }

    [HarmonyPatch(typeof(GearDetailsWindow), "OnOpen")]
    [HarmonyPostfix]
    public static void OnOpenPostfix(GearDetailsWindow __instance)
    {
        if (CurrentPreviewMode == "disabled")
        {
            currentPreview = null;
            currentPreviewType = "disabled";
            lastHoveredIndex = -1;
            return;
        }

        currentPreview = CreateTextPreview(__instance);
        currentPreviewType = CurrentPreviewMode;
        lastHoveredIndex = -1;
    }

    private static TextPreview CreateTextPreview(GearDetailsWindow window)
    {
        var previewGO = new GameObject("TextPreview");
        previewGO.transform.SetParent(window.transform, false);

        var preview = previewGO.AddComponent<TextPreview>();
        var rt = previewGO.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(10000, 10000);
        previewGO.SetActive(false);

        return preview;
    }

    [HarmonyPatch(typeof(GearDetailsWindow), "OnCloseCallback")]
    [HarmonyPostfix]
    public static void OnCloseCallbackPostfix()
    {
        if (currentPreview != null)
        {
            ((TextPreview)currentPreview).Hide();
        }
        currentPreviewType = null;
        lastHoveredIndex = -1;
    }

    [HarmonyPatch(typeof(GearDetailsWindow), "Update")]
    [HarmonyPostfix]
    public static void UpdatePostfix(GearDetailsWindow __instance)
    {
        if (currentPreviewType != CurrentPreviewMode)
        {
            if (currentPreview != null)
            {
                ((TextPreview)currentPreview).Hide();
                currentPreview = null;
            }

            if (CurrentPreviewMode == "text")
            {
                currentPreview = CreateTextPreview(__instance);
            }

            currentPreviewType = CurrentPreviewMode;
        }

        if (currentPreview == null) return;

        var loadoutButtons = Traverse.Create(__instance).Field("loadoutButtons").GetValue() as LoadoutHoverInfo[];
        if (loadoutButtons == null) return;

        int hoveredIndex = -1;
        Vector2 mousePos = PlayerInput.Controls.Menu.Point.ReadValue<Vector2>();
        for (int i = 0; i < loadoutButtons.Length; i++)
        {
            if (loadoutButtons[i] != null && IsButtonHovered(loadoutButtons[i], mousePos))
            {
                hoveredIndex = i;
                break;
            }
        }

        if (hoveredIndex != lastHoveredIndex)
        {
            if (lastHoveredIndex >= 0)
            {
                ((TextPreview)currentPreview).Hide();
            }

            if (hoveredIndex >= 0)
            {
                var upgrades = GetLoadoutUpgradePlacements(__instance, hoveredIndex);
                if (upgrades.Count > 0)
                {
                    int minY = upgrades.Min(p => p.Y);
                    if (minY < 0)
                    {
                        foreach (var p in upgrades) p.Y -= minY;
                    }

                    var pos = CalculatePreviewPosition(mousePos);
                    ((TextPreview)currentPreview).Setup(upgrades);
                    ((TextPreview)currentPreview).SetPosition(pos);
                    ((TextPreview)currentPreview).Show();
                }
            }

            lastHoveredIndex = hoveredIndex;
        }
    }

    private static bool IsButtonHovered(LoadoutHoverInfo button, Vector2 mousePos)
    {
        var eventData = new PointerEventData(EventSystem.current) { position = mousePos };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        return results.Any(r => r.gameObject == button.gameObject);
    }

    private static Vector2 CalculatePreviewPosition(Vector2 mousePos)
    {
        var uiCanvas = Menu.Instance.gameObject.GetComponentInParent<Canvas>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)uiCanvas.transform, mousePos, uiCanvas.worldCamera, out var localPos);
        localPos += new Vector2(500, -250);
        return localPos;
    }

    private static List<UpgradePlacement> GetLoadoutUpgradePlacements(GearDetailsWindow window, int loadoutIndex)
    {
        var placements = new List<UpgradePlacement>();
        try
        {
            object upgradable = Traverse.Create(window).Property("UpgradablePrefab").GetValue();
            if (upgradable == null) return placements;

            var playerDataInstance = typeof(PlayerData).GetProperty("Instance").GetValue(null);
            var getGearDataMethod = typeof(PlayerData).GetMethod("GetGearData", new Type[] { typeof(IUpgradable) });
            var gearData = getGearDataMethod?.Invoke(playerDataInstance, new object[] { upgradable });
            if (gearData == null) return placements;

            var loadoutsField = gearData.GetType().GetField("loadouts", BindingFlags.NonPublic | BindingFlags.Instance);
            var loadouts = (Array)loadoutsField?.GetValue(gearData);
            if (loadouts == null || loadoutIndex < 0 || loadoutIndex >= loadouts.Length) return placements;

            var loadout = loadouts.GetValue(loadoutIndex);
            if (loadout == null) return placements;

            var upgradesList = Traverse.Create(loadout).Field("upgrades").GetValue();
            if (upgradesList == null) return placements;

            var upgradesIList = upgradesList as System.Collections.IList;
            if (upgradesIList == null) return placements;

            for (int i = 0; i < upgradesIList.Count; i++)
            {
                var equipData = upgradesIList[i];
                if (equipData == null) continue;

                var upgrade = (UpgradeInstance)Traverse.Create(equipData).Method("GetUpgrade").GetValue();
                if (upgrade == null) continue;

                var placement = new UpgradePlacement();
                var savedX = (sbyte)Traverse.Create(equipData).Field("x").GetValue();
                var savedY = (sbyte)Traverse.Create(equipData).Field("y").GetValue();
                placement.X = savedX;
                placement.Y = savedY - yOffset;
                placement.Rotation = (byte)Traverse.Create(equipData).Field("rotation").GetValue();
                placement.Upgrade = upgrade;
                placements.Add(placement);
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError("Error getting loadout placements: " + ex.Message);
        }

        return placements;
    }
}
