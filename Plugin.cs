using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using ConsolePanel;
using HarmonyLib;
using UnityEngine;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AdminQoL;

internal static class AdminQoLConfigSections
{
    internal const string General = "1 - General";
    internal const string GameplayQoL = "2 - Gameplay QoL";
}

[BepInPlugin(ModGUID, ModName, ModVersion)]
public class AdminQoLPlugin : BaseUnityPlugin
{
    internal const string ModName = "AdminQoL";
    internal const string ModVersion = "1.0.3";
    internal const string Author = "sighsorry";
    internal const string ModGUID = $"{Author}.{ModName}";

    private static readonly Harmony Harmony = new(ModGUID);
    internal static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource(ModName);

    private static ConfigEntry<bool> _requireServerAdmin = null!;
    private static ConfigEntry<bool> _suppressUnlockNotifications = null!;
    private static ConfigEntry<bool> _logSuppressedUnlocks = null!;
    private static ConfigEntry<bool> _removeSkillLevelUpEffects = null!;
    private static ConfigEntry<bool> _removeSkillLevelUpAlarm = null!;
    private static ConfigEntry<bool> _removeEquipDelay = null!;
    private static ConfigEntry<bool> _loadYamlItemSets = null!;

    private static FileSystemWatcher? _configWatcher;
    private static FileSystemWatcher? _itemSetWatcher;
    private static readonly object ReloadLock = new();
    private static DateTime _lastConfigReloadTime;
    private static DateTime _lastItemSetReloadTime;

    private static readonly TimeSpan ReloadDebounce = TimeSpan.FromSeconds(1);
    private const string AdminProbePrefix = "adminqol_admintest_";
    private const float AdminProbeRetrySeconds = 5f;
    private const float AdminProbeTimeoutSeconds = 3f;
    private static ZNet? _adminProbeZNet;
    private static long _adminProbePlayerId;
    private static string _adminProbeToken = "";
    private static bool _adminProbePending;
    private static bool? _adminProbeVerified;
    private static float _adminProbeNextTime;
    private static float _adminProbeDeadline;

    public void Awake()
    {
        bool saveOnSet = Config.SaveOnConfigSet;
        Config.SaveOnConfigSet = false;

        _requireServerAdmin = Config.Bind(AdminQoLConfigSections.General, "Require Server Admin", true, "Require the local player to be host or listed in the server admin list before AdminQoL commands run.");
        _loadYamlItemSets = Config.Bind(AdminQoLConfigSections.General, "Load YAML Item Sets", true, "Load AdminQoL.ItemSets.yml and inject those sets into Valheim's vanilla itemset command.");
        _suppressUnlockNotifications = Config.Bind(AdminQoLConfigSections.GameplayQoL, "Suppress Unlock Notifications", true, "Suppress the top-left unlock popup queue used for new recipes, items, pieces, stations, materials, and trophies.");
        _logSuppressedUnlocks = Config.Bind(AdminQoLConfigSections.GameplayQoL, "Log Suppressed Unlocks", true, "When unlock popups are suppressed, keep the unlock text in the MessageHud log.");
        _removeSkillLevelUpEffects = Config.Bind(AdminQoLConfigSections.GameplayQoL, "Remove Skill Level Up Effects", true, "Remove the VFX/SFX played when a skill gains a level.");
        _removeSkillLevelUpAlarm = Config.Bind(AdminQoLConfigSections.GameplayQoL, "Remove Skill Level Up Alarm", true, "Remove the top-left or center message when a skill gains a level.");
        _removeEquipDelay = Config.Bind(AdminQoLConfigSections.GameplayQoL, "Remove Equip Delay", true, "Equip and unequip items immediately instead of using Valheim's timed equip action.");
        ConsolePanelModule.Initialize(gameObject, Config);

        CustomItemSetManager.Initialize(Paths.ConfigPath);

        Harmony.PatchAll(Assembly.GetExecutingAssembly());
        SetupConfigWatcher();
        SetupItemSetWatcher();

        Config.Save();
        Config.SaveOnConfigSet = saveOnSet;
    }

