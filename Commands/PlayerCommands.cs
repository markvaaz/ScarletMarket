using ScarletCore.Services;
using ScarletCore.Utils;
using ScarletMarket.Services;
using VampireCommandFramework;

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

    if (items.Count > 1 && item == null) {
      foreach (var result in items) {
        ctx.Reply($"- {result.Name}".Format());
      }
      ctx.Reply($"Found multiple items for '{itemName}'. Be more specific!".FormatError());
      ctx.Reply("Tip: Use ~\"~quotation marks~\"~ for items with spaces in their names.".Format());
      return;
    }

    if (!trader.TryAddCostItem(item.Value.PrefabGUID, amount)) {
      ctx.Reply("You can't use the same item as both product and payment!".FormatError());
      return;
    }

    ctx.Reply($"Price set: {item.Value.Name} x{amount}".FormatSuccess());
    ctx.Reply("Ready to open? Use ~.market open~ to start selling!".Format());
  }
}