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
    private void PutCommandInInput(CommandEntry entry)
    {
        PutCommandInInput(entry.Command);
    }

    private void PutCommandInInput(string command)
    {
        if (global::Console.instance?.m_input == null)
        {
            return;
        }

        EndSearchInputFocus();
        TMP_InputField input = (TMP_InputField)(object)global::Console.instance.m_input;
        input.text = command;
        input.caretPosition = input.text.Length;
        global::Console.instance.m_input.ActivateInputField();
    }

    private void BeginSearchInputFocus()
    {
        _searchInputWanted = true;
        TMP_InputField? consoleInput = GetConsoleInputField();
        if (consoleInput != null)
        {
            CaptureConsoleInputLock(consoleInput);
            _consoleInputSnapshot = consoleInput.text ?? "";
            _hasConsoleInputSnapshot = true;
            consoleInput.readOnly = true;
            consoleInput.interactable = false;
            consoleInput.DeactivateInputField();
        }
    }

    private void EndSearchInputFocus()
    {
        if (_searchInputField != null)
        {
            _searchInputField.DeactivateInputField();
        }

        ClearSearchFocusState();
    }

    private bool ReleaseSearchFocusForConsoleClick()
    {
        if (!_searchInputWanted || !IsPrimaryMouseDown())
        {
            return false;
        }

        TMP_InputField? consoleInput = GetConsoleInputField();
        if (consoleInput == null || !IsPointerInsideInput(consoleInput))
        {
            return false;
        }

        EndSearchInputFocus();
        EventSystem.current?.SetSelectedGameObject(consoleInput.gameObject);
        consoleInput.Select();
        consoleInput.ActivateInputField();
        consoleInput.caretPosition = consoleInput.text?.Length ?? 0;
        return true;
    }

    private void ClearSearchFocusState()
    {
        _searchInputWanted = false;
        _hasConsoleInputSnapshot = false;
        _consoleInputSnapshot = "";
        RestoreConsoleInputLock();
    }

    private void EnforceExclusiveInputFocus()
    {
        if (_searchInputField == null)
        {
            ClearSearchFocusState();
            return;
        }

        if (!_searchInputWanted && !_searchInputField.isFocused)
        {
            ClearSearchFocusState();
            return;
        }

        TMP_InputField? consoleInput = GetConsoleInputField();
        if (consoleInput == null || ReferenceEquals(consoleInput, _searchInputField))
        {
            return;
        }

        CaptureConsoleInputLock(consoleInput);
        consoleInput.readOnly = true;
        consoleInput.interactable = false;
        if (!_hasConsoleInputSnapshot)
        {
            _consoleInputSnapshot = consoleInput.text ?? "";
            _hasConsoleInputSnapshot = true;
        }

        if (!string.Equals(consoleInput.text, _consoleInputSnapshot, StringComparison.Ordinal))
        {
            consoleInput.text = _consoleInputSnapshot;
            consoleInput.caretPosition = consoleInput.text.Length;
        }

        if (consoleInput.isFocused)
        {
            consoleInput.DeactivateInputField();
        }

        EventSystem.current?.SetSelectedGameObject(_searchInputField.gameObject);
        _searchInputField.Select();
        _searchInputField.ActivateInputField();
    }

    private void CaptureConsoleInputLock(TMP_InputField consoleInput)
    {
        if (_hasConsoleReadOnlySnapshot)
        {
            return;
        }

        _consoleReadOnlySnapshot = consoleInput.readOnly;
        _consoleInteractableSnapshot = consoleInput.interactable;
        _hasConsoleReadOnlySnapshot = true;
    }

    private void RestoreConsoleInputLock()
    {
        if (!_hasConsoleReadOnlySnapshot)
        {
            return;
        }

        TMP_InputField? consoleInput = GetConsoleInputField();
        if (consoleInput != null)
        {
            consoleInput.readOnly = _consoleReadOnlySnapshot;
            consoleInput.interactable = _consoleInteractableSnapshot;
        }

        _hasConsoleReadOnlySnapshot = false;
        _consoleReadOnlySnapshot = false;
        _consoleInteractableSnapshot = false;
    }

    private static bool IsPointerInsideInput(TMP_InputField input)
    {
        if (input.transform is not RectTransform inputRect)
        {
            return false;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(inputRect, GetMousePosition(), null);
    }

    private static bool IsPrimaryMouseDown()
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

    private static bool IsPrimaryMouseHeld()
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

    private static Vector3 GetMousePosition()
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

    private static TMP_InputField? GetConsoleInputField()
    {
        if (global::Console.instance?.m_input == null)
        {
            return null;
        }

        try
        {
            return (TMP_InputField)(object)global::Console.instance.m_input;
        }
        catch
        {
            return null;
        }
    }
}
