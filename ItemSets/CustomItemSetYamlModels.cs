using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AdminQoL;
internal static partial class CustomItemSetManager
{
    private sealed class YamlItemSetFile
    {
        public List<YamlItemSet> ItemSets { get; set; } = new();
    }

    private sealed class YamlItemSet
    {
        public bool? Enabled { get; set; }
        public string Name { get; set; } = "";
        public bool? ReplaceExisting { get; set; }
        public List<YamlSetItem> Items { get; set; } = new();
        public List<YamlSetSkill> Skills { get; set; } = new();
        public List<string> KnownStations { get; set; } = new();
        public List<string> KnownItems { get; set; } = new();
        public List<string> InheritKnownFromItemSet { get; set; } = new();
    }

    private sealed class YamlSetItem
    {
        public string Prefab { get; set; } = "";
        public int Quality { get; set; } = 1;
        public int Stack { get; set; } = 1;
        public bool Use { get; set; } = true;
        public int HotbarSlot { get; set; }
        public YamlJewelcraftingItem? Jewelcrafting { get; set; }
        public YamlEpicLootItem? EpicLoot { get; set; }
    }

    private sealed class YamlSetSkill
    {
        public string Skill { get; set; } = "";
        public int Level { get; set; }
    }
}
