using System;
using System.Collections.Generic;
using System.Text;
using ProjectM;
using ScarletCore;
using ScarletCore.Data;
using ScarletCore.Services;
using ScarletCore.Systems;
using ScarletCore.Utils;
using ScarletMarket.Models;
using ScarletMarket.Services;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using VampireCommandFramework;

namespace ScarletMarket.Commands;

[CommandGroup("market")]
public static class AdminCommands {
  private static readonly Dictionary<PlayerData, PlotModel> _selectedPlots = [];
  private static readonly Dictionary<PlayerData, ActionId> _selectedActions = [];

  [Command("reload", adminOnly: true)]
  public static void ReloadTraderService(ChatCommandContext ctx) {
    TraderService.Reload();
    Plugin.ReloadSettings();
    PrefabService.LoadItemPrefabNames();
    ctx.Reply("Trader service has been reloaded.".FormatSuccess());
  }

  [Command("forceopen", adminOnly: true)]
  public static void ForceOpenShop(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Couldn't find your player data.".FormatError());
      return;
    }

    if (!TraderService.TryGetPlot(player.Position, out var plot)) {
      ctx.Reply("You need to be inside a plot to open a shop.".FormatError());
      return;
    }

    if (plot.Trader == null) {
      ctx.Reply("No shop found in this plot.".FormatError());
      return;
    }

    var trader = plot.Trader;

