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
internal static class ConsolePanelPointer
{
    internal static bool IsPrimaryMouseDown()
    {
        try
        {
            if (ZInput.GetMouseButtonDown(0))
            {
                return true;
            }
        }
        catch
        {
        }

        return Input.GetMouseButtonDown(0);
    }

    internal static bool IsPrimaryMouseHeld()
    {
        try
        {
            return Input.GetMouseButton(0);
        }
        catch
        {
            return false;
        }
    }

    internal static float GetWheelDelta()
    {
        try
        {
            return Input.mouseScrollDelta.y;
        }
        catch
        {
            return 0f;
        }
    }

    internal static Vector3 GetMousePosition()
    {
        try
        {
            return ZInput.mousePosition;
        }
        catch
        {
            return Input.mousePosition;
        }
    }

    internal static bool IsActuallyVisible(Transform transform)
    {
        for (Transform? current = transform; current != null; current = current.parent)
        {
            if (!current.gameObject.activeInHierarchy)
            {
                return false;
            }
        }

        return true;
    }

    internal static bool IsInsideScrollViewport(RectTransform rect, Vector3 mousePosition)
    {
        ScrollRect? scrollRect = rect.GetComponentInParent<ScrollRect>();
        RectTransform? viewport = scrollRect?.viewport;
        return viewport == null || RectTransformUtility.RectangleContainsScreenPoint(viewport, mousePosition, null);
    }
}

internal sealed class ManualClickForwarder : MonoBehaviour
{
    private Action? _onClick;
    private RectTransform? _rectTransform;
    private int _lastDispatchFrame = -1;

    internal void Configure(Action onClick)
    {
        _onClick = onClick;
        _rectTransform = transform as RectTransform;
    }

    private void Awake()
    {
        _rectTransform = transform as RectTransform;
    }

    private void Update()
    {
        if (!ConsolePanelInputBlock.IsActive || _onClick == null || _rectTransform == null || !ConsolePanelPointer.IsPrimaryMouseDown())
        {
            return;
        }

        if (_lastDispatchFrame == Time.frameCount)
        {
            return;
        }

        if (!ConsolePanelPointer.IsActuallyVisible(transform))
        {
            return;
        }

        Vector3 mousePosition = ConsolePanelPointer.GetMousePosition();
        if (!RectTransformUtility.RectangleContainsScreenPoint(_rectTransform, mousePosition, null))
        {
            return;
        }

        if (!ConsolePanelPointer.IsInsideScrollViewport(_rectTransform, mousePosition))
        {
            return;
        }

        _lastDispatchFrame = Time.frameCount;
        ConsolePanelModule.Diagnostic($"manual click dispatched on '{gameObject.name}' at {mousePosition}");
        _onClick();
    }
}
internal sealed class ManualHoverTooltip : MonoBehaviour
{
    private string _text = "";
    private Action<string, Vector3>? _show;
    private Action? _hide;
    private RectTransform? _rectTransform;
    private bool _hovering;

    internal void Configure(string text, Action<string, Vector3> show, Action hide)
    {
        _text = text;
        _show = show;
        _hide = hide;
        _rectTransform = transform as RectTransform;
    }

    private void Awake()
    {
        _rectTransform = transform as RectTransform;
    }

    private void OnDisable()
    {
        StopHovering();
    }

    private void OnDestroy()
    {
        StopHovering();
    }

    private void Update()
    {
        if (!ConsolePanelInputBlock.IsActive || _rectTransform == null || _show == null)
        {
            StopHovering();
            return;
        }

        Vector3 mousePosition = ConsolePanelPointer.GetMousePosition();
        bool inside = ConsolePanelPointer.IsActuallyVisible(transform)
                      && RectTransformUtility.RectangleContainsScreenPoint(_rectTransform, mousePosition, null)
                      && ConsolePanelPointer.IsInsideScrollViewport(_rectTransform, mousePosition);
        if (!inside)
        {
            StopHovering();
            return;
        }

        _hovering = true;
        _show(_text, mousePosition);
    }

    private void StopHovering()
    {
        if (!_hovering)
        {
            return;
        }

        _hovering = false;
        _hide?.Invoke();
    }
}
internal sealed class ManualScrollForwarder : MonoBehaviour
{
    private ScrollRect? _scrollRect;
    private RectTransform? _viewport;

    internal void Configure(ScrollRect scrollRect)
    {
        _scrollRect = scrollRect;
        _viewport = scrollRect.viewport;
    }

    private void Update()
    {
        if (!ConsolePanelInputBlock.IsActive || _scrollRect == null || _scrollRect.content == null)
        {
            return;
        }

        _viewport ??= _scrollRect.viewport;
        if (_viewport == null || !_scrollRect.gameObject.activeInHierarchy)
        {
            return;
        }

        float wheel = ConsolePanelPointer.GetWheelDelta();
        if (Mathf.Abs(wheel) < 0.001f)
        {
            return;
        }

        Vector3 mousePosition = ConsolePanelPointer.GetMousePosition();
        if (!RectTransformUtility.RectangleContainsScreenPoint(_viewport, mousePosition, null))
        {
            return;
        }

        float scrollablePixels = Mathf.Max(1f, _scrollRect.content.rect.height - _viewport.rect.height);
        float sensitivity = ConsolePanelLayout.ScrollSensitivity;
        _scrollRect.StopMovement();
        _scrollRect.verticalNormalizedPosition = Mathf.Clamp01(_scrollRect.verticalNormalizedPosition + wheel * sensitivity / scrollablePixels);
    }
}
