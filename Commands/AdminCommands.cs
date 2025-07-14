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

    if (!TraderService.TryGetTraderInPlot(plot, out var trader)) {
      ctx.Reply("No shop found in this plot.".FormatError());
      return;
    }

    if (!trader.IsEmpty()) {
      ctx.Reply("Shop or storage isn't empty. Use ~.market forceremove trader~ to remove anyway.".FormatError());
      return;
    }

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

    TraderService.ForceRemoveTrader(player);
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

    if (TraderService.TryGetTraderInPlot(plot, out var trader)) {
      trader.AlignToPlotRotation(plot.Rotation);
    }

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

  [Command("iwanttoremoveall", "Remove all entities. Be careful!", adminOnly: true)]
  public static void RemoveAllEntities(ChatCommandContext ctx) {
    TraderService.ClearAll();
    ctx.Reply("All entities have been cleared.".FormatSuccess());
  }
}