    if (trader.AdminSetAsReady(player)) {
      ctx.Reply($"Shop {trader.Name} has been forcefully opened.".FormatSuccess());
    } else {
      ctx.Reply("Failed to open shop.".FormatError());
    }
  }

  [Command("forceclose", adminOnly: true)]
  public static void ForceCloseShop(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Couldn't find your player data.".FormatError());
      return;
    }

    if (!TraderService.TryGetPlot(player.Position, out var plot)) {
      ctx.Reply("You need to be inside a plot to close a shop.".FormatError());
      return;
    }

    if (plot.Trader == null) {
      ctx.Reply("No shop found in this plot.".FormatError());
      return;
    }

    var trader = plot.Trader;

    trader.SetAsNotReady();

    ctx.Reply($"Shop {trader.Name} has been forcefully closed.".FormatSuccess());
  }

  [Command("forceopenall", adminOnly: true)]
  public static void ForceOpenAllShops(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Couldn't find your player data.".FormatError());
      return;
    }
    var traders = TraderService.TraderEntities.Values;
    if (traders.Count == 0) {
      ctx.Reply("No shops found.".FormatError());
      return;
    }

    foreach (var trader in traders) {
      trader.AdminSetAsReady(player);
    }

    ctx.Reply($"All shops have been forcefully opened.".FormatSuccess());
  }

  [Command("forcecloseall", adminOnly: true)]
  public static void ForceCloseAllShops(ChatCommandContext ctx) {
    var traders = TraderService.TraderEntities.Values;
    if (traders.Count == 0) {
      ctx.Reply("No shops found.".FormatError());
      return;
    }

    foreach (var trader in traders) {
      trader.SetAsNotReady();
    }

    ctx.Reply($"All shops have been forcefully closed.".FormatSuccess());
  }

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
      } else if (inp.State.InputsDown == SyncedButtonInputAction.OffensiveSpell) {
        plot.Rotate();
      }
    });

    ActionScheduler.Delayed(() => {
      if (_selectedActions.TryGetValue(player, out ActionId actionId))
        ActionScheduler.CancelAction(actionId);
    }, 180);

    ctx.Reply($"Plot attached to you, please move it to the desired position.".FormatSuccess());
  }

  [Command("forcemove", adminOnly: true)]
  public static void ForceMovePlot(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Couldn't find your player data.".FormatError());
      return;
    }

    EntityInput entityInput() => player.CharacterEntity.Read<EntityInput>();

    if (!_selectedPlots.TryGetValue(player, out var plot) && !TraderService.TryGetPlot(entityInput().AimPosition, out plot)) {
      ctx.Reply("You need to aim at a plot to move it.".FormatError());
      return;
    }

    _selectedActions[player] = ActionScheduler.OncePerFrame((end) => {
      var inp = entityInput();
      var existingPlot = TraderService.TryGetPlot(inp.AimPosition, out _, plot);
      if (existingPlot) return;
      plot.MovePlotTo(inp.AimPosition);
      if (inp.State.InputsDown == SyncedButtonInputAction.Primary) {
        end();
        _selectedActions.Remove(player);
        ctx.Reply("Plot placed.".FormatSuccess());
      } else if (inp.State.InputsDown == SyncedButtonInputAction.OffensiveSpell) {
        plot.Rotate();
      }
    });

    ActionScheduler.Delayed(() => {
      if (_selectedActions.TryGetValue(player, out ActionId actionId))
        ActionScheduler.CancelAction(actionId);
    }, 180);

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
    }

    ctx.Reply($"Showing {plots.Count} plots.".FormatSuccess());
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
    }

    ctx.Reply($"Hiding {plots.Count} plots.".FormatSuccess());
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

    ctx.Reply($"You now have access to the shop {trader.Name}.".FormatSuccess());
    ctx.Reply($"Don't forget to use ~.market revokeaccess~ to give access back to the owner.".FormatSuccess());
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
    ctx.Reply($"Access revoked! Shop {trader.Name} is now back to the owner.".FormatSuccess());
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

  [Command("forcerename", adminOnly: true)]
  public static void Rename(ChatCommandContext ctx, string newName) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Player not found.".FormatError());
      return;
    }

    if (!TraderService.TryGetPlot(player.Position, out var plot)) {
      ctx.Reply("You need to be inside a plot to rename a shop.".FormatError());
      return;
    }

    if (plot.Trader == null) {
      ctx.Reply("No shop found in this plot.".FormatError());
      return;
    }

    var trader = plot.Trader;

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

  [Command("getinactive", adminOnly: true)]
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

  [Command("list", adminOnly: true)]
  public static void ListShops(ChatCommandContext ctx) {
    var traders = TraderService.TraderEntities.Values;

    if (traders.Count == 0) {
      ctx.Reply("No shops found.".FormatError());
      return;
    }

    ctx.Reply($"Found {traders.Count} shops:".FormatSuccess());

    foreach (var trader in traders) {
      var status = trader.State == TraderState.Ready ? "Open" : "Closed";
      var owner = trader.Owner.Name;
      var position = $"({trader.Position.x:F1}, {trader.Position.y:F1}, {trader.Position.z:F1})";

      ctx.Reply($"- ~{trader.Name}~ by {owner} [{status}] at {position}".Format());
    }
  }

  [Command("cleanorphans", "Remove only orphaned ScarletMarket entities in radius", adminOnly: true)]
  public static void CleanOrphanEntities(ChatCommandContext ctx, float radius) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Couldn't find your player data.".FormatError());
      return;
    }

    if (radius <= 0 || radius > 50) {
      ctx.Reply("Radius must be between 1 and 50 meters.".FormatError());
      return;
    }

    var playerPos = player.Position;
    var orphanedEntities = new List<Entity>();
    var registeredEntities = new HashSet<Entity>();

    // Collect all registered entities to protect them
    foreach (var trader in TraderService.TraderEntities.Values) {
      registeredEntities.Add(trader.Trader);
      registeredEntities.Add(trader.StorageChest);
      registeredEntities.Add(trader.Stand);
      registeredEntities.Add(trader.Coffin);
    }

    foreach (var plot in TraderService.Plots) {
      registeredEntities.Add(plot.Entity);
      registeredEntities.Add(plot.Inspect);

      // Protect GhostTrader entities
      if (plot.GhostPlaceholder != null) {
        registeredEntities.Add(plot.GhostPlaceholder.StorageChest);
        registeredEntities.Add(plot.GhostPlaceholder.Trader);
        registeredEntities.Add(plot.GhostPlaceholder.Coffin);
      }
    }

    // Counters for different entity types
    var entityCounts = new Dictionary<string, int> {
      ["Traders"] = 0,
      ["Storage"] = 0,
      ["Stands"] = 0,
      ["Coffins"] = 0,
      ["Plots"] = 0,
      ["Inspect"] = 0,
      ["GhostTraders"] = 0,
      ["GhostStorage"] = 0,
      ["GhostCoffins"] = 0
    };

    // Query for ScarletMarket entities
    var queryBuilder = new EntityQueryBuilder(Allocator.Temp);
    queryBuilder.AddAll(ComponentType.ReadOnly<NameableInteractable>());
    queryBuilder.WithOptions(EntityQueryOptions.IncludeDisabled);

    var query = GameSystems.EntityManager.CreateEntityQuery(ref queryBuilder).ToEntityArray(Allocator.Temp);

    foreach (var entity in query) {
      if (!entity.Has<NameableInteractable>()) continue;

      // Check if it's a ScarletMarket entity and identify type
      string entityType = null;
      if (entity.IdEquals(Ids.Trader)) entityType = "Traders";
      else if (entity.IdEquals(Ids.Storage)) entityType = "Storage";
      else if (entity.IdEquals(Ids.Stand)) entityType = "Stands";
      else if (entity.IdEquals(Ids.Coffin)) entityType = "Coffins";
      else if (entity.IdEquals(Ids.Plot)) entityType = "Plots";
      else if (entity.IdEquals(Ids.Inspect)) entityType = "Inspect";
      else if (entity.IdEquals(Ids.GhostTrader)) entityType = "GhostTraders";
      else if (entity.IdEquals(Ids.GhostStorage)) entityType = "GhostStorage";
      else if (entity.IdEquals(Ids.GhostCoffin)) entityType = "GhostCoffins";

      if (entityType == null) continue;

      // Skip if registered (not orphaned)
      if (registeredEntities.Contains(entity)) continue;

      // Check if within radius
      var entityPos = entity.Position();
      if (math.distance(playerPos, entityPos) <= radius) {
        orphanedEntities.Add(entity);
        entityCounts[entityType]++;
      }
    }

    if (orphanedEntities.Count == 0) {
      ctx.Reply($"No orphaned ScarletMarket entities found within {radius}m.".FormatSuccess());
      return;
    }

    // Remove orphaned entities
    foreach (var entity in orphanedEntities) {
      entity.Destroy();
    }

    // Build detailed message
    var message = new StringBuilder();
    message.AppendLine($"Removed {orphanedEntities.Count} orphaned ScarletMarket entities within {radius}m:");

    foreach (var kvp in entityCounts) {
      if (kvp.Value > 0) {
        message.AppendLine($"- {kvp.Key}: {kvp.Value}");
      }
    }

    ctx.Reply(message.ToString().TrimEnd().FormatSuccess());
  }

  [Command("forceremove radius", description: "Remove ALL ScarletMarket entities in radius. EXTREMELY DANGEROUS!", adminOnly: true)]
  public static void ForceRemoveRadius(ChatCommandContext ctx, float radius, string confirmation = null) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Couldn't find your player data.".FormatError());
      return;
    }

    if (confirmation != "IAGREE") {
      ctx.Reply("This command will ~REMOVE ALL~ ScarletMarket entities within the specified radius.".FormatError());
      ctx.Reply("To confirm, please re-run the command with the confirmation parameter: ~.market forceremove radius <radius> IAGREE~".FormatError());
      return;
    }

    if (radius <= 0 || radius > 50) {
      ctx.Reply("Radius must be between 1 and 50 meters.".FormatError());
      return;
    }

    var playerPos = player.Position;
    var entitiesToRemove = new List<Entity>();

    // Query ALL ScarletMarket entities
    var queryBuilder = new EntityQueryBuilder(Allocator.Temp);
    queryBuilder.AddAll(ComponentType.ReadOnly<NameableInteractable>());
    queryBuilder.WithOptions(EntityQueryOptions.IncludeDisabled);

    var query = GameSystems.EntityManager.CreateEntityQuery(ref queryBuilder).ToEntityArray(Allocator.Temp);

    foreach (var entity in query) {
      if (!entity.Has<NameableInteractable>()) continue;

      // Check if it's a ScarletMarket entity
      bool isScarletEntity =
        entity.IdEquals(Ids.Trader) || entity.IdEquals(Ids.Storage) || entity.IdEquals(Ids.Stand) ||
        entity.IdEquals(Ids.Coffin) || entity.IdEquals(Ids.Plot) || entity.IdEquals(Ids.Inspect) ||
        entity.IdEquals(Ids.GhostTrader) || entity.IdEquals(Ids.GhostStorage) || entity.IdEquals(Ids.GhostCoffin);

      if (!isScarletEntity) continue;

      var entityPos = entity.Position();
      if (math.distance(playerPos, entityPos) <= radius) {
        entitiesToRemove.Add(entity);
      }
    }

    if (entitiesToRemove.Count == 0) {
      ctx.Reply($"No ScarletMarket entities found within {radius}m.".FormatSuccess());
      return;
    }

    // Remove entities from registry first
    var tradersToRemove = new List<TraderModel>();
    var plotsToRemove = new List<PlotModel>();

    foreach (var entity in entitiesToRemove) {
      // Find and mark traders for removal
      var trader = TraderService.GetTrader(entity);
      if (trader != null && !tradersToRemove.Contains(trader)) {
        tradersToRemove.Add(trader);
      }

      // Find and mark plots for removal
      foreach (var plot in TraderService.Plots) {
        if (plot.Entity == entity || plot.Inspect == entity) {
          if (!plotsToRemove.Contains(plot)) {
            plotsToRemove.Add(plot);
          }
        }
      }
    }

    // Remove from registry
    foreach (var trader in tradersToRemove) {
      TraderService.TraderEntities.Remove(trader.Trader);
      TraderService.StorageEntities.Remove(trader.StorageChest);
      TraderService.StandEntities.Remove(trader.Stand);
      TraderService.TraderById.Remove(trader.Owner.PlatformId);
    }

    foreach (var plot in plotsToRemove) {
      TraderService.Plots.Remove(plot);
    }

    // Destroy entities
    int removed = 0;
    foreach (var entity in entitiesToRemove) {
      entity.Destroy();
      removed++;
    }

    ctx.Reply($"FORCE REMOVED {removed} ScarletMarket entities within {radius}m.".FormatSuccess());
    ctx.Reply($"Removed {tradersToRemove.Count} traders and {plotsToRemove.Count} plots from registry.".Format());
  }
}