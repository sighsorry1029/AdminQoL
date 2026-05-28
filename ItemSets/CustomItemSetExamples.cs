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
    private static void EnsureYamlFileFromVanillaSets()
    {
        if (string.IsNullOrWhiteSpace(_filePath) || File.Exists(_filePath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        List<ItemSets.ItemSet> vanillaSets = _originalSets ?? new List<ItemSets.ItemSet>();
        YamlItemSetFile file = new()
        {
            ItemSets = vanillaSets
                .Where(set => !string.IsNullOrWhiteSpace(set.m_name))
                .Select(ToYamlItemSet)
                .OrderBy(set => set.Name)
                .ToList()
        };

        string yaml = VanillaYamlHeader + Serializer.Serialize(file);
        File.WriteAllText(_filePath, yaml);
        AdminQoLPlugin.Log.LogInfo($"Created {FileName} with {file.ItemSets.Count} vanilla itemsets.");
    }

    private static YamlItemSet ToYamlItemSet(ItemSets.ItemSet set)
    {
        return new YamlItemSet
        {
            Name = set.m_name,
            ReplaceExisting = true,
            Items = set.m_items
                .Where(item => item?.m_item != null)
                .Select(item => new YamlSetItem
                {
                    Prefab = GetItemPrefabName(item.m_item),
                    Quality = Math.Max(1, item.m_quality),
                    Stack = Math.Max(1, item.m_stack),
                    Use = item.m_use,
                    HotbarSlot = Mathf.Clamp(item.m_hotbarSlot, 0, 8)
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.Prefab))
                .ToList(),
            Skills = set.m_skills
                .Select(skill => new YamlSetSkill
                {
                    Skill = skill.m_skill.ToString(),
                    Level = Mathf.Clamp(skill.m_level, 0, 100)
                })
                .ToList(),
            KnownStations = set.m_knownStations
                .Where(station => station != null)
                .Select(station => station.gameObject.name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList(),
            KnownItems = set.m_knownItems
                .Where(item => item != null)
                .Select(GetItemPrefabName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList(),
            InheritKnownFromItemSet = set.m_inheritKnownFromItemSet
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList()
        };
    }

    private static string GetItemPrefabName(ItemDrop itemDrop)
    {
        return itemDrop.gameObject != null ? itemDrop.gameObject.name : "";
    }

    private static string BuildEpicLootExampleYaml()
    {
        YamlItemSetFile file = new()
        {
            ItemSets = BuildEpicLootExampleSets()
        };

        return EpicLootExampleHeader + Serializer.Serialize(file);
    }

    private static string BuildJewelcraftingExampleYaml()
    {
        YamlItemSetFile file = new()
        {
            ItemSets = BuildJewelcraftingExampleSets()
        };

        return JewelcraftingExampleHeader + Serializer.Serialize(file);
    }

    private static List<YamlItemSet> BuildEpicLootExampleSets()
    {
        List<YamlItemSet> examples = GetVanillaExampleSources()
            .Select((set, setIndex) =>
            {
                YamlItemSet yamlSet = ToYamlItemSet(set);
                yamlSet.Enabled = true;
                yamlSet.Name = "AdminQoL_Example_EpicLoot_" + SafeYamlName(set.m_name, setIndex);
                yamlSet.ReplaceExisting = null;

                int magicIndex = 0;
                foreach (YamlSetItem item in yamlSet.Items)
                {
                    if (!IsLikelyEquipmentPrefab(item.Prefab))
                    {
                        continue;
                    }

                    item.EpicLoot = CreateEpicLootExampleBlock(item.Prefab, setIndex, magicIndex);
                    magicIndex++;
                }

                EnsureAtLeastOneEpicLootBlock(yamlSet, setIndex);
                return yamlSet;
            })
            .ToList();

        if (examples.Count == 0)
        {
            examples.Add(CreateFallbackEpicLootSet());
        }

        return examples;
    }

    private static List<YamlItemSet> BuildJewelcraftingExampleSets()
    {
        List<YamlItemSet> examples = GetVanillaExampleSources()
            .Select((set, setIndex) =>
            {
                YamlItemSet yamlSet = ToYamlItemSet(set);
                yamlSet.Enabled = true;
                yamlSet.Name = "AdminQoL_Example_Jewelcrafting_" + SafeYamlName(set.m_name, setIndex);
                yamlSet.ReplaceExisting = null;

                int socketIndex = 0;
                foreach (YamlSetItem item in yamlSet.Items)
                {
                    if (!IsLikelyEquipmentPrefab(item.Prefab))
                    {
                        continue;
                    }

                    item.Jewelcrafting = CreateJewelcraftingExampleBlock(socketIndex);
                    socketIndex++;
                }

                EnsureAtLeastOneJewelcraftingBlock(yamlSet);
                return yamlSet;
            })
            .ToList();

        if (examples.Count == 0)
        {
            examples.Add(CreateFallbackJewelcraftingSet());
        }

        return examples;
    }

    private static IEnumerable<ItemSets.ItemSet> GetVanillaExampleSources()
    {
        return (_originalSets ?? new List<ItemSets.ItemSet>())
            .Where(set => set != null && !string.IsNullOrWhiteSpace(set.m_name) && set.m_items.Any(item => item?.m_item != null))
            .OrderBy(set => ExampleSetSortWeight(set.m_name))
            .ThenBy(set => set.m_name, StringComparer.OrdinalIgnoreCase);
    }

    private static int ExampleSetSortWeight(string name)
    {
        string normalized = name.ToLowerInvariant();
        if (normalized.Contains("start"))
        {
            return 0;
        }

        if (normalized.Contains("meadow"))
        {
            return 1;
        }

        if (normalized.Contains("black") || normalized.Contains("forest"))
        {
            return 2;
        }

        if (normalized.Contains("swamp"))
        {
            return 3;
        }

        if (normalized.Contains("mountain"))
        {
            return 4;
        }

        if (normalized.Contains("plain"))
        {
            return 5;
        }

        if (normalized.Contains("mist"))
        {
            return 6;
        }

        if (normalized.Contains("ash"))
        {
            return 7;
        }

        return 100;
    }

    private static string SafeYamlName(string name, int fallbackIndex)
    {
        string safe = new(name.Where(char.IsLetterOrDigit).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? $"Set{fallbackIndex + 1}" : safe;
    }

    private static bool IsLikelyEquipmentPrefab(string prefab)
    {
        if (string.IsNullOrWhiteSpace(prefab))
        {
            return false;
        }

        string name = prefab.ToLowerInvariant();
        string[] equipmentTokens =
        {
            "armor", "helmet", "helm", "cape", "shield", "buckler", "sword", "knife", "axe", "mace",
            "club", "spear", "atgeir", "bow", "crossbow", "staff", "sledge", "pickaxe", "torch"
        };

        return equipmentTokens.Any(token => name.Contains(token));
    }

    private static YamlEpicLootItem CreateEpicLootExampleBlock(string prefab, int setIndex, int itemIndex)
    {
        return ((setIndex + itemIndex) % 3) switch
        {
            0 => new YamlEpicLootItem
            {
                Rarity = "Epic",
                DisplayName = $"AdminQoL Example {prefab}",
                EffectCount = 3,
                Effects = new List<YamlEpicLootEffect>
                {
                    new() { Type = "AddFireDamage", Value = 12f },
                    new() { Type = "random", Value = "random" }
                },
                AugmentedEffectIndices = new List<int> { 0 }
            },
            1 => new YamlEpicLootItem
            {
                RarityWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Rare"] = 50f,
                    ["Epic"] = 35f,
                    ["Legendary"] = 15f
                },
                EffectCount = 2,
                Effects = new List<YamlEpicLootEffect>
                {
                    new() { Type = "random", Value = "random" }
                }
            },
            _ => new YamlEpicLootItem
            {
                Rarity = "random",
                TypeNameOverride = "example gear",
                Unidentified = false,
                EffectCount = 2,
                Effects = new List<YamlEpicLootEffect>
                {
                    new() { Type = "AddFireDamage", Value = "random" },
                    new() { Type = "random", Value = "random" }
                }
            }
        };
    }

    private static YamlJewelcraftingItem CreateJewelcraftingExampleBlock(int itemIndex)
    {
        return (itemIndex % 3) switch
        {
            0 => new YamlJewelcraftingItem
            {
                SocketsLocked = true,
                SocketSlotsLocked = true,
                Sockets = new List<string>
                {
                    "Simple_Red_Socket",
                    "Common_Merged_Gemstone_Black_Blue",
                    "random_perfect"
                }
            },
            1 => new YamlJewelcraftingItem
            {
                SocketsLocked = false,
                SocketSlotsLocked = true,
                Sockets = new List<string>
                {
                    "random_simple",
                    "Advanced_Merged_Gemstone_Black_Red",
                    "empty"
                }
            },
            _ => new YamlJewelcraftingItem
            {
                SocketsLocked = true,
                SocketSlotsLocked = false,
                Sockets = new List<string>
                {
                    "random_perfect",
                    "random_advanced",
                    "Perfect_Merged_Gemstone_Black_Yellow"
                }
            }
        };
    }

    private static void EnsureAtLeastOneEpicLootBlock(YamlItemSet yamlSet, int setIndex)
    {
        if (yamlSet.Items.Any(item => item.EpicLoot != null))
        {
            return;
        }

        YamlSetItem? firstItem = yamlSet.Items.FirstOrDefault();
        if (firstItem != null)
        {
            firstItem.EpicLoot = CreateEpicLootExampleBlock(firstItem.Prefab, setIndex, 0);
        }
    }

    private static void EnsureAtLeastOneJewelcraftingBlock(YamlItemSet yamlSet)
    {
        if (yamlSet.Items.Any(item => item.Jewelcrafting != null))
        {
            return;
        }

        YamlSetItem? firstItem = yamlSet.Items.FirstOrDefault();
        if (firstItem != null)
        {
            firstItem.Jewelcrafting = CreateJewelcraftingExampleBlock(0);
        }
    }

    private static YamlItemSet CreateFallbackEpicLootSet()
    {
        return new YamlItemSet
        {
            Enabled = true,
            Name = "AdminQoL_Example_EpicLoot_Fallback",
            Items = new List<YamlSetItem>
            {
                new()
                {
                    Prefab = "SwordIron",
                    Quality = 4,
                    Stack = 1,
                    Use = true,
                    HotbarSlot = 1,
                    EpicLoot = CreateEpicLootExampleBlock("SwordIron", 0, 0)
                },
                new()
                {
                    Prefab = "ShieldIronBuckler",
                    Quality = 4,
                    Stack = 1,
                    Use = true,
                    HotbarSlot = 2,
                    EpicLoot = CreateEpicLootExampleBlock("ShieldIronBuckler", 0, 1)
                }
            }
        };
    }

    private static YamlItemSet CreateFallbackJewelcraftingSet()
    {
        return new YamlItemSet
        {
            Enabled = true,
            Name = "AdminQoL_Example_Jewelcrafting_Fallback",
            Items = new List<YamlSetItem>
            {
                new()
                {
                    Prefab = "SwordSilver",
                    Quality = 4,
                    Stack = 1,
                    Use = true,
                    HotbarSlot = 1,
                    Jewelcrafting = CreateJewelcraftingExampleBlock(0)
                }
            }
        };
    }

    private const string VanillaYamlHeader = """
# AdminQoL itemsets.
# This file is generated once from Valheim's built-in itemsets and is never overwritten by AdminQoL.
# Edit existing entries to override vanilla sets, remove entries you do not care about, or add new sets.
# These entries are injected into Valheim's vanilla "itemset" command.
# Use prefab names for items, for example SwordIron, Hammer, ArmorIronChest, Wood.
# AdminQoL also loads AdminQoL.ItemSets*.yml files in this config folder.
# EpicLoot and Jewelcrafting examples are generated separately as AdminQoL.ItemSets.EpicLoot.yml
# and AdminQoL.ItemSets.Jewelcrafting.yml so this vanilla file stays small.
# hotbarSlot uses 1-8 for hotbar keys. Use 0 or omit it to skip hotbar placement.
# replaceExisting defaults to false when omitted. Use true only when replacing a set with the same name.

""";

    private const string EpicLootExampleHeader = """
# AdminQoL EpicLoot itemset reference.
# This file is generated once and is never overwritten by AdminQoL.
# AdminQoL reads this file because it matches AdminQoL.ItemSets*.yml.
# Entries are enabled by default, but this file is loaded only when EpicLoot is installed.
# Set enabled: false on any entry you do not want in the itemset command.
#
# EpicLoot set bonuses are not defined here. Define custom Legendary/Mythic items and set bonuses
# in EpicLoot's legendaries.json, then reference their legendaryId and optional setId here.
#
# Top-level itemset fields:
#   enabled: false disables the entry. Missing enabled means enabled.
#   name: itemset command name.
#   replaceExisting: true replaces an existing vanilla/YAML set with the same name. Missing means false.
#
# Item fields:
#   prefab: Valheim item prefab name, for example SwordIron.
#   quality: item quality. Defaults to 1.
#   stack: item stack size. Defaults to 1.
#   use: true equips/uses the item through the vanilla itemset command. Defaults to true.
#   hotbarSlot: 1-8 moves the item to that hotbar slot. 0 or omitted means no hotbar move.
#
# epicLoot fields:
#   rarity:
#     Allowed values: Magic, Rare, Epic, Legendary, Mythic, random.
#     Omit rarity to use Magic, unless legendaryId lets AdminQoL infer Legendary/Mythic.
#     random chooses one EpicLoot.ItemRarity enum value at runtime.
#   rarityWeights:
#     YAML map from rarity name to positive number.
#     Allowed keys: Magic, Rare, Epic, Legendary, Mythic.
#     If at least one valid positive entry exists, this overrides rarity.
#     Example: { Rare: 50, Epic: 35, Legendary: 15 }
#   legendaryId:
#     String ID from EpicLoot legendaries.json.
#     Use the key/ID of a loaded LegendaryItems or MythicItems entry.
#     Values are server/profile dependent and cannot be listed by AdminQoL.
#     Optional. Omit for normal non-unique magic items.
#   setId:
#     String ID from EpicLoot legendaries.json.
#     Use the key/ID of a loaded LegendarySets or MythicSets entry.
#     Values are server/profile dependent and cannot be listed by AdminQoL.
#     Optional when legendaryId belongs to a loaded EpicLoot set.
#   displayName:
#     Any string for MagicItem.DisplayName.
#     Optional. Omit with legendaryId to use EpicLoot's legendary display name.
#   typeNameOverride:
#     Any string for MagicItem.TypeNameOverride.
#     Optional. Changes the item type/name wording EpicLoot displays.
#   unidentified:
#     Allowed values: true, false.
#     Optional. Maps to MagicItem.IsUnidentified.
#   effectCount:
#     Integer 0-32.
#     Target effect count. Extra slots are filled with random available EpicLoot effects.
#     If effects and effectCount are both omitted, AdminQoL uses EpicLoot's own LootRoller.
#   effects:
#     List of effect entries.
#     type: exact EpicLoot effect Type ID, or random.
#       random uses EpicLoot.MagicItemEffectDefinitions.GetAvailableEffects(...) for the item.
#       Exact IDs are case-insensitive in AdminQoL lookup, but writing the canonical ID is recommended.
#       Some exact IDs are item-type or rarity restricted by EpicLoot requirements.
#     value: number, random, or omitted.
#       number sets the exact effect value.
#       random or omitted uses EpicLoot's rarity-based value roll.
#       Some EpicLoot effects are valueless; for those, EpicLoot treats the value as display/logic neutral.
#   augmentedEffectIndices:
#     List of zero-based effect indexes, for example [0, 2].
#     Indexes refer to the final effects list order after configured effects and random fill.
#
# Current EpicLoot effect Type IDs commonly available in EpicLoot.MagicEffectType:
#   Utility / unique-style:
#     DvergerCirclet, InstantMead, AutoMead, DecreaseMeadCooldown, Megingjord,
#     Wishbone, Andvaranaut, Indestructible, CoinHoarder, Weightless, Waterproof,
#     Warmth, Glowing, FreeBuild, Comfortable, Luck, Apportation
#   Damage adders:
#     AddBluntDamage, AddSlashingDamage, AddPiercingDamage, AddFireDamage,
#     AddFrostDamage, AddLightningDamage, AddPoisonDamage, AddSpiritDamage
#   Damage modifiers:
#     ModifyDamage, ModifyPhysicalDamage, ModifyElementalDamage, ModifyBackstab,
#     ModifyStaggerDamage, ModifySummonDamage
#   Resistance adders:
#     AddFireResistancePercentage, AddFrostResistancePercentage,
#     AddLightningResistancePercentage, AddPoisonResistancePercentage,
#     AddSpiritResistancePercentage, AddElementalResistancePercentage,
#     AddBluntResistancePercentage, AddSlashingResistancePercentage,
#     AddPiercingResistancePercentage, AddChoppingResistancePercentage,
#     AddPhysicalResistancePercentage
#   Armor / block / parry:
#     ModifyArmor, ModifyBlockPower, ModifyBlockForce, ModifyParry,
#     ModifyBlockStaminaUse, ModifyParryWindow, Bulwark
#   Attack and weapon handling:
#     ModifyAttackSpeed, ModifyAttackStaminaUse, ModifyAttackEitrUse,
#     ModifyAttackHealthUse, ModifyDrawStaminaUse, ModifyProjectileSpeed,
#     ModifyMagicFireRate, ModifyFireRate, AmmoConservation, QuickDraw,
#     Throwable, RecallWeapon, TripleBowShot, DoubleMagicShot, OffSetAttack
#   Movement / stamina / regen:
#     ModifyMovementSpeed, RemoveSpeedPenalty, ModifySprintStaminaUse,
#     ModifyDodgeStaminaUse, ModifyJumpStaminaUse, ModifyRunStaminaDrain,
#     ModifyStaminaRegen, ModifyHealthRegen, ModifyEitrRegen,
#     AddHealthRegen, IncreaseHealth, IncreaseStamina, IncreaseEitr,
#     AddCarryWeight, ReduceWeight, FeatherFall, DoubleJump
#   Environment / gathering / exploration:
#     IncreaseMiningDrop, IncreaseTreeDrop, IncreaseHeatResistance,
#     ModifyBuildDistance, ModifyPickupRange, ModifyNoise, ModifyDiscoveryRadius,
#     ModifyWispRange
#   Life/eitr steal and combat procs:
#     LifeSteal, EitrLeech, Bloodlust, Paralyze, ExplosiveArrows, Executioner,
#     Riches, Opportunist, Duelist, Immovable, Slow, FrostDamageAOE,
#     ChainLightning, ReflectDamage, AvoidDamageTaken, StaggerOnDamageTaken,
#     DodgeBuff, Undying
#   Skill modifiers:
#     AddSwordsSkill, AddKnivesSkill, AddClubsSkill, AddPolearmsSkill,
#     AddSpearsSkill, AddBlockingSkill, AddAxesSkill, AddBowsSkill,
#     AddCrossbowsSkill, AddUnarmedSkill, AddPickaxesSkill, AddFishingSkill,
#     AddElementalMagicSkill, AddBloodMagicSkill, AddMovementSkills,
#     AddCrafterSkills, QuickLearner
#   Low-health conditional variants:
#     ModifyLowHealth, ModifyMovementSpeedLowHealth, ModifyHealthRegenLowHealth,
#     ModifyStaminaRegenLowHealth, ModifyEitrRegenLowHealth, ModifyArmorLowHealth,
#     ModifyDamageLowHealth, ModifyBlockPowerLowHealth, ModifyParryLowHealth,
#     ModifyAttackSpeedLowHealth, AvoidDamageTakenLowHealth, LifeStealLowHealth
#   Magic/eitr themed:
#     SpellSword, BulkUp, EitrWeave, DartingThoughts, HeadHunter
#
# If your installed/server-synchronized EpicLoot config adds custom effect definitions,
# effects[].type can also use those loaded custom Type IDs.
#
# Generated examples below are copied from Valheim's built-in itemsets, renamed with
# AdminQoL_Example_EpicLoot_*, and then annotated with epicLoot blocks. They mix:
#   - fixed rarity
#   - weighted random rarity
#   - random rarity
#   - fixed effect ID with fixed value
#   - fixed effect ID with random value
#   - fully random effect

""";

    private const string JewelcraftingExampleHeader = """
# AdminQoL Jewelcrafting itemset reference.
# This file is generated once and is never overwritten by AdminQoL.
# AdminQoL reads this file because it matches AdminQoL.ItemSets*.yml.
# Entries are enabled by default, but this file is loaded only when Jewelcrafting is installed.
# Set enabled: false on any entry you do not want in the itemset command.
#
# jewelcrafting fields:
#   socketsLocked:
#     Allowed values: true, false.
#     Optional. Omit to leave Jewelcrafting's default behavior unchanged.
#     true adds Jewelcrafting's SocketsLock custom data flag.
#     Meaning: lock the socketed gems themselves. Existing gems cannot be removed
#     or replaced through normal Jewelcrafting unsocket/socket interactions.
#     This does not mean hotbar slots; it refers to Jewelcrafting gem sockets.
#   socketSlotsLocked:
#     Allowed values: true, false.
#     Optional. Omit to leave Jewelcrafting's default behavior unchanged.
#     true adds Jewelcrafting's SocketSlotsLock custom data flag.
#     Meaning: lock the number/shape of Jewelcrafting socket slots. Socket frame
#     style items and other slot-count changes are blocked by Jewelcrafting.
#     This does not mean hotbar slots; it refers to Jewelcrafting gem socket slots.
#   sockets:
#     List of socket entries. Values can be:
#     empty
#     exact Jewelcrafting gem prefab name
#       regular examples: Simple_Red_Socket, Advanced_Blue_Socket, Perfect_Green_Socket
#       merged examples: Common_Merged_Gemstone_Black_Blue,
#         Advanced_Merged_Gemstone_Black_Red,
#         Perfect_Merged_Gemstone_Black_Yellow
#     random_simple
#     random_advanced
#     random_perfect
#   Common lock combinations:
#     socketsLocked: true + socketSlotsLocked: true
#       Fixed gem loadout and fixed socket slot count.
#     socketsLocked: true + socketSlotsLocked: false
#       Gems are locked, but slot-count changes are not explicitly locked by AdminQoL.
#     socketsLocked: false + socketSlotsLocked: true
#       Socket slot count is locked, but gems may be changed if Jewelcrafting allows it.
#     Omitted/false for both:
#       AdminQoL applies sockets without adding either Jewelcrafting lock flag.
#
# Generated examples below are copied from Valheim's built-in itemsets, renamed with
# AdminQoL_Example_Jewelcrafting_*, and then annotated with jewelcrafting blocks. They mix:
#   - exact gem prefab names such as Simple_Red_Socket
#   - exact merged gem prefab names such as Common_Merged_Gemstone_Black_Blue
#   - random_simple
#   - random_advanced
#   - random_perfect
#   - empty sockets

""";
}
