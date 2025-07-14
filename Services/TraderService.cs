using System.Collections.Generic;
using ProjectM;
using ScarletCore;
using ScarletCore.Data;
using ScarletCore.Services;
using ScarletCore.Systems;
using ScarletCore.Utils;
using ScarletMarket.Models;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;

namespace ScarletMarket.Services;

internal static class TraderService {
  public static readonly Dictionary<Entity, TraderModel> TraderEntities = [];
  public static readonly Dictionary<Entity, TraderModel> StorageEntities = [];
  public static readonly Dictionary<Entity, TraderModel> StandEntities = [];
  public static readonly Dictionary<ulong, TraderModel> TraderById = [];
  public static readonly List<PlotModel> Plots = [];
  private static Entity _defaultStandEntity;
  public static Entity DefaultStandEntity {
    get {
      if (_defaultStandEntity == Entity.Null) {
        if (!PrefabGuidToEntityMap.TryGetValue(Spawnable.StandChest, out var defaultStand)) {
          Log.Error($"Failed to find prefab for GUID: {Spawnable.StandChest.GuidHash}");
          return Entity.Null;
        }
        _defaultStandEntity = defaultStand;
      }

      return _defaultStandEntity;
    }
  }

  public static void Initialize() {
    SetContainerSize(Spawnable.StorageChest, 35);
    SetContainerSize(Spawnable.StandChest, 35);
    RegisterOnLoad();
  }

  public static void CreateTrader(PlayerData player) {
    if (TraderById.ContainsKey(player.PlatformId)) {
      MessageService.Send(player, "You already have a shop!".FormatError());
      return;
    }

    if (!TryGetPlot(player.Position, out var plot)) {
      MessageService.Send(player, "You need to be inside a plot to create a shop.".FormatError());
      return;
    }

    if (!IsPlotEmpty(plot)) {
      MessageService.Send(player, "This plot already has a shop!".FormatError());
      return;
    }

    var trader = new TraderModel(player, plot.Position);

    plot.Trader = trader;
    trader.Plot = plot;

    // Align trader with plot rotation
    trader.AlignToPlotRotation(plot.Rotation);

    plot.Hide();

    TraderEntities[trader.Stand] = trader;
    StorageEntities[trader.StorageChest] = trader;
    StandEntities[trader.Trader] = trader;
    TraderById[player.PlatformId] = trader;

    MessageService.Send(player, "Your shop has been created! You can now add items to sell.".FormatSuccess());
    MessageService.Send(player, "Put your first item in the first slot to get started.".FormatSuccess());
  }

  public static bool TryCreatePlot(PlayerData player, out PlotModel plot) {
    if (TryGetPlot(player.Position, out plot)) {
      MessageService.Send(player, "There's already a plot here!".FormatError());
      return false;
    }

    if (WillOverlapWithExistingPlot(player.Position)) {
      MessageService.Send(player, "Can't create a plot here - it would overlap with an existing one!".FormatError());
      return false;
    }

    plot = new PlotModel(player.Position);
    Plots.Add(plot);
    return true;
  }

  public static bool TryRemoveTrader(PlayerData player) {
    if (!TraderById.TryGetValue(player.PlatformId, out var trader)) {
      MessageService.Send(player, "You don't have a shop to remove.".FormatError());
      return false;
    }

    if (trader.IsEmpty()) {
      ForceRemoveTrader(player);
      return true;
    }

    MessageService.Send(player, "Empty your ~shop~ and ~storage~ first before removing it.".FormatError());
    return false;
  }

  public static void ForceRemoveTrader(PlayerData player) {
    if (!TraderById.TryGetValue(player.PlatformId, out var trader)) {
      MessageService.Send(player, "You don't have a shop to remove.".FormatError());
      return;
    }

    trader.Plot.Show();
    trader.Plot.Trader = null;

    // Remove trader from all dictionaries
    TraderEntities.Remove(trader.Stand);
    StorageEntities.Remove(trader.StorageChest);
    StandEntities.Remove(trader.Trader);
    TraderById.Remove(player.PlatformId);

    // Destroy trader entities
    trader.Destroy();

    MessageService.Send(player, "Your shop has been removed.".FormatSuccess());
  }

