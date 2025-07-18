using System;
using ScarletCore;
using ScarletCore.Services;
using ScarletCore.Utils;
using ScarletMarket.Services;
using VampireCommandFramework;

namespace ScarletMarket.Commands;

[CommandGroup("market")]
public static class AdminCommands {
  [Command("create plot", adminOnly: true)]
  public static void CreatePlot(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Couldn't find your player data.".FormatError());
      return;
    }

    if (!TraderService.TryCreatePlot(player, out var plot)) {
      return;
    }

    ctx.Reply($"Plot created at {plot.Position}.".FormatSuccess());
  }

  [Command("claimaccess", adminOnly: true)]
  public static void GiveMeAccess(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Couldn't find your player data.".FormatError());
      return;
    }

    if (!TraderService.TryGetPlot(player.Position, out var plot)) {
      ctx.Reply("You need to be inside a plot to get access.".FormatError());
      return;
    }

    if (plot.Trader == null) {
      ctx.Reply("No shop found in this plot.".FormatError());
      return;
    }

    var trader = plot.Trader;

    trader.Stand.SetTeam(player.CharacterEntity);
    trader.StorageChest.SetTeam(player.CharacterEntity);

    ctx.Reply($"You now have access to the shop at {plot.Position}.".FormatSuccess());
  }

  [Command("revokeaccess", adminOnly: true)]
  public static void RevokeMyAccess(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Couldn't find your player data.".FormatError());
      return;
    }

    if (!TraderService.TryGetPlot(player.Position, out var plot)) {

      return;
    }

    if (plot.Trader == null) {
      ctx.Reply("No shop found in this plot.".FormatError());
      return;
    }

    var trader = plot.Trader;

    if (trader.State == TraderState.Ready) {
      trader.Stand.SetTeam(TraderService.DefaultStandEntity);
    } else {
      trader.Stand.SetTeam(trader.Owner.CharacterEntity);
    }

    trader.StorageChest.SetTeam(trader.Owner.CharacterEntity);

    // given acces back to the owner
    ctx.Reply($"Access revoked! Shop is now private to its owner.".FormatSuccess());
  }

  [Command("remove shop", adminOnly: true)]
  public static void RemoveTrader(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Couldn't find your player data.".FormatError());
      return;
    }

    if (!TraderService.TryGetPlot(player.Position, out var plot)) {
      ctx.Reply("You need to be inside a plot to remove a shop.".FormatError());
      return;
    }

    if (plot.Trader == null) {
      ctx.Reply("No shop found in this plot.".FormatError());
      return;
    }

    var trader = plot.Trader;

    if (!trader.IsEmpty()) {
      ctx.Reply("Shop or storage isn't empty. Use ~.market forceremove trader~ to remove anyway.".FormatError());
      return;
    }

    TraderService.ForceRemoveTrader(trader.Owner);

    ctx.Reply($"Shop {trader.Name} has been removed from the plot.".FormatSuccess());
  }

  [Command("remove plot", adminOnly: true)]
  public static void RemovePlot(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Couldn't find your player data.".FormatError());
      return;
    }

    if (!TraderService.TryGetPlot(player.Position, out var plot)) {
      ctx.Reply("You need to be inside a plot to remove it.".FormatError());
      return;
    }

    if (!TraderService.IsPlotEmpty(plot)) {
      ctx.Reply("This plot isn't empty. Remove the shop first.".FormatError());
      return;
    }

    TraderService.ForceRemovePlot(plot);
    ctx.Reply("Plot has been removed.".FormatSuccess());
  }

  [Command("forceremove plot", adminOnly: true)]
  public static void ForceRemovePlot(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Couldn't find your player data.".FormatError());
      return;
    }

    if (!TraderService.TryGetPlot(player.Position, out var plot)) {
      ctx.Reply("You need to be inside a plot to remove it.".FormatError());
      return;
    }

    TraderService.ForceRemovePlot(plot);

    ctx.Reply("Plot has been forcefully removed.".FormatSuccess());
  }

  [Command("forceremove shop", adminOnly: true)]
  public static void ForceRemoveTrader(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Couldn't find your player data.".FormatError());
      return;
    }

    if (!TraderService.TryGetPlot(player.Position, out var plot)) {
      ctx.Reply("You need to be inside a plot to remove a shop.".FormatError());
      return;
    }

    if (!TraderService.TryGetTraderInPlot(plot, out var trader)) {
      ctx.Reply("No shop found in this plot.".FormatError());
      return;
    }

    TraderService.ForceRemoveTrader(trader.Owner);
    ctx.Reply($"Shop {trader.Name} has been forcefully removed from the plot.".FormatSuccess());
  }

  [Command("rotate", adminOnly: true)]
  public static void RotatePlot(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Couldn't find your player data.".FormatError());
      return;
    }

    if (!TraderService.TryGetPlot(player.Position, out var plot)) {
      ctx.Reply("You need to be inside a plot to rotate it.".FormatError());
      return;
    }

    plot.Rotate();

    ctx.Reply($"Plot rotated to {plot.GetCurrentRotationDegrees()} degrees.".FormatSuccess());
  }

  [Command("clear emptyplots", adminOnly: true)]
  public static void ClearEmptyPlots(ChatCommandContext ctx) {
    var count = TraderService.ClearEmptyPlots();
    ctx.Reply($"Removed {count} empty plots.".FormatSuccess());
  }

  [Command("clear emptyshops", adminOnly: true)]
  public static void ClearEmptyTraders(ChatCommandContext ctx) {
    var count = TraderService.ClearEmptyTraders();
    ctx.Reply($"Removed {count} empty shops.".FormatSuccess());
  }

  [Command("get inactive", adminOnly: true)]
  public static void GetInactiveShops(ChatCommandContext ctx, int days) {
    var platformIds = TraderService.TraderById.Keys;
    int count = 0;

    foreach (var id in platformIds) {
      if (!PlayerService.TryGetById(id, out var player)) {
        continue;
      }

      var lastConnected = player.ConnectedSince;

      if (lastConnected.AddDays(days) > DateTime.UtcNow) {
        continue;
      }

      var trader = TraderService.GetTrader(player.PlatformId);

      if (trader == null) {
        continue;
      }

      count++;
      ctx.Reply($"Inactive shop found: ~{trader.Name}~ at ~{trader.Position}~ (Last connected: ~{lastConnected}~)".FormatSuccess());
    }

    ctx.Reply($"Found ~{count}~ inactive shops that haven't been used in the last ~{days}~ days.".Format());
  }

  [Command("iwanttoclearinactiveshops", adminOnly: true)]
  public static void ClearInactiveShops(ChatCommandContext ctx, int maxDays) {
    int count = 0;
    var traders = TraderService.TraderEntities.Values;

    foreach (var trader in traders) {
      var player = trader.Owner;
      var lastOnline = player.ConnectedSince;
      if (lastOnline < DateTime.Now.AddDays(-maxDays)) {
        TraderService.ForceRemoveTrader(player);
        count++;
        ctx.Reply($"Removed inactive shop: ~{trader.Name}~ at ~{trader.Position}~ (Last connected: ~{lastOnline}~)".FormatSuccess());
      }
    }

    ctx.Reply($"Removed {count} inactive shops that haven't been used in the last {maxDays} days.".FormatSuccess());
  }

  [Command("iwanttoremoveeverything", "Remove all shop related entities. Be careful!", adminOnly: true)]
  public static void RemoveAllEntities(ChatCommandContext ctx) {
    TraderService.ClearAll();
    ctx.Reply("All entities have been cleared.".FormatSuccess());
  }
}