    private void OnDestroy()
    {
        ConsolePanelModule.Shutdown();
        Config.Save();
        _configWatcher?.Dispose();
        _itemSetWatcher?.Dispose();
        Harmony.UnpatchSelf();
    }

    private void Update()
    {
        if (_requireServerAdmin.Value)
        {
            PrimeServerAdminProbe();
        }
    }

    private void SetupConfigWatcher()
    {
        string configFileName = $"{ModGUID}.cfg";
        _configWatcher = new FileSystemWatcher(Paths.ConfigPath, configFileName)
        {
            IncludeSubdirectories = false,
            SynchronizingObject = ThreadingHelper.SynchronizingObject,
            EnableRaisingEvents = true
        };
        _configWatcher.Changed += ReloadConfigAfterFileChange;
        _configWatcher.Created += ReloadConfigAfterFileChange;
        _configWatcher.Renamed += ReloadConfigAfterFileChange;
    }

    private void SetupItemSetWatcher()
    {
        _itemSetWatcher = new FileSystemWatcher(Paths.ConfigPath, CustomItemSetManager.FileWatcherFilter)
        {
            IncludeSubdirectories = false,
            SynchronizingObject = ThreadingHelper.SynchronizingObject,
            EnableRaisingEvents = true
        };
        _itemSetWatcher.Changed += ReloadItemSetsAfterFileChange;
        _itemSetWatcher.Created += ReloadItemSetsAfterFileChange;
        _itemSetWatcher.Renamed += ReloadItemSetsAfterFileChange;
    }

    private void ReloadConfigAfterFileChange(object sender, FileSystemEventArgs e)
    {
        DateTime now = DateTime.Now;
        if (now - _lastConfigReloadTime < ReloadDebounce)
        {
            return;
        }

        lock (ReloadLock)
        {
            try
            {
                Config.Reload();
                Config.Save();
                Log.LogInfo("Configuration reloaded.");
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to reload configuration: {ex.Message}");
            }
        }

        _lastConfigReloadTime = now;
    }

    private void ReloadItemSetsAfterFileChange(object sender, FileSystemEventArgs e)
    {
        DateTime now = DateTime.Now;
        if (now - _lastItemSetReloadTime < ReloadDebounce)
        {
            return;
        }

        CustomItemSetManager.Reload();
        _lastItemSetReloadTime = now;
    }

    internal static bool ShouldSuppressUnlockNotifications()
    {
        return _suppressUnlockNotifications.Value;
    }

    internal static bool ShouldLogSuppressedUnlocks()
    {
        return _logSuppressedUnlocks.Value;
    }

    internal static bool ShouldLoadYamlItemSets()
    {
        return _loadYamlItemSets.Value;
    }

    internal static bool ShouldRemoveSkillLevelUpEffects()
    {
        return _removeSkillLevelUpEffects.Value;
    }

    internal static bool ShouldRemoveSkillLevelUpAlarm()
    {
        return _removeSkillLevelUpAlarm.Value;
    }

    internal static bool ShouldRemoveEquipDelay()
    {
        return _removeEquipDelay.Value;
    }

    internal static bool HasAdminAccess()
    {
        if (!_requireServerAdmin.Value)
        {
            return true;
        }

        if (ZNet.instance == null)
        {
            return true;
        }

        if (ZNet.instance.IsServer() || ZNet.instance.LocalPlayerIsAdminOrHost())
        {
            MarkServerAdminProbeSuccess(ZNet.instance);
            return true;
        }

        UpdateServerAdminProbeState();
        if (_adminProbeVerified == true)
        {
            return true;
        }

        StartServerAdminProbe(force: false);
        return false;
    }

    internal static string GetAdminAccessDeniedMessage()
    {
        if (_adminProbePending)
        {
            return "AdminQoL is verifying server admin access. Try the command again in a moment.";
        }

        return "You must be host or a server admin to use this AdminQoL command.";
    }

    internal static object RequireAdmin(Terminal.ConsoleEventArgs args, Func<Terminal.ConsoleEventArgs, object> action)
    {
        try
        {
            if (!HasAdminAccess())
            {
                return GetAdminAccessDeniedMessage();
            }

            return action(args);
        }
        catch (Exception ex)
        {
            Log.LogError(ex);
            return ex.Message;
        }
    }

