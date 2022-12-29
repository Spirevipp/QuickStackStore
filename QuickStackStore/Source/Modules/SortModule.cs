﻿using System;
using System.Collections.Generic;
using System.Linq;
using static QuickStackStore.QSSConfig;

namespace QuickStackStore
{
    public static class SortModule
    {
        /* Categories:
            0 = None, Customization, Misc
            1 = Trophie
            2 = Material
            3 = Fish
            4 = Consumable
            5 = AmmoNonEquipable
            6 = Ammo
            7 = Bow, Tool, OneHandedWeapon, TwoHandedWeapon, TwoHandedWeaponLeft, Attach_Atgeir, Torch
            8 = Shield
            9 = Utility
            10 = Helmet, Shoulder, Chest, Hands, Legs
        */

        // convert the type enum to custom categories
        public static int[] TypeToCategory = new int[] { 0, 2, 4, 7, 7, 8, 10, 10, 0, 6, 0, 10, 10, 1, 7, 7, 0, 10, 9, 7, 7, 3, 7, 5 };

        private static bool ShouldSortItem(ItemDrop.ItemData item, UserConfig playerConfig, int inventoryHeight, bool includeHotbar)
        {
            return !playerConfig.IsItemNameFavorited(item.m_shared)
                && ShouldSortSlot(item.m_gridPos, playerConfig, inventoryHeight, includeHotbar);
        }

        // when changing this, also change SortModule.GetAllowedSlots
        private static bool ShouldSortSlot(Vector2i slot, UserConfig playerConfig, int playerInventoryHeight, bool includeHotbar)
        {
            return (slot.y > 0 || includeHotbar)
                && !playerConfig.IsSlotFavorited(slot)
                && !CompatibilitySupport.IsEquipOrQuickSlot(playerInventoryHeight, slot);
        }

        public static void DoSort(Player player)
        {
            Container container = InventoryGui.instance.m_currentContainer;

            var playerConfig = UserConfig.GetPlayerConfig(player.GetPlayerID());

            if (container != null)
            {
                switch (SortConfig.SortHotkeyBehaviorWhenContainerOpen.Value)
                {
                    case SortBehavior.OnlySortContainer:
                        Sort(InventoryGui.instance.m_currentContainer.m_inventory);
                        break;

                    case SortBehavior.SortBoth:
                        Sort(InventoryGui.instance.m_currentContainer.m_inventory);
                        Sort(player.m_inventory, playerConfig);
                        break;
                }
            }
            else
            {
                Sort(player.m_inventory, playerConfig);
            }
        }

        public static IComparable SortByGetter(ItemDrop.ItemData item)
        {
            switch (SortConfig.SortCriteria.Value)
            {
                case SortCriteriaEnum.TranslatedName:
                    return Localization.instance.Localize(item.m_shared.m_name);

                case SortCriteriaEnum.Value:
                    return item.m_shared.m_value;

                case SortCriteriaEnum.Weight:
                    return item.m_shared.m_weight;

                case SortCriteriaEnum.Type:
                    var typeNum = (int)item.m_shared.m_itemType;

                    if (typeNum < 0 || typeNum > 23)
                    {
                        return typeNum;
                    }
                    else
                    {
                        return TypeToCategory[(int)item.m_shared.m_itemType];
                    }

                case SortCriteriaEnum.InternalName:
                default:
                    return item.m_shared.m_name;
            }
        }

        public static int SortCompare(ItemDrop.ItemData a, ItemDrop.ItemData b)
        {
            int comp = SortByGetter(a).CompareTo(SortByGetter(b));

            if (!SortConfig.SortInAscendingOrder.Value)
            {
                comp *= -1;
            }

            if (comp == 0)
            {
                comp = a.m_shared.m_name.CompareTo(b.m_shared.m_name);
            }

            if (comp == 0)
            {
                comp = -a.m_quality.CompareTo(b.m_quality);
            }

            if (comp == 0)
            {
                comp = -a.m_stack.CompareTo(b.m_stack);
            }

            return comp;
        }

