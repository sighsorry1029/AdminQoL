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
internal static class ConsolePanelInputBlock
{
    private static bool _active;
    private static bool _hasStoredMouseCapture;
    private static bool _previousMouseCapture;

    internal static bool IsActive => _active;

    internal static void SetActive(bool active, bool releaseMouseCapture = false)
    {
        if (_active == active)
        {
            if (active && releaseMouseCapture)
            {
                ApplyMouseCaptureBlock();
            }
            else if (active)
            {
                RestoreMouseCapture();
            }

            return;
        }

        _active = active;
        if (active && releaseMouseCapture)
        {
            ApplyMouseCaptureBlock();
        }
        else
        {
            RestoreMouseCapture();
        }
    }

    private static void ApplyMouseCaptureBlock()
    {
        GameCamera camera = GameCamera.instance;
        if (camera == null)
        {
            return;
        }

        if (!_hasStoredMouseCapture)
        {
            _previousMouseCapture = camera.m_mouseCapture;
            _hasStoredMouseCapture = true;
        }

        camera.m_mouseCapture = false;
        camera.UpdateMouseCapture();
    }

    private static void RestoreMouseCapture()
    {
        GameCamera camera = GameCamera.instance;
        if (camera != null && _hasStoredMouseCapture)
        {
            camera.m_mouseCapture = _previousMouseCapture;
            camera.UpdateMouseCapture();
        }

        _hasStoredMouseCapture = false;
    }
}
