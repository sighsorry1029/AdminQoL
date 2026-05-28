# AdminQoL
Console panel GUI with favorites, Expands vanilla itemsets command via YAML with EpicLoot and Jewelcrafting examples.  Also adds learn/unlearn and clear-inventory commands, suppresses recipe unlock spam, removes equip delay and skill-up effects.

![](https://i.ibb.co/twDb2JjL/Screenshot-2026-05-21-210950.png) <br>
Search for all the console commands that vanilla and other mods adds. Also you can favorite the commands in groups. <br>

![](https://i.ibb.co/vvcD8Qkk/Screenshot-2026-05-21-211013.png) <br>
There is also wooden theme for console panel. You can turn off console panel with f6 by default.

![](https://i.ibb.co/hRqG7c73/seticon.png) <br>
In vanilla, there is console command `itemsets`. Make your own sets for testing. You can edit vanilla sets or make your own sets. Set supports both Epicloot and Jewelcrafting. <br>

![](https://i.ibb.co/WWt5wQXT/Video-Project-11.gif) <br>
Example of calling in built-in sets. Use command `adminqol_clearinventory` to clear your inventory if you need it. <br>

## Features

- Suppresses Valheim unlock popup spam from `MessageHud.QueueUnlockMsg`.
- Can hide skill level-up VFX/SFX and skill level-up alarm messages independently.
- Optional instant equip/unequip by removing Valheim's timed equip action.
- Adds admin-only local commands:
## Features

- Suppresses Valheim unlock popup spam from `MessageHud.QueueUnlockMsg`.
- Can hide skill level-up VFX/SFX and skill level-up alarm messages independently.
- Optional instant equip/unequip by removing Valheim's timed equip action.
- Adds admin-only local commands:
  - 'itemsets' <itemsetname> itemsetname is defined at `AdminQoL.ItemSets.yml`, `AdminQoL.ItemSets.EpicLoot.yml` and `AdminQoL.ItemSets.Jewelcrafting.yml`
  - `adminqol_learn <prefab|all>` learns all recipes/discoveries, or a prefab item/build piece/crafting station and its related recipes.
  - `adminqol_unlearn <prefab|all>` unlearns all known items, or a prefab item/build piece/crafting station and its related recipes.
  - `adminqol_clearinventory` permanently removes every item entry from the local player's inventory.
  - `adminqol_itemsets_reload` reloads `AdminQoL.ItemSets.yml`.
  - `adminqol_itemsets_list` lists currently loaded YAML itemsets.
- Injects YAML itemsets into Valheim's vanilla `itemset` command, so custom sets work with `itemset <name> [quality] [keep]`.
- Optional YAML item metadata for Jewelcrafting sockets and EpicLoot magic items when those mods are loaded.
- EpicLoot set bonuses remain defined by EpicLoot `legendaries.json`; AdminQoL only marks itemset items with the matching EpicLoot IDs.
- Adds a ConsolePanel command browser to Valheim's F5 console, with command grouping, search, numbered favorite profiles, and F6 show/hide.
- Does not use ServerSync. This is intentionally a client/admin utility.

## Config

The BepInEx config file is `BepInEx/config/sighsorry.AdminQoL.cfg`.

- `1 - General`: `Require Server Admin` and `Load YAML Item Sets`.
- `2 - Gameplay QoL`: unlock popup/log suppression, skill level-up effects/alarm removal, and instant equip.
- `3 - Console Panel`: panel size, visual style, vanilla console bottom offset, mouse cursor release, and toggle key.
- `4 - Console Panel Favorites`: favorite tab count and favorite command lists.

Learn/unlearn examples:

```text
adminqol_learn all
adminqol_learn ShieldWood
adminqol_learn piece_workbench
adminqol_unlearn all
adminqol_unlearn ShieldWood
adminqol_unlearn piece_workbench
```

Targets match prefab names only. Item targets affect item discovery and matching output recipes; crafting station targets affect the station entry and recipes crafted there. Both full learn and full unlearn use the explicit `all` target, and `all` is included in tab completion.

## YAML Itemsets

On first launch, the mod creates `BepInEx/config/AdminQoL.ItemSets.yml` from Valheim's built-in itemsets. The file is generated once and is never overwritten by AdminQoL after it exists.

AdminQoL also creates and loads these optional example/reference files:

- `BepInEx/config/AdminQoL.ItemSets.EpicLoot.yml`
- `BepInEx/config/AdminQoL.ItemSets.Jewelcrafting.yml`

All files matching `AdminQoL.ItemSets*.yml` are loaded. The generated EpicLoot file is parsed only when EpicLoot is installed, and the generated Jewelcrafting file is parsed only when Jewelcrafting is installed. Their example entries are copied from all vanilla itemsets, renamed, and expanded with mod-specific blocks. They are `enabled: true` by default; set an entry to `enabled: false` if you do not want it in the `itemset` command.

```yaml
itemSets:
  - name: MyAdminSet
    items:
      - prefab: SwordIron
        quality: 4
        stack: 1
        use: true
        hotbarSlot: 1
    skills:
      - skill: Swords
        level: 100
    knownStations:
      - piece_workbench
    knownItems:
      - Wood
      - Stone
    inheritKnownFromItemSet:
      - Start
```

`hotbarSlot` is Valheim's itemset hotbar placement. `1` means hotbar slot 1, `8` means hotbar slot 8, and `0` or an omitted field means no hotbar move.

If a YAML set name matches a vanilla set and `replaceExisting` is true, the YAML definition replaces the vanilla set at runtime. Missing `replaceExisting` is treated as false. Delete a generated file if you want AdminQoL to regenerate that file.

Jewelcrafting socket values support exact gem prefab names, `empty`, `random_simple`, `random_advanced`, and `random_perfect`. EpicLoot effects support exact effect IDs or `type: random`; `value: random` rolls through EpicLoot's effect value ranges.

AdminQoL does not register EpicLoot set definitions. Define custom EpicLoot sets in EpicLoot's `legendaries.json`, then reference the loaded `legendaryId` and optional `setId` from an `AdminQoL.ItemSets*.yml` file. If `setId` is omitted, AdminQoL asks EpicLoot which loaded set owns that `legendaryId`. The generated `AdminQoL.ItemSets.EpicLoot.yml` documents every supported `epicLoot:` field.