  public static bool TryRemovePlot(PlotModel plot) {
    if (plot.Trader != null) {
      Log.Error("Cannot remove plot while it has an associated trader.");
      return false;
    }

    ForceRemovePlot(plot);
    return true;
  }

  public static void ForceRemovePlot(PlotModel plot) {
    if (TryGetTraderInPlot(plot, out var trader)) {
      ForceRemoveTrader(trader.Owner);
    }
    plot.Destroy();
    Plots.Remove(plot);
  }

  public static bool IsPlotEmpty(PlotModel plot) {
    return !TryGetTraderInPlot(plot, out _);
  }

  public static TraderModel GetTrader(ulong platformId) {
    if (TraderById.TryGetValue(platformId, out var trader)) {
      return trader;
    }

    return null;
  }

  public static TraderModel GetTrader(Entity entity) {
    if (TraderEntities.TryGetValue(entity, out var trader)) {
      return trader;
    }

    if (StorageEntities.TryGetValue(entity, out trader)) {
      return trader;
    }

    if (StandEntities.TryGetValue(entity, out trader)) {
      return trader;
    }

    return null;
  }

  public static bool TryGetPlot(float3 position, out PlotModel plot) {
    plot = null;

    foreach (var p in Plots) {
      if (p.IsInside(position)) {
        plot = p;
        return true;
      }
    }

    return false;
  }

  public static bool TryGetTraderInPlot(PlotModel plot, out TraderModel trader) {
    trader = null;

    foreach (var traderModel in TraderEntities.Values) {
      if (plot.IsInside(traderModel.Position)) {
        trader = traderModel;
        return true;
      }
    }

    return false;
  }

  public static bool WillOverlapWithExistingPlot(float3 position) {
    foreach (var plot in Plots) {
      if (math.distance(plot.Position, position) < PLOT_RADIUS + plot.Radius) {
        return true;
      }
    }
    return false;
  }

  public static void RegisterOnLoad() {
    RegisterTraders();
    RegisterPlots();
    BindPlotsToTraders();
  }

  public static void RegisterTraders() {
    var query = GameSystems.EntityManager.CreateEntityQuery(
      ComponentType.ReadOnly<NameableInteractable>(),
      ComponentType.ReadOnly<Follower>()
    ).ToEntityArray(Allocator.Temp);

    var traders = new Dictionary<PlayerData, Entity>();
    var storages = new Dictionary<PlayerData, Entity>();
    var stands = new Dictionary<PlayerData, Entity>();
    var coffins = new Dictionary<PlayerData, Entity>();

    foreach (var entity in query) {
      if (!entity.Has<NameableInteractable>() || !entity.Has<Follower>()) continue;

      var owner = entity.Read<Follower>().Followed._Value;
      var playerData = owner.GetPlayerData();

      if (playerData == null) continue;

      if (entity.IdEquals(TRADER_ID)) {
        traders[playerData] = entity;
      } else if (entity.IdEquals(STORAGE_ID)) {
        storages[playerData] = entity;
      } else if (entity.IdEquals(STAND_ID)) {
        stands[playerData] = entity;
      } else if (entity.IdEquals(COFFIN_ID)) {
        coffins[playerData] = entity;
      }
    }

    foreach (var kvp in traders) {
      var player = kvp.Key;
      var traderEntity = kvp.Value;

      if (storages.TryGetValue(player, out var storageEntity) && stands.TryGetValue(player, out var standEntity) && coffins.TryGetValue(player, out var coffinEntity)) {
        var traderModel = new TraderModel(player, storageEntity, standEntity, traderEntity, coffinEntity);

        if (traderModel.IsEmpty()) {
          traderModel.Destroy();
          continue;
        }

        TraderEntities[standEntity] = traderModel;
        StorageEntities[storageEntity] = traderModel;
        StandEntities[traderEntity] = traderModel;
        TraderById[player.PlatformId] = traderModel;
      }
    }
  }