    internal static bool HandleServerAdminProbeRemotePrint(string text)
    {
        if (!_adminProbePending)
        {
            return false;
        }

        if (string.Equals(text, $"Unbanning user {_adminProbeToken}", StringComparison.Ordinal))
        {
            MarkServerAdminProbeSuccess(ZNet.instance);
            return true;
        }

        if (string.Equals(text, "You are not admin", StringComparison.Ordinal))
        {
            _adminProbePending = false;
            _adminProbeVerified = false;
            _adminProbeNextTime = Time.realtimeSinceStartup + AdminProbeRetrySeconds;
            return true;
        }

        return false;
    }

    private static void PrimeServerAdminProbe()
    {
        if (ZNet.instance == null || ZNet.instance.IsServer())
        {
            ResetServerAdminProbe();
            return;
        }

        UpdateServerAdminProbeState();
        if (_adminProbeVerified == null)
        {
            StartServerAdminProbe(force: false);
        }
    }

    private static void UpdateServerAdminProbeState()
    {
        ZNet? znet = ZNet.instance;
        long playerId = GetLocalPlayerId();
        if (!ReferenceEquals(_adminProbeZNet, znet) || _adminProbePlayerId != playerId)
        {
            ResetServerAdminProbe();
            _adminProbeZNet = znet;
            _adminProbePlayerId = playerId;
            _adminProbeToken = playerId > 0 ? AdminProbePrefix + playerId.ToString(CultureInfo.InvariantCulture) : "";
        }

        if (_adminProbePending && Time.realtimeSinceStartup > _adminProbeDeadline)
        {
            _adminProbePending = false;
            _adminProbeVerified = false;
            _adminProbeNextTime = Time.realtimeSinceStartup + AdminProbeRetrySeconds;
        }
    }

    private static void StartServerAdminProbe(bool force)
    {
        ZNet? znet = ZNet.instance;
        if (znet == null || znet.IsServer())
        {
            return;
        }

        UpdateServerAdminProbeState();
        if (_adminProbePending || string.IsNullOrWhiteSpace(_adminProbeToken))
        {
            return;
        }

        float now = Time.realtimeSinceStartup;
        if (!force && now < _adminProbeNextTime)
        {
            return;
        }

        try
        {
            _adminProbePending = true;
            _adminProbeDeadline = now + AdminProbeTimeoutSeconds;
            _adminProbeNextTime = now + AdminProbeRetrySeconds;
            znet.Unban(_adminProbeToken);
        }
        catch (Exception ex)
        {
            _adminProbePending = false;
            _adminProbeVerified = false;
            _adminProbeNextTime = Time.realtimeSinceStartup + AdminProbeRetrySeconds;
            Log.LogDebug($"Admin probe failed: {ex.Message}");
        }
    }

    private static void MarkServerAdminProbeSuccess(ZNet? znet)
    {
        _adminProbeZNet = znet;
        _adminProbePlayerId = GetLocalPlayerId();
        _adminProbePending = false;
        _adminProbeVerified = true;
        _adminProbeNextTime = Time.realtimeSinceStartup + AdminProbeRetrySeconds;
    }

    private static void ResetServerAdminProbe()
    {
        _adminProbeZNet = null;
        _adminProbePlayerId = 0;
        _adminProbeToken = "";
        _adminProbePending = false;
        _adminProbeVerified = null;
        _adminProbeNextTime = 0f;
        _adminProbeDeadline = 0f;
    }

    private static long GetLocalPlayerId()
    {
        long playerId = Game.instance?.GetPlayerProfile()?.GetPlayerID() ?? 0L;
        if (playerId != 0L)
        {
            return playerId;
        }

        Player localPlayer = Player.m_localPlayer;
        return localPlayer != null ? localPlayer.GetPlayerID() : 0L;
    }
}

[HarmonyPatch(typeof(ZNet), "RPC_RemotePrint")]
internal static class ServerAdminProbeRemotePrintPatch
{
    private static bool Prefix(string text)
    {
        return !AdminQoLPlugin.HandleServerAdminProbeRemotePrint(text);
    }
}

