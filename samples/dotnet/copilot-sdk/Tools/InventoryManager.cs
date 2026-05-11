// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.AI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace CopilotSdk.Tools;

public static class InventoryManager
{
    private static readonly ConcurrentDictionary<string, List<string>> _inventories = new();

    public static AIFunction CreateTool(string conversationId)
    {
        InventoryTool tool = new(conversationId);

        return AIFunctionFactory.Create(
            (Func<string, string?, string>)tool.Handle,
            "manage_inventory",
            "Manage the adventurer's inventory by adding, removing, or listing items.");
    }

    private static string Manage(string conversationId, string action, string? item)
    {
        string normalizedAction = action?.Trim().ToLowerInvariant() ?? string.Empty;
        string? normalizedItem = string.IsNullOrWhiteSpace(item) ? null : item.Trim();
        List<string> inventory = _inventories.GetOrAdd(conversationId, _ => new List<string>());

        return normalizedAction switch
        {
            "add" when normalizedItem == null => "Specify an item to add to the inventory.",
            "add" => AddItem(inventory, normalizedItem),
            "remove" when normalizedItem == null => "Specify an item to remove from the inventory.",
            "remove" => RemoveItem(inventory, normalizedItem),
            "list" => ListItems(inventory),
            _ => $"Unknown action '{normalizedAction}'. Use 'add', 'remove', or 'list'."
        };
    }

    private static string AddItem(List<string> inventory, string item)
    {
        lock (inventory)
        {
            inventory.Add(item);
            return $"📦 Added '{item}' to inventory. ({inventory.Count} items total)";
        }
    }

    private static string RemoveItem(List<string> inventory, string item)
    {
        lock (inventory)
        {
            if (inventory.Remove(item))
            {
                return $"🗑️ Removed '{item}' from inventory. ({inventory.Count} items remaining)";
            }
        }

        return $"'{item}' not found in inventory.";
    }

    private static string ListItems(List<string> inventory)
    {
        lock (inventory)
        {
            if (inventory.Count == 0)
            {
                return "🎒 The adventurer's pack is empty.";
            }

            string items = string.Join("\n", inventory.Select(i => $"  • {i}"));
            return $"🎒 Inventory ({inventory.Count} items):\n{items}";
        }
    }

    private sealed class InventoryTool(string conversationId)
    {
        [Description("Manage the adventurer's inventory.")]
        public string Handle(
            [Description("Action to perform: add, remove, or list.")] string action,
            [Description("Item name for add or remove actions.")] string? item = null)
        {
            return Manage(conversationId, action, item);
        }
    }
}
