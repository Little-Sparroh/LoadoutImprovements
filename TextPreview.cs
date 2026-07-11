using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sparroh.UI;

public class TextPreview : MonoBehaviour
{
    private CanvasGroup canvasGroup;
    private readonly List<UIText> textElements = new List<UIText>();
    private UIPanel panel;

    private void Awake()
    {
        UITheme.Initialize();
        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0.95f;

        // Use themed panel as background
        var img = gameObject.GetComponent<Image>();
        if (img == null)
            img = gameObject.AddComponent<Image>();
        img.color = UIColors.TooltipBg;
        UIFactory.ApplyWhiteSprite(img);

        var border = gameObject.AddComponent<Outline>();
        border.effectColor = UIColors.Border;
        border.effectDistance = new Vector2(1f, -1f);

        UIFactory.AddVerticalLayout(gameObject,
            UITheme.S(UITheme.SpacingTight),
            UITheme.ScaledPadding(10, 10, 10, 10),
            TextAnchor.UpperLeft,
            controlChildHeight: true,
            expandHeight: false);

        UIFactory.AddContentSizeFitter(gameObject,
            ContentSizeFitter.FitMode.PreferredSize,
            ContentSizeFitter.FitMode.PreferredSize);
    }

    public void Setup(List<LoadoutPreviewMod.UpgradePlacement> placements)
    {
        Clear();

        foreach (var placement in placements)
        {
            if (placement.Upgrade == null) continue;

            var uiText = UIText.Create(transform, "TextElement",
                placement.Upgrade.Upgrade.Name,
                UITheme.ScaledFontSmall,
                placement.Upgrade.Upgrade.Color,
                TextAlignmentOptions.Left);
            UIHelpers.EnsureLayoutElement(uiText.GameObject, preferredHeight: UITheme.S(18f));
            textElements.Add(uiText);
        }
    }

    private void Clear()
    {
        foreach (var text in textElements)
        {
            if (text != null && text.GameObject != null)
                Destroy(text.GameObject);
        }
        textElements.Clear();
    }

    public void SetPosition(Vector2 position)
    {
        ((RectTransform)transform).anchoredPosition = position;
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
