using System;
using System.Collections.Generic;
using ProjectM;
using ScarletCore;
using ScarletCore.Data;
using ScarletCore.Services;
using ScarletCore.Systems;
using ScarletCore.Utils;
using ScarletMarket.Models;
using ScarletMarket.Services;
using Unity.Mathematics;
using VampireCommandFramework;

namespace ScarletMarket.Commands;

[CommandGroup("market")]
public static class AdminCommands {
  private static readonly Dictionary<PlayerData, PlotModel> _selectedPlots = [];
  private static readonly Dictionary<PlayerData, ActionId> _selectedActions = [];

  [Command("create plot", adminOnly: true)]
  public static void CreatePlot(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Couldn't find your player data.".FormatError());
      return;
    }

    if (!TraderService.TryCreatePlot(player, false, out var plot)) {
      return;
    }

    ctx.Reply($"Plot created at {plot.Position}.".FormatSuccess());
  }

  [Command("forcecreate plot", adminOnly: true)]
  public static void ForceCreatePlot(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Couldn't find your player data.".FormatError());
      return;
    }

    if (!TraderService.TryCreatePlot(player, true, out var plot)) {
      return;
    }

    ctx.Reply($"Plot created at {plot.Position}.".FormatSuccess());
  }

  [Command("select", adminOnly: true)]
  public static void SelectPlot(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Couldn't find your player data.".FormatError());
      return;
    }

    if (!TraderService.TryGetPlot(player.Position, out var plot)) {
      ctx.Reply("You need to be inside a plot to select it.".FormatError());
      return;
    }

    _selectedPlots[player] = plot;
    ctx.Reply($"Plot at {plot.Position} selected.".FormatSuccess());
  }

  [Command("deselect", adminOnly: true)]
  public static void DeselectPlot(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Couldn't find your player data.".FormatError());
      return;
    }

    if (_selectedPlots.Remove(player)) {
      ctx.Reply("Plot selection cleared.".FormatSuccess());
    } else {
      ctx.Reply("You have no plot selected.".FormatError());
    }
  }

  [Command("move", adminOnly: true)]
  public static void MovePlot(ChatCommandContext ctx, float x, float y, float z) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Couldn't find your player data.".FormatError());
      return;
    }

    if (!_selectedPlots.TryGetValue(player, out var plot)) {
      ctx.Reply("You have no plot selected.".FormatError());
      return;
    }

    var newPosition = new float3(x, y, z);

    if (TraderService.WillOverlapWithExistingPlot(newPosition, plot)) {
      ctx.Reply("~Cannot move plot:~ would block access to an existing one.".FormatError());
      return;
    }
    plot.MovePlotTo(newPosition);
    ctx.Reply($"Plot moved to {newPosition}.".FormatSuccess());
  }

  [Command("move", adminOnly: true)]
  public static void MovePlot(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Couldn't find your player data.".FormatError());
      return;
    }

    EntityInput entityInput() => player.CharacterEntity.Read<EntityInput>();

    if (!_selectedPlots.TryGetValue(player, out var plot) && !TraderService.TryGetPlot(entityInput().AimPosition, out plot)) {
      ctx.Reply("You need to aim at a plot to move it.".FormatError());
      return;
    }

    plot.Show();

    _selectedActions[player] = ActionScheduler.OncePerFrame((end) => {
      var inp = entityInput();
      if (TraderService.WillOverlapWithExistingPlot(inp.AimPosition, plot)) {
        return;
      }
      plot.MovePlotTo(inp.AimPosition);
      if (inp.State.InputsDown == SyncedButtonInputAction.Primary) {
        end();
        _selectedActions.Remove(player);
        ctx.Reply("Plot placed.".FormatSuccess());
        plot.Hide();
      } else if (inp.State.InputsDown == SyncedButtonInputAction.OffensiveSpell) {
        plot.Rotate();
      }
    });

    ctx.Reply($"Plot attached to you, please move it to the desired position.".FormatSuccess());
  }

  [Command("place", adminOnly: true)]
  public static void DetachPlot(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Couldn't find your player data.".FormatError());
      return;
    }

    if (_selectedPlots.TryGetValue(player, out var plot)) {
      if (TraderService.WillOverlapWithExistingPlot(player.Position, plot)) {
        ctx.Reply("~Cannot move plot:~ would block access to an existing one.".FormatError());
        return;
      }
      _selectedPlots.Remove(player);
      plot.MovePlotTo(player.Position);
      ctx.Reply("Plot placed.".FormatSuccess());
    } else {
      ctx.Reply("You have no plot attached.".FormatError());
    }
  }

  [Command("showradius", adminOnly: true)]
  public static void ShowRadius(ChatCommandContext ctx) {
    var plots = TraderService.Plots;
    if (plots.Count == 0) {
      ctx.Reply("No plots found.".FormatError());
      return;
    }

    foreach (var plot in plots) {
      plot.ShowPlot();
      ctx.Reply($"Showing plot at {plot.Position} with radius {plot.Radius}.".FormatSuccess());
    }
  }

  [Command("hideradius", adminOnly: true)]
  public static void HideRadius(ChatCommandContext ctx) {
    var plots = TraderService.Plots;
    if (plots.Count == 0) {
      ctx.Reply("No plots found.".FormatError());
      return;
    }

    foreach (var plot in plots) {
      plot.HidePlot();
      ctx.Reply($"Hiding plot at {plot.Position}.".FormatSuccess());
    }
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
    int count = 0;
    var traders = TraderService.TraderEntities.Values;

    foreach (var trader in traders) {
      var player = trader.Owner;
      var lastConnected = player.ConnectedSince;

      if (lastConnected.AddDays(days) > DateTime.UtcNow) {
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