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
internal enum PanelVisualStyle
{
    Wood,
    Modern
}
internal sealed class PanelImageStyle
{
    internal Sprite Sprite { get; }
    internal Material? Material { get; }
    internal Color Color { get; }
    internal Image.Type Type { get; }

    private PanelImageStyle(Sprite sprite, Material? material, Color color, Image.Type type)
    {
        Sprite = sprite;
        Material = material;
        Color = color;
        Type = type == Image.Type.Filled ? Image.Type.Simple : type;
    }

    internal static PanelImageStyle From(Image image)
    {
        Sprite sprite = image.sprite!;
        Image.Type type = image.type;
        if (type == Image.Type.Simple && sprite.border.sqrMagnitude > 0f)
        {
            type = Image.Type.Sliced;
        }

        return new PanelImageStyle(sprite, image.material, image.color, type);
    }

    internal static PanelImageStyle From(Sprite sprite, Color color, Image.Type type)
    {
        return new PanelImageStyle(sprite, null, color, type);
    }

    internal static PanelImageStyle From(Sprite sprite, Material? material, Color color, Image.Type type)
    {
        return new PanelImageStyle(sprite, material, color, type);
    }
}
internal readonly struct RectTransformState
{
    internal readonly Vector2 AnchorMin;
    internal readonly Vector2 AnchorMax;
    internal readonly Vector2 Pivot;
    internal readonly Vector2 AnchoredPosition;
    internal readonly Vector2 SizeDelta;
    internal readonly Vector2 OffsetMin;
    internal readonly Vector2 OffsetMax;

    private RectTransformState(RectTransform rect)
    {
        AnchorMin = rect.anchorMin;
        AnchorMax = rect.anchorMax;
        Pivot = rect.pivot;
        AnchoredPosition = rect.anchoredPosition;
        SizeDelta = rect.sizeDelta;
        OffsetMin = rect.offsetMin;
        OffsetMax = rect.offsetMax;
    }

    internal static RectTransformState Capture(RectTransform rect)
    {
        return new RectTransformState(rect);
    }

    internal void Apply(RectTransform rect)
    {
        rect.anchorMin = AnchorMin;
        rect.anchorMax = AnchorMax;
        rect.pivot = Pivot;
        rect.anchoredPosition = AnchoredPosition;
        rect.sizeDelta = SizeDelta;
        rect.offsetMin = OffsetMin;
        rect.offsetMax = OffsetMax;
    }
}

internal readonly struct CommandEntry
{
    internal string Command { get; }
    internal string Description { get; }
    internal string Owner { get; }

    internal CommandEntry(string command, string description, string owner)
    {
        Command = command;
        Description = description;
        Owner = owner;
    }
}

internal readonly struct FavoriteActionBinding
{
    internal int FavoriteIndex { get; }
    internal Button Button { get; }

    internal FavoriteActionBinding(int favoriteIndex, Button button)
    {
        FavoriteIndex = favoriteIndex;
        Button = button;
    }
}
