using System;
using System.Collections;
using Sparroh.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class LoadoutRenameDialog : MonoBehaviour
{
    private static LoadoutRenameDialog Instance;

    private UIInputField inputField;
    private Action<string> onApplyCallback;
    private Action onCancelCallback;
    private UIWindow window;

    public static bool IsActive => Instance != null;

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            OnApplyClicked();
        else if (keyboard.escapeKey.wasPressedThisFrame)
            OnCancelClicked();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }


    public static void Show(Vector2 screenPosition, string currentName, Action<string> onApply, Action onCancel,
        GearDetailsWindow targetWindow, int targetLoadoutIndex)
    {
        if (IsActive)
            Close();

        UITheme.Initialize();

        var window = UIWindow.Create(
            "LoadoutRename",
            new Vector2(420f, 200f),
            "Rename Loadout",
            false,
            true,
            UITheme.DialogSortingOrder);

        var hostGo = new GameObject("LoadoutRenameDialogHost");
        hostGo.transform.SetParent(window.Canvas.transform, false);
        var dialog = hostGo.AddComponent<LoadoutRenameDialog>();
        dialog.Initialize(window, currentName, onApply, onCancel);

        Instance = dialog;
    }

    public static void Close()
    {
        if (Instance == null) return;

        var dialog = Instance;
        Instance = null;

        dialog.onApplyCallback = null;
        dialog.onCancelCallback = null;


        var win = dialog.window;
        dialog.window = null;
        win?.Destroy();
    }


    private void Initialize(UIWindow uiWindow, string currentName, Action<string> onApply, Action onCancel)
    {
        window = uiWindow;
        onApplyCallback = onApply;
        onCancelCallback = onCancel;


        window.OnClose(OnCancelClicked);

        var content = window.Content;
        UIFactory.AddVerticalLayout(content.gameObject,
            UITheme.S(UITheme.SpacingNormal),
            UITheme.ScaledPadding(16, 16, 12, 12),
            TextAnchor.MiddleCenter);

        inputField = UIInputField.Create(content, currentName ?? string.Empty, "Loadout name");
        UIHelpers.EnsureLayoutElement(inputField.GameObject,
            preferredHeight: UITheme.ScaledInputHeight + UITheme.S(4f),
            minHeight: UITheme.ScaledInputHeight);

        var buttons = UIFactory.CreateRect("Buttons", content);
        UIHelpers.EnsureLayoutElement(buttons.gameObject,
            preferredHeight: UITheme.ScaledButtonHeight + UITheme.S(8f));
        UIFactory.AddHorizontalLayout(buttons.gameObject, UITheme.S(12f),
            new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter, false);

        UIButton.Create(buttons, "Cancel", OnCancelClicked)
            .SetWidth(UITheme.S(120f));

        UIButton.Create(buttons, "Apply", OnApplyClicked, UIButtonStyle.Primary)
            .SetWidth(UITheme.S(120f));


        StartCoroutine(FocusInputNextFrame());
    }

    private IEnumerator FocusInputNextFrame()
    {
        yield return null;
        FocusInput(true);
        yield return null;
        if (inputField?.Input != null && !inputField.Input.isFocused)
            FocusInput(true);
    }

    private void FocusInput(bool reenable = false)
    {
        if (inputField?.Input == null) return;

        var input = inputField.Input;


        if (reenable)
        {
            input.enabled = false;
            input.enabled = true;
        }

        var es = EventSystem.current;
        if (es != null)
            es.SetSelectedGameObject(inputField.GameObject);

        input.ActivateInputField();
        input.Select();

        var text = inputField.Text ?? string.Empty;
        input.caretPosition = text.Length;
        input.selectionAnchorPosition = text.Length;
        input.selectionFocusPosition = text.Length;
        input.ForceLabelUpdate();
    }


    private void OnApplyClicked()
    {
        if (Instance != this) return;

        var text = inputField?.Text;
        var apply = onApplyCallback;

        onCancelCallback = null;
        onApplyCallback = null;

        if (!string.IsNullOrWhiteSpace(text))
            apply?.Invoke(text);

        Close();
    }

    private void OnCancelClicked()
    {
        if (Instance != this) return;

        var cancel = onCancelCallback;
        onCancelCallback = null;
        onApplyCallback = null;

        cancel?.Invoke();
        Close();
    }
}