  public static void RegisterPlots() {
    var query = GameSystems.EntityManager.CreateEntityQuery(
      ComponentType.ReadOnly<NameableInteractable>(),
      ComponentType.ReadOnly<DuelInstance>()
    ).ToEntityArray(Allocator.Temp);

    foreach (var entity in query) {
      if (!entity.Has<NameableInteractable>() || !entity.Has<DuelInstance>() || !entity.IdEquals(PLOT_ID)) continue;
      Plots.Add(new PlotModel(entity));
    }
  }

  public static void BindPlotsToTraders() {
    foreach (var plot in Plots) {
      if (!TryGetTraderInPlot(plot, out var trader)) {
        plot.Show();
        continue;
      }
      trader.Plot = plot;
      plot.Trader = trader;
      plot.Hide();
    }
  }

  public static int ClearEmptyTraders() {
    int count = 0;
    foreach (var trader in TraderEntities.Values) {
      if (trader.IsEmpty()) {
        TraderEntities.Remove(trader.Stand);
        StorageEntities.Remove(trader.StorageChest);
        StandEntities.Remove(trader.Trader);
        TraderById.Remove(trader.Owner.PlatformId);
        trader.Destroy();
        count++;
      }
    }
    return count;
  }

  public static int ClearEmptyPlots() {
    int count = 0;
    for (int i = Plots.Count - 1; i >= 0; i--) {
      var plot = Plots[i];
      if (IsPlotEmpty(plot)) {
        plot.Destroy();
        Plots.RemoveAt(i);
        count++;
      }
    }
    return count;
  }

  public static void SetContainerSize(PrefabGUID prefabGUID, int slots) {
    if (!PrefabGuidToEntityMap.TryGetValue(prefabGUID, out var prefabEntity)) {
      Log.Error($"Failed to find prefab for GUID: {prefabGUID.GuidHash}");
      return;
    }

    if (prefabEntity.TryGetBuffer<InventoryInstanceElement>(out var instanceBuffer)) {
      InventoryInstanceElement inventoryInstanceElement = instanceBuffer[0];

      inventoryInstanceElement.RestrictedCategory = (long)ItemCategory.ALL;
      inventoryInstanceElement.Slots = slots;
      inventoryInstanceElement.MaxSlots = slots;

      instanceBuffer[0] = inventoryInstanceElement;
    }
  }

  public static void ClearAll() {
    var query = GameSystems.EntityManager.CreateEntityQuery(
      ComponentType.ReadOnly<NameableInteractable>()
    ).ToEntityArray(Allocator.Temp);

    foreach (var entity in query) {
      if (!entity.Has<NameableInteractable>()) continue;
      if (entity.IdEquals(TRADER_ID) || entity.IdEquals(STORAGE_ID) || entity.IdEquals(COFFIN_ID) || entity.IdEquals(STAND_ID) || entity.IdEquals(PLOT_ID)) {
        entity.Destroy();
      }
    }

    TraderEntities.Clear();
    StorageEntities.Clear();
    StandEntities.Clear();
    TraderById.Clear();
    Plots.Clear();
  }

  public static void SendSucessSCT(PlayerData player, string message) {
    ScrollingCombatTextMessage.Create(
      GameSystems.EntityManager,
      GameSystems.EndSimulationEntityCommandBufferSystem.CreateCommandBuffer(),
      AssetGuid.FromString(message),
      player.Position,
      new float3(1f, 0f, 0f),
      player.CharacterEntity,
      0,
      SCT_PREFAB,
      player.UserEntity
    );
  }

  public static void SendErrorSCT(PlayerData player, string message) {
    ScrollingCombatTextMessage.Create(
      GameSystems.EntityManager,
      GameSystems.EndSimulationEntityCommandBufferSystem.CreateCommandBuffer(),
      AssetGuid.FromString(message),
      player.Position,
      new float3(1f, 0f, 0f),
      player.CharacterEntity,
      0,
      SCT_PREFAB,
      player.UserEntity
    );
  }
}
