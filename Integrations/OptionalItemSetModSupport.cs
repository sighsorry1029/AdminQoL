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
internal static class OptionalItemSetModSupport
{
    internal static void Apply(string setName, ItemSetItemModifier modifier, ItemDrop.ItemData itemData)
    {
        if (modifier.Jewelcrafting != null)
        {
            JewelcraftingItemSetIntegration.Apply(setName, modifier.PrefabName, itemData, modifier.Jewelcrafting);
        }

        if (modifier.EpicLoot != null)
        {
            EpicLootItemSetIntegration.Apply(setName, modifier.PrefabName, itemData, modifier.EpicLoot);
        }
    }
}
