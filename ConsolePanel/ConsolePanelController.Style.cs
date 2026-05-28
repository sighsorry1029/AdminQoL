using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ConsolePanel;
internal sealed partial class ConsolePanelController
{
    private void ApplySurfaceImage(Image image, SurfaceRole role, Color fallbackColor, bool preferInventoryPanel = false)
    {
        image.raycastTarget = true;
        if (ConsolePanelModule.VisualStyle.Value == PanelVisualStyle.Modern)
        {
            image.sprite = GetSolidUiSprite();
            image.material = null;
            image.type = Image.Type.Simple;
            image.color = ModernColor(role, fallbackColor.a);
            return;
        }

        image.color = fallbackColor;

        if (preferInventoryPanel || role is SurfaceRole.Header or SurfaceRole.Search or SurfaceRole.Button or SurfaceRole.FavoriteBar)
        {
            _inventoryPanelStyle ??= FindInventoryPanelStyle();
            PanelImageStyle? style = _inventoryPanelStyle;
            if (style != null)
            {
                image.sprite = style.Sprite;
                image.material = style.Material;
                image.color = role == SurfaceRole.Panel
                    ? WithAlpha(style.Color, fallbackColor.a)
                    : WithAlpha(MultiplyColor(style.Color, WoodSpriteTint()), fallbackColor.a * WoodSpriteTint().a);
                image.type = style.Type;
                return;
            }
        }

        image.sprite = null;
        image.material = null;
        image.type = Image.Type.Simple;
    }