        public static void Sort(Inventory inventory, UserConfig playerConfig = null)
        {
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();

            bool includeHotbar = playerConfig == null || (GeneralConfig.OverrideHotkeyBarBehavior.Value != OverrideHotkeyBarBehavior.NeverAffectHotkeyBar && SortConfig.SortIncludesHotkeyBar.Value);

            var allowedSlots = GetAllowedSlots(inventory, includeHotbar, playerConfig);

            List<ItemDrop.ItemData> toSort;

            if (playerConfig == null)
            {
                toSort = new List<ItemDrop.ItemData>(inventory.m_inventory);
            }
            else
            {
                toSort = inventory.m_inventory.Where(item => ShouldSortItem(item, playerConfig, inventory.GetHeight(), includeHotbar)).ToList();
            }

            if (SortConfig.SortMergesStacks.Value)
            {
                MergeStacks(toSort, inventory);
            }

            toSort.Sort((a, b) => SortCompare(a, b));

            for (int i = 0; i < toSort.Count; i++)
            {
                Helper.Log($"Sorting item from ({toSort[i].m_gridPos}) to ({allowedSlots[i]})", DebugSeverity.Everything);

                toSort[i].m_gridPos = allowedSlots[i];
            }

            sw.Stop();
            Helper.Log($"Sorting time: {sw.Elapsed}", DebugSeverity.AlsoSpeedTests);

            inventory.Changed();
        }

        private static List<Vector2i> GetAllowedSlots(Inventory inventory, bool includeHotbar, UserConfig playerConfig)
        {
            var allowedSlots = new List<Vector2i>();

            int y;
            int yMax;

            if (GeneralConfig.UseTopDownLogicForEverything.Value)
            {
                y = includeHotbar ? 0 : 1;
                yMax = inventory.GetHeight();
            }
            else
            {
                // this simulates iterating backwards, when you negate/abs y
                y = -inventory.GetHeight() + 1;
                yMax = includeHotbar ? 1 : 0;
            }

            var blockedSlots = new HashSet<Vector2i>();

            if (playerConfig != null)
            {
                foreach (var item in inventory.m_inventory)
                {
                    if (playerConfig.IsItemNameFavorited(item.m_shared))
                    {
                        blockedSlots.Add(item.m_gridPos);
                    }
                }
            }

            for (; y < yMax; y++)
            {
                for (int x = 0; x < inventory.GetWidth(); x++)
                {
                    // see y initialization for reason for abs
                    var pos = new Vector2i(x, Math.Abs(y));

                    if (playerConfig != null)
                    {
                        if (blockedSlots.Contains(pos))
                        {
                            continue;
                        }

                        if (SortConfig.SortLeavesEmptyFavoritedSlotsEmpty.Value && playerConfig.IsSlotFavorited(pos))
                        {
                            continue;
                        }

                        if (CompatibilitySupport.IsEquipOrQuickSlot(inventory.GetHeight(), pos))
                        {
                            continue;
                        }
                    }

                    allowedSlots.Add(pos);
                }
            }

            return allowedSlots;
        }

        internal static void MergeStacks(List<ItemDrop.ItemData> toMerge, Inventory inventory)
        {
            var grouped = toMerge.Where(itm => itm.m_stack < itm.m_shared.m_maxStackSize).GroupBy(itm => new { itm.m_shared.m_name, itm.m_quality }).Select(grouping => grouping.ToList()).ToList();

            foreach (var nonFullStacks in grouped)
            {
                if (nonFullStacks.Count <= 1)
                {
                    continue;
                }

                var totalItemCount = 0;

                foreach (var item in nonFullStacks)
                {
                    totalItemCount += item.m_stack;
                }

                var maxStack = nonFullStacks.First().m_shared.m_maxStackSize;

                var remainingItemCount = totalItemCount;

                foreach (var item in nonFullStacks)
                {
                    if (remainingItemCount <= 0)
                    {
                        item.m_stack = 0;
                        inventory.RemoveItem(item);
                        toMerge.Remove(item);
                    }
                    else
                    {
                        item.m_stack = Math.Min(maxStack, remainingItemCount);

                        remainingItemCount -= item.m_stack;
                    }
                }
            }
        }
    }
}