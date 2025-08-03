using ScarletCore.Services;
using ScarletCore.Utils;
using ScarletMarket.Services;
using VampireCommandFramework;
using System.Text;
using System.Linq;
using ScarletMarket.Models;
using Unity.Mathematics;
using System.Collections.Generic;

namespace ScarletMarket.Commands;

[CommandGroup("market")]
public static class PlayerCommands {
  [Command("claim", "Set up your own shop to start trading")]
  public static void ClaimPlot(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Player not found.".FormatError());
      return;
    }

    TraderService.TryBuyPlot(player);
  }

  [Command("unclaim", "Delete your shop permanently")]
  public static void UnclaimPlot(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Player not found.".FormatError());
      return;
    }

    TraderService.TryRemoveTrader(player);
  }

  [Command("getowner")]
  public static void GetOwner(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Couldn't find your player data.".FormatError());
      return;
    }

    if (!TraderService.TryGetPlot(player.Position, out var plot)) {
      ctx.Reply("You need to be inside a plot to get its owner.".FormatError());
      return;
    }

    if (plot.Trader == null) {
      ctx.Reply("No shop found in this plot.".FormatError());
      return;
    }

    var trader = plot.Trader;
    ctx.Reply($"Shop owner: ~{trader.Owner.Name}~".FormatSuccess());
  }

  [Command("open", "Open your shop for business")]
  public static void Open(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Player not found.".FormatError());
      return;
    }

    var trader = TraderService.GetTrader(player.PlatformId);

    if (trader == null) {
      ctx.Reply("You don't have a shop yet! Use ~.market claim~ to get started.".FormatError());
      return;
    }

    trader.SetAsReady();

    ctx.Reply("Shop opened! You can now sell items to other players.".FormatSuccess());
  }

  [Command("close", "Close your shop for editing")]
  public static void Close(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Player not found.".FormatError());
      return;
    }

    var trader = TraderService.GetTrader(player.PlatformId);

    if (trader == null) {
      ctx.Reply("You don't have a shop yet! Use ~.market claim~ to get started.".FormatError());
      return;
    }

    trader.SetAsNotReady();

    ctx.Reply("Shop closed! You can now add or change items for sale.".FormatSuccess());
  }

  [Command("addcost", "Set the price for an item in your shop")]
  public static void AddCost(ChatCommandContext ctx, string itemName, int amount) {
    if (amount > 4000) {
      ctx.Reply("Whoa there! Price can't exceed ~4000~ items.".FormatError());
      return;
    }

    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Player not found.".FormatError());
      return;
    }

    var trader = TraderService.GetTrader(player.PlatformId);

    if (trader == null) {
      ctx.Reply("You don't have a shop yet! Use ~.market claim~ to get started.".FormatError());
      return;
    }

    if (!trader.HasItemWithNoCost()) {
      ctx.Reply("Add an item to your shop first, then set its price!".FormatError());
      return;
    }

    if (trader.State == TraderState.Ready) {
      ctx.Reply("Your shop is currently open. Use ~.market close~ to edit it.".FormatError());
      return;
    }

    var item = ItemSearchService.FindByExactName(itemName);

    var items = ItemSearchService.SearchByName(itemName);

    if (items.Count == 0) {
      ctx.Reply($"Couldn't find any item named '{itemName}'. Check the spelling?".FormatError());
      return;
    }

    if (items.Count > 1 && item.PrefabGUID.GuidHash == 0) {
      foreach (var result in items) {
        ctx.Reply($"- {result.Name}".Format());
      }
      ctx.Reply($"Found multiple items for '{itemName}'. Be more specific!".FormatError());
      ctx.Reply("Tip: Use ~\"~quotation marks~\"~ for items with spaces in their names.".Format());
      return;
    }

    if (items.Count == 1 && item.PrefabGUID.GuidHash == 0) {
      item = items[0];
    }

    if (!trader.TryAddCostItem(item.PrefabGUID, amount)) {
      ctx.Reply("You can't use the same item as both product and payment!".FormatError());
      return;
    }

    ctx.Reply($"Price set: {item.Name} x{amount}".FormatSuccess());
    ctx.Reply("Ready to open? Use ~.market open~ to start selling!".Format());
  }

  [Command("rename", "Change your shop's name")]
  public static void Rename(ChatCommandContext ctx, string newName) {
    if (!Plugin.Settings.Get<bool>("AllowCustomShopNames")) {
      ctx.Reply("Custom shop names are disabled.".FormatError());
      return;
    }
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Player not found.".FormatError());
      return;
    }

    var trader = TraderService.GetTrader(player.PlatformId);

    if (trader == null) {
      ctx.Reply("You don't have a shop yet! Use ~.market claim~ to get started.".FormatError());
      return;
    }

    if (string.IsNullOrWhiteSpace(newName)) {
      ctx.Reply("Shop name cannot be empty!".FormatError());
      return;
    }

    if (newName.Contains('(') || newName.Contains(')')) {
      ctx.Reply("Shop name cannot contain parentheses ~()~. They are reserved for shop status.".FormatError());
      return;
    }

    var byteCount = Encoding.UTF8.GetByteCount($"{newName} ({CLOSED_TEXT})");
    if (byteCount > 50) {
      ctx.Reply($"Shop name is too long! Try a shorter name.".FormatError());
      return;
    }

    if (trader.TrySetCustomName(newName)) {
      ctx.Reply($"Shop renamed to: ~{newName}~".FormatSuccess());
    } else {
      ctx.Reply("Failed to rename shop. Please try a different name.".FormatError());
    }
  }

  [Command("search buy", "Search for shops selling a specific item")]
  public static void SearchBuy(ChatCommandContext ctx, string itemName) {
    var item = ItemSearchService.FindAllByExactName(itemName);
    var items = ItemSearchService.SearchAllByName(itemName);

    if (items.Count == 0) {
      ctx.Reply($"Couldn't find any item named '{itemName}'. Check the spelling?".FormatError());
      return;
    }

    if (items.Count > 1 && item.PrefabGUID.GuidHash == 0) {
      foreach (var result in items) {
        ctx.Reply($"- {result.Name}".Format());
      }
      ctx.Reply($"Found multiple items for '{itemName}'. Be more specific!".FormatError());
      ctx.Reply("Tip: Use ~\"~quotation marks~\"~ for items with spaces in their names.".Format());
      return;
    }

    if (items.Count == 1 && item.PrefabGUID.GuidHash == 0) {
      item = items[0];
    }

    var searchResults = new List<(string shopName, string ownerName, string price, float3 position)>();

    // Search through all traders
    foreach (var trader in TraderService.TraderEntities.Values) {
      // Only search in open shops
      if (trader.State != TraderState.Ready) {
        continue;
      }

      // Check valid item slots (0-6 and 21-27)
      int[] validSlots = [0, 1, 2, 3, 4, 5, 6, 21, 22, 23, 24, 25, 26, 27];

      foreach (var slot in validSlots) {
        if (InventoryService.TryGetItemAtSlot(trader.Stand, slot, out var inventoryItem)) {
          if (inventoryItem.ItemType.GuidHash == item.PrefabGUID.GuidHash) {
            // Found the item! Get the price from the corresponding cost slot
            var costSlot = slot < 7 ? slot + 7 : slot + 7; // 0-6 -> 7-13, 21-27 -> 28-34

            if (InventoryService.TryGetItemAtSlot(trader.Stand, costSlot, out var costItem)) {
              var costItemResult = ItemSearchService.FindAllByPrefabGUID(costItem.ItemType);
              var costText = costItemResult.HasValue
                ? $"{costItem.Amount}x {costItemResult.Value.Name}"
                : $"{costItem.Amount}x Unknown Item";

              var shopName = trader.SanitizeName(trader.Name);
              if (string.IsNullOrWhiteSpace(shopName)) {
                shopName = $"{trader.Owner.Name}'s Shop";
              }

              searchResults.Add((shopName, trader.Owner.Name, costText, trader.Position));
            }
          }
        }
      }
    }

    if (searchResults.Count == 0) {
      ctx.Reply($"No shops are currently selling ~{item.Name}~.".FormatError());
      return;
    }

    ctx.Reply($"Found ~{item.Name}~ for sale at {searchResults.Count} shop(s):".FormatSuccess());

    foreach (var result in searchResults.Take(10)) { // Limit to 10 results to avoid spam
      ctx.Reply($"• ~{result.shopName}~ (Owner: {result.ownerName}) - Price: ~{result.price}~".Format());
    }

    if (searchResults.Count > 10) {
      ctx.Reply($"... and {searchResults.Count - 10} more shops. Try being more specific with your search.".Format());
    }
  }

  [Command("search sell", "Search for shops that want to buy a specific item")]
  public static void SearchSell(ChatCommandContext ctx, string itemName) {
    var item = ItemSearchService.FindAllByExactName(itemName);
    var items = ItemSearchService.SearchAllByName(itemName);

    if (items.Count == 0) {
      ctx.Reply($"Couldn't find any item named '{itemName}'. Check the spelling?".FormatError());
      return;
    }

    if (items.Count > 1 && item.PrefabGUID.GuidHash == 0) {
      foreach (var result in items) {
        ctx.Reply($"- {result.Name}".Format());
      }
      ctx.Reply($"Found multiple items for '{itemName}'. Be more specific!".FormatError());
      ctx.Reply("Tip: Use ~\"~quotation marks~\"~ for items with spaces in their names.".Format());
      return;
    }

    if (items.Count == 1 && item.PrefabGUID.GuidHash == 0) {
      item = items[0];
    }

    var searchResults = new List<(string shopName, string ownerName, string selling, float3 position)>();

    // Search through all traders
    foreach (var trader in TraderService.TraderEntities.Values) {
      // Only search in open shops
      if (trader.State != TraderState.Ready) {
        continue;
      }

      // Check valid cost slots (7-13 and 28-34) for the item we want to sell
      int[] costSlots = [7, 8, 9, 10, 11, 12, 13, 28, 29, 30, 31, 32, 33, 34];

      foreach (var costSlot in costSlots) {
        if (InventoryService.TryGetItemAtSlot(trader.Stand, costSlot, out var costItem)) {
          if (costItem.ItemType.GuidHash == item.PrefabGUID.GuidHash) {
            // Found a shop that wants this item! Get what they're selling
            var itemSlot = costSlot < 14 ? costSlot - 7 : costSlot - 7; // 7-13 -> 0-6, 28-34 -> 21-27

            if (InventoryService.TryGetItemAtSlot(trader.Stand, itemSlot, out var sellingItem)) {
              var sellingItemResult = ItemSearchService.FindAllByPrefabGUID(sellingItem.ItemType);
              var sellingText = sellingItemResult.HasValue
                ? $"{sellingItem.Amount}x {sellingItemResult.Value.Name}"
                : $"{sellingItem.Amount}x Unknown Item";

              var shopName = trader.SanitizeName(trader.Name);
              if (string.IsNullOrWhiteSpace(shopName)) {
                shopName = $"{trader.Owner.Name}'s Shop";
              }

              var priceText = $"Wants {costItem.Amount}x {item.Name} for {sellingText}";
              searchResults.Add((shopName, trader.Owner.Name, priceText, trader.Position));
            }
          }
        }
      }
    }

    if (searchResults.Count == 0) {
      ctx.Reply($"No shops are currently buying ~{item.Name}~.".FormatError());
      return;
    }

    ctx.Reply($"Found {searchResults.Count} shop(s) that want to buy ~{item.Name}~:".FormatSuccess());

    foreach (var result in searchResults.Take(10)) { // Limit to 10 results to avoid spam
      ctx.Reply($"• ~{result.shopName}~ (Owner: {result.ownerName}) - {result.selling}".Format());
    }

    if (searchResults.Count > 10) {
      ctx.Reply($"... and {searchResults.Count - 10} more shops. Try being more specific with your search.".Format());
    }
  }
}