    private static Sprite GetSolidUiSprite()
    {
        if (SolidUiSprite != null)
        {
            return SolidUiSprite;
        }

        Texture2D texture = new(1, 1, TextureFormat.RGBA32, mipChain: false)
        {
            name = "ConsolePanel_SolidUiTexture",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Point
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        Object.DontDestroyOnLoad(texture);
        SolidUiSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
        SolidUiSprite.name = "ConsolePanel_SolidUiSprite";
        Object.DontDestroyOnLoad(SolidUiSprite);
        return SolidUiSprite;
    }

    private static Color ModernColor(SurfaceRole role, float alpha)
    {
        Color color = role switch
        {
            SurfaceRole.Panel => new Color(0.12f, 0.115f, 0.105f, 0.66f),
            SurfaceRole.Header => new Color(0.55f, 0.42f, 0.30f, 0.76f),
            SurfaceRole.Search => new Color(0.20f, 0.18f, 0.16f, 0.82f),
            SurfaceRole.Scroll => new Color(0.28f, 0.25f, 0.23f, 0.42f),
            SurfaceRole.Info => new Color(0.55f, 0.42f, 0.30f, 0.68f),
            SurfaceRole.MutedInfo => new Color(0.36f, 0.32f, 0.30f, 0.58f),
            SurfaceRole.Button => new Color(0.38f, 0.34f, 0.31f, 0.82f),
            SurfaceRole.FavoriteBar => new Color(0.28f, 0.25f, 0.23f, 0.78f),
            SurfaceRole.Tooltip => new Color(0.16f, 0.14f, 0.13f, 0.94f),
            SurfaceRole.ScrollbarBackground => new Color(0.08f, 0.075f, 0.07f, 0.55f),
            SurfaceRole.ScrollbarHandle => AccentColor(alpha),
            _ => new Color(0.36f, 0.32f, 0.30f, alpha)
        };
        if (role != SurfaceRole.ScrollbarHandle)
        {
            color.a = Mathf.Min(color.a, alpha);
        }

        return color;
    }

    private static PanelImageStyle? FindInventoryPanelStyle()
    {
        Sprite? sprite = FindLoadedSprite("woodpanel_trophys");
        if (sprite == null)
        {
            ConsolePanelModule.Diagnostic("wood panel style unavailable: sprite 'woodpanel_trophys' was not found");
            return null;
        }

        Material? material = FindLoadedMaterial("litpanel");
        ConsolePanelModule.Diagnostic($"wood panel style selected: sprite='{sprite.name}', material='{material?.name ?? "none"}', type={Image.Type.Sliced}");
        return PanelImageStyle.From(sprite, material, Color.white, Image.Type.Sliced);
    }

    private static TMP_FontAsset? FindFontAsset()
    {
        TMP_FontAsset? consoleFont = global::Console.instance?.m_output?.font
                                     ?? global::Console.instance?.m_search?.font;
        if (consoleFont != null)
        {
            ConsolePanelModule.Diagnostic($"using TMP font from console: '{consoleFont.name}'");
            return consoleFont;
        }

        try
        {
            string[] preferred =
            {
                "Valheim-AveriaSansLibre",
                "Valheim-Norse",
                "AveriaSansLibre",
                "Norse",
                "LiberationSans SDF"
            };

            List<TMP_FontAsset> fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>()
                .Where(font => font != null)
                .ToList();

            foreach (string name in preferred)
            {
                TMP_FontAsset? match = fonts.FirstOrDefault(font => string.Equals(font.name, name, StringComparison.OrdinalIgnoreCase))
                                       ?? fonts.FirstOrDefault(font => font.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
                if (match != null)
                {
                    ConsolePanelModule.Diagnostic($"using TMP font from resources: '{match.name}'");
                    return match;
                }
            }

            TMP_FontAsset? fallback = fonts.FirstOrDefault();
            if (fallback != null)
            {
                ConsolePanelModule.Diagnostic($"using first loaded TMP font: '{fallback.name}'");
                return fallback;
            }
        }
        catch (Exception ex)
        {
            ConsolePanelModule.Diagnostic($"failed to find loaded TMP font: {ex.Message}");
        }

        try
        {
            Font osFont = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Segoe UI", "Malgun Gothic" }, 16);
            TMP_FontAsset runtimeFont = TMP_FontAsset.CreateFontAsset(osFont);
            runtimeFont.name = "ConsolePanel_RuntimeTMPFont";
            ConsolePanelModule.Diagnostic("created runtime TMP font asset");
            return runtimeFont;
        }
        catch (Exception ex)
        {
            ConsolePanelModule.Diagnostic($"failed to create runtime TMP font: {ex.Message}");
            return null;
        }
    }

    private void EnsureTmpDefaultFont()
    {
        _font ??= FindFontAsset();
        if (_font == null || _tmpDefaultFontApplied)
        {
            return;
        }

        try
        {
            TMP_Settings.defaultFontAsset = _font;
            _tmpDefaultFontApplied = true;
            ConsolePanelModule.Diagnostic($"set TMP default font asset to '{_font.name}'");
        }
        catch (Exception ex)
        {
            ConsolePanelModule.Diagnostic($"failed to set TMP default font asset: {ex.Message}");
        }
    }

    private static Sprite? FindLoadedSprite(string spriteName)
    {
        try
        {
            Sprite? directSprite = Resources.FindObjectsOfTypeAll<Sprite>()
                .FirstOrDefault(sprite => sprite != null && string.Equals(sprite.name, spriteName, StringComparison.OrdinalIgnoreCase));
            if (directSprite != null)
            {
                return directSprite;
            }

            foreach (SpriteAtlas atlas in Resources.FindObjectsOfTypeAll<SpriteAtlas>())
            {
                Sprite sprite = atlas.GetSprite(spriteName);
                if (sprite != null)
                {
                    return sprite;
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static Material? FindLoadedMaterial(string materialName)
    {
        try
        {
            return Resources.FindObjectsOfTypeAll<Material>()
                .FirstOrDefault(material => material != null && string.Equals(material.name, materialName, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private static Color MultiplyColor(Color baseColor, Color tint)
    {
        return new Color(
            Mathf.Clamp01(baseColor.r * tint.r),
            Mathf.Clamp01(baseColor.g * tint.g),
            Mathf.Clamp01(baseColor.b * tint.b),
            Mathf.Clamp01(baseColor.a * tint.a));
    }

    private static Color AccentColor(float alpha = 1f)
    {
        return ConsolePanelModule.VisualStyle.Value == PanelVisualStyle.Modern
            ? new Color(1f, 0.90f, 0.16f, alpha)
            : new Color(1f, 0.70f, 0.32f, alpha);
    }

    private static Color AccentHighlightColor()
    {
        return ConsolePanelModule.VisualStyle.Value == PanelVisualStyle.Modern
            ? new Color(1f, 0.96f, 0.32f, 1f)
            : new Color(1f, 0.78f, 0.40f, 1f);
    }

    private static Color AccentPressedColor()
    {
        return ConsolePanelModule.VisualStyle.Value == PanelVisualStyle.Modern
            ? new Color(1f, 0.98f, 0.50f, 1f)
            : new Color(1f, 0.84f, 0.50f, 1f);
    }

    private static Color ButtonTextColor()
    {
        return new Color(1f, 0.92f, 0.70f, 1f);
    }

    private static Color SelectedButtonTextColor()
    {
        return ConsolePanelModule.VisualStyle.Value == PanelVisualStyle.Modern
            ? new Color(0.12f, 0.08f, 0.035f, 1f)
            : ButtonTextColor();
    }

    private static Color SelectedButtonBackgroundColor(float alpha = 1f)
    {
        if (ConsolePanelModule.VisualStyle.Value == PanelVisualStyle.Modern)
        {
            return new Color(0.78f, 0.57f, 0.29f, alpha);
        }

        Color tint = WoodSelectedTint();
        tint.a = Mathf.Clamp01(tint.a * alpha);
        return tint;
    }

    private static Color WoodSpriteTint()
    {
        return new Color(1f, 1f, 1f, 76f / 255f);
    }

    private static Color WoodSelectedTint()
    {
        return new Color(0f, 1f, 1f, 1f);
    }

    private static Color ButtonNormalColor()
    {
        return ConsolePanelModule.VisualStyle.Value == PanelVisualStyle.Modern
            ? new Color(0.43f, 0.38f, 0.34f, 0.86f)
            : new Color(0.42f, 0.32f, 0.16f, 0.95f);
    }

    private static Color ButtonHighlightColor()
    {
        return ConsolePanelModule.VisualStyle.Value == PanelVisualStyle.Modern
            ? new Color(0.56f, 0.45f, 0.34f, 0.96f)
            : new Color(0.60f, 0.48f, 0.25f, 1f);
    }

    private static Color ButtonPressedColor()
    {
        return ConsolePanelModule.VisualStyle.Value == PanelVisualStyle.Modern
            ? new Color(0.88f, 0.50f, 0.10f, 1f)
            : new Color(0.78f, 0.64f, 0.32f, 1f);
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            child.gameObject.SetActive(false);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
    }
}