[HarmonyPatch(typeof(MessageHud), nameof(MessageHud.QueueUnlockMsg))]
internal static class SuppressUnlockMessagePatch
{
    private static bool Prefix(MessageHud __instance, string topic, string description)
    {
        if (!AdminQoLPlugin.ShouldSuppressUnlockNotifications())
        {
            return true;
        }

        if (AdminQoLPlugin.ShouldLogSuppressedUnlocks())
        {
            __instance.AddLog($"{topic}: {description}");
        }

        return false;
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.OnSkillLevelup))]
internal static class SkillLevelUpEffectsPatch
{
    private static bool Prefix()
    {
        return !AdminQoLPlugin.ShouldRemoveSkillLevelUpEffects();
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.Message))]
internal static class SkillLevelUpAlarmPatch
{
    private static bool Prefix(string msg)
    {
        return !AdminQoLPlugin.ShouldRemoveSkillLevelUpAlarm() || !IsSkillLevelUpMessage(msg);
    }

    private static bool IsSkillLevelUpMessage(string? msg)
    {
        string message = msg ?? "";
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.TrimStart().StartsWith("$msg_skillup ", StringComparison.OrdinalIgnoreCase);
    }
}

[HarmonyPatch(typeof(Player), "QueueEquipAction")]
internal static class InstantEquipActionPatch
{
    private static readonly MethodInfo? CancelReloadAction = AccessTools.Method(typeof(Player), "CancelReloadAction");

    private static bool Prefix(Player __instance, ItemDrop.ItemData item)
    {
        if (!AdminQoLPlugin.ShouldRemoveEquipDelay())
        {
            return true;
        }

        if (item == null)
        {
            return false;
        }

        if (__instance.IsEquipActionQueued(item))
        {
            __instance.RemoveEquipAction(item);
            return false;
        }

        CancelReloadAction?.Invoke(__instance, null);
        __instance.EquipItem(item);
        return false;
    }
}

[HarmonyPatch(typeof(Player), "QueueUnequipAction")]
internal static class InstantUnequipActionPatch
{
    private static readonly MethodInfo? CancelReloadAction = AccessTools.Method(typeof(Player), "CancelReloadAction");

    private static bool Prefix(Player __instance, ItemDrop.ItemData item)
    {
        if (!AdminQoLPlugin.ShouldRemoveEquipDelay())
        {
            return true;
        }

        if (item == null)
        {
            return false;
        }

        if (__instance.IsEquipActionQueued(item))
        {
            __instance.RemoveEquipAction(item);
            return false;
        }

        CancelReloadAction?.Invoke(__instance, null);
        __instance.UnequipItem(item);
        return false;
    }
}

[HarmonyPatch(typeof(ItemSets), nameof(ItemSets.Awake))]
internal static class ItemSetsAwakePatch
{
    private static void Postfix(ItemSets __instance)
    {
        CustomItemSetManager.ApplyYamlSetsToItemSets(__instance);
    }
}

[HarmonyPatch(typeof(ObjectDB), "Awake")]
internal static class ObjectDBAwakePatch
{
    private static void Postfix()
    {
        KnowledgeTargetIndex.Invalidate();
        CustomItemSetManager.ApplyYamlSetsToItemSets(ItemSets.instance);
    }
}

[HarmonyPatch(typeof(ItemSets), nameof(ItemSets.TryGetSet))]
internal static class ItemSetsTryGetSetPatch
{
    private static void Prefix(string name, ref ActiveItemSetApplication? __state)
    {
        __state = CustomItemSetManager.BeginItemSetApplication(name);
    }

    private static Exception? Finalizer(Exception? __exception, ActiveItemSetApplication? __state)
    {
        CustomItemSetManager.EndItemSetApplication(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), new[]
{
    typeof(string),
    typeof(int),
    typeof(int),
    typeof(int),
    typeof(long),
    typeof(string),
    typeof(Vector2i),
    typeof(bool)
})]
internal static class InventoryAddItemFromNamePatch
{
    private static void Postfix(string name, ItemDrop.ItemData __result)
    {
        if (__result != null)
        {
            CustomItemSetManager.ApplyItemModifiers(name, __result);
        }
    }
}
