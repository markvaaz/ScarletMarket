using System;
using System.Collections.Generic;
using System.Linq;
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
using Unity.Mathematics;

namespace ScarletMarket.Services;

internal static class TraderService {
  private const float ANIMATION_CHECK_INTERVAL = 20f;
  private const int ANIMATION_CHANCE_DENOMINATOR = 3;

  public static readonly Dictionary<Entity, TraderModel> TraderEntities = [];
  public static readonly Dictionary<Entity, TraderModel> StorageEntities = [];
  public static readonly Dictionary<Entity, TraderModel> StandEntities = [];
  public static readonly Dictionary<ulong, TraderModel> TraderById = [];
  public static readonly List<PlotModel> Plots = [];
  private static readonly List<Entity> OrphanCandidates = [];
  public static ActionId RunningAnimationCheck { get; private set; }
  private static Entity _defaultStandEntity;
  private static Settings Settings => Plugin.Settings;
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
    RegisterOnLoad();
    RemoveInactiveTraders();
    ActionScheduler.Repeating(SetRandomAnimation, ANIMATION_CHECK_INTERVAL);
  }

  public static void SetRandomAnimation() {
    foreach (var trader in TraderEntities.Values) {
      if (!trader.Trader.Exists()) continue;

      var randomNumber = UnityEngine.Random.Range(0, ANIMATION_CHANCE_DENOMINATOR);

      if (randomNumber != 0) continue;

      var animation = Animations.GetRandomAnimation();

      Animations.RemoveAnimations(trader.Trader);

      BuffService.TryApplyBuff(trader.Trader, animation.Key, animation.Value);
    }
  }

  public static void RemoveInactiveTraders() {
    if (!Plugin.Settings.Get<bool>("TraderTimeoutEnabled")) {
      return;
    }

    if (Plugin.Settings.Get<bool>("RemoveEmptyTradersOnStartup")) {
      ClearEmptyTraders();
    }

    foreach (var trader in TraderEntities.Values) {
      var player = trader.Owner;
      var lastOnline = player.ConnectedSince;
      var maxDays = Settings.Get<int>("MaxInactiveDays");
      if (lastOnline < DateTime.Now.AddDays(-maxDays)) {
        ForceRemoveTrader(player);
      }
    }
  }

  public static void TryBuyPlot(PlayerData player) {
    if (TraderById.ContainsKey(player.PlatformId)) {
      MessageService.Send(player, "You already have a shop!".FormatError());
      return;
    }

    if (!TryGetPlot(player.Position, out var plot)) {
      MessageService.Send(player, "You need to be inside a plot to create a shop.".FormatError());
      return;
    }

    var requiredPrefabGUID = new PrefabGUID(Settings.Get<int>("PrefabGUID"));
    var requiredAmount = Settings.Get<int>("Amount");

    if (requiredPrefabGUID.GuidHash != 0 && requiredAmount > 0 && !InventoryService.HasAmount(player.CharacterEntity, requiredPrefabGUID, requiredAmount)) {
      var item = ItemSearchService.FindAllByPrefabGUID(requiredPrefabGUID);
      MessageService.Send(player, $"You don't have enough ~{item.Value.Name}~ to claim this plot.".FormatError());
      return;
    }

    CreateTrader(player, plot);
  }

  public static void CreateTrader(PlayerData player, PlotModel plot) {
    if (!IsPlotEmpty(plot)) {
      MessageService.Send(player, "This plot already has a shop!".FormatError());
      return;
    }

    var trader = new TraderModel(player, plot);

    plot.Trader = trader;

    // Align trader with plot rotation
    trader.AlignToPlotRotation();

    plot.Hide();

    // Register trader entities in plot's AttachedBuffer
    if (!plot.Entity.Has<AttachedBuffer>())
      plot.Entity.AddBuffer<AttachedBuffer>();

    var attachedBuffer = plot.Entity.ReadBuffer<AttachedBuffer>();
    attachedBuffer.Add(new AttachedBuffer { Entity = trader.StorageChest });
    attachedBuffer.Add(new AttachedBuffer { Entity = trader.Stand });
    attachedBuffer.Add(new AttachedBuffer { Entity = trader.Trader });
    attachedBuffer.Add(new AttachedBuffer { Entity = trader.Coffin });

    TraderEntities[trader.Trader] = trader;
    StorageEntities[trader.StorageChest] = trader;
    StandEntities[trader.Stand] = trader;
    TraderById[player.PlatformId] = trader;

    MessageService.Send(player, "~Your shop has been created!~ You can now add items to sell.".FormatSuccess());
  }

  public static bool TryCreatePlot(PlayerData player, bool force, out PlotModel plot) {
    if (TryGetPlot(player.Position, out plot)) {
      if (force) MessageService.Send(player, "~Cannot force plot:~ would block access to an existing one.".FormatError());
      else MessageService.Send(player, "There's already a plot here!".FormatError());

      return false;
    }

    if (!force && WillOverlapWithExistingPlot(player.Position)) {
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

    // Clear AttachedBuffer from plot
    if (trader.Plot != null && trader.Plot.Entity.Has<AttachedBuffer>()) {
      var attachedBuffer = trader.Plot.Entity.ReadBuffer<AttachedBuffer>();
      attachedBuffer.Clear();
    }

    trader.Plot.Show();
    trader.Plot.Trader = null;

    // Remove trader from all dictionaries
    TraderEntities.Remove(trader.Trader);
    StorageEntities.Remove(trader.StorageChest);
    StandEntities.Remove(trader.Stand);
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
    return TraderById.TryGetValue(platformId, out var trader) ? trader : null;
  }

  public static TraderModel GetTrader(Entity entity) {
    return TraderEntities.TryGetValue(entity, out var trader) ? trader :
           StorageEntities.TryGetValue(entity, out trader) ? trader :
           StandEntities.TryGetValue(entity, out trader) ? trader : null;
  }

  public static bool TryGetPlot(float3 position, out PlotModel plot, PlotModel exclude = null) {
    plot = null;

    foreach (var p in Plots) {
      if (exclude != null && p == exclude) continue;
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

  public static bool WillOverlapWithExistingPlot(float3 position, PlotModel excludePlot = null) {
    var minDistance = PLOT_RADIUS * 2;
    return Plots.Where(plot => excludePlot == null || plot != excludePlot)
                .Any(plot => math.distance(plot.Position, position) < minDistance);
  }

  public static void RegisterOnLoad() {
    RegisterOnLoadEntities();
    BindPlotsToTraders();
  }

  public static void RegisterOnLoadEntities() {
    EntityQueryBuilder queryBuilder = new(Allocator.Temp);
    queryBuilder.AddAll(ComponentType.ReadOnly<NameableInteractable>());
    queryBuilder.WithOptions(EntityQueryOptions.IncludeDisabled);

    var query = GameSystems.EntityManager.CreateEntityQuery(ref queryBuilder).ToEntityArray(Allocator.Temp);

    var plots = new Dictionary<Entity, Entity>();
    var inspects = new Dictionary<Entity, Entity>();
    var ghostStorages = new Dictionary<Entity, Entity>();
    var ghostTraders = new Dictionary<Entity, Entity>();
    var ghostCoffins = new Dictionary<Entity, Entity>();

    OrphanCandidates.Clear(); // Limpar candidatos anteriores

    foreach (var entity in query) {
      if (!entity.Has<NameableInteractable>()) continue;

      if (entity.IdEquals(Ids.Plot)) {
        plots[entity] = entity;
        continue;
      }

      if (!entity.Has<Follower>()) continue;

      var followed = entity.Read<Follower>().Followed._Value;

      if (entity.IdEquals(Ids.Inspect)) {
        inspects[followed] = entity;
      } else if (entity.IdEquals(Ids.GhostStorage)) {
        ghostStorages[followed] = entity;
      } else if (entity.IdEquals(Ids.GhostTrader)) {
        ghostTraders[followed] = entity;
      } else if (entity.IdEquals(Ids.GhostCoffin)) {
        ghostCoffins[followed] = entity;
      } else if (entity.IdEquals(Ids.Trader) || entity.IdEquals(Ids.Storage) ||
                entity.IdEquals(Ids.Stand) || entity.IdEquals(Ids.Coffin)) {
        OrphanCandidates.Add(entity);
      }
    }

    RegisterPlotsFromEntities(plots, inspects);
    RegisterTradersFromPlots(plots);
    RegisterGhostsFromEntities(ghostStorages, ghostTraders, ghostCoffins);
    RegisterOrphanTraders();
  }

  private static void RegisterPlotsFromEntities(Dictionary<Entity, Entity> plots, Dictionary<Entity, Entity> inspects) {
    foreach (var plotEntity in plots.Keys) {
      var inspectEntity = inspects[plotEntity];
      var plot = new PlotModel(plotEntity, inspectEntity);
      Plots.Add(plot);
    }
  }

  private static void RegisterTradersFromPlots(Dictionary<Entity, Entity> plots) {
    foreach (var plotEntity in plots.Keys) {
      if (!plotEntity.Has<AttachedBuffer>()) continue;

      var attachedBuffer = plotEntity.ReadBuffer<AttachedBuffer>();
      if (attachedBuffer.Length == 0) continue;

      Entity storageEntity = Entity.Null;
      Entity standEntity = Entity.Null;
      Entity traderEntity = Entity.Null;
      Entity coffinEntity = Entity.Null;

      foreach (var attached in attachedBuffer) {
        var entity = attached.Entity;
        if (!entity.Exists()) continue;

        if (entity.IdEquals(Ids.Storage)) {
          storageEntity = entity;
        } else if (entity.IdEquals(Ids.Stand)) {
          standEntity = entity;
        } else if (entity.IdEquals(Ids.Trader)) {
          traderEntity = entity;
        } else if (entity.IdEquals(Ids.Coffin)) {
          coffinEntity = entity;
        }
      }

      if (storageEntity != Entity.Null && storageEntity.Has<Follower>()) {
        var player = storageEntity.Read<Follower>().Followed._Value.GetPlayerData();
        if (player != null) {
          var traderModel = new TraderModel(player, storageEntity, standEntity, traderEntity, coffinEntity);

          TraderEntities[traderModel.Trader] = traderModel;
          StorageEntities[traderModel.StorageChest] = traderModel;
          StandEntities[traderModel.Stand] = traderModel;
          TraderById[player.PlatformId] = traderModel;

          // Remove entidades processadas da lista de órfãos para otimizar performance
          var processedEntities = new[] { storageEntity, standEntity, traderEntity, coffinEntity }
            .Where(e => e != Entity.Null).ToHashSet();
          OrphanCandidates.RemoveAll(processedEntities.Contains);
        }
      }
    }
  }

  private static void RegisterOrphanTraders() {
    var orphanStorages = new Dictionary<PlayerData, Entity>();
    var orphanStands = new Dictionary<PlayerData, Entity>();
    var orphanCoffins = new Dictionary<PlayerData, Entity>();

    foreach (var entity in OrphanCandidates) {
      var playerData = entity.Read<Follower>().Followed._Value.GetPlayerData();
      if (playerData == null) continue;

      // Check if entity is already registered (processed during plot registration)
      if ((entity.IdEquals(Ids.Trader) && TraderEntities.ContainsKey(entity)) ||
          (entity.IdEquals(Ids.Storage) && StorageEntities.ContainsKey(entity)) ||
          (entity.IdEquals(Ids.Stand) && StandEntities.ContainsKey(entity))) {
        continue;
      }

      // Add orphan entities
      if (entity.IdEquals(Ids.Trader)) {
        // Destroy orphaned trader entity to ensure respawn with current prefab
        entity.Destroy();
      } else if (entity.IdEquals(Ids.Storage)) {
        orphanStorages[playerData] = entity;
      } else if (entity.IdEquals(Ids.Stand)) {
        orphanStands[playerData] = entity;
      } else if (entity.IdEquals(Ids.Coffin)) {
        orphanCoffins[playerData] = entity;
      }
    }

    // Register orphan traders
    foreach (var kvp in orphanStorages) {
      var player = kvp.Key;
      if (TraderById.ContainsKey(player.PlatformId)) {
        continue; // Already has a trader registered
      }

      var storageEntity = orphanStorages.GetValueOrDefault(player);
      var standEntity = orphanStands.GetValueOrDefault(player);
      var traderEntity = Entity.Null; // Will be respawned by TraderModel constructor
      var coffinEntity = orphanCoffins.GetValueOrDefault(player);

      // Find which plot contains the majority of these entities
      var entities = new[] { storageEntity, standEntity, coffinEntity }
        .Where(e => e != Entity.Null)
        .ToList();

      var (targetPlot, maxEntitiesInPlot) = Plots
        .Select(plot => new {
          plot,
          count = entities.Count(e => plot.IsInside(e.Position()))
        })
        .Where(x => x.count > 0)
        .OrderByDescending(x => x.count)
        .Select(x => (x.plot, x.count))
        .FirstOrDefault();

      if (targetPlot != null && maxEntitiesInPlot > 0) {
        var traderModel = new TraderModel(player, storageEntity, standEntity, traderEntity, coffinEntity) {
          Plot = targetPlot
        };

        targetPlot.Trader = traderModel;

        // Add entities to plot buffer for future compatibility
        if (!targetPlot.Entity.Has<AttachedBuffer>()) {
          targetPlot.Entity.AddBuffer<AttachedBuffer>();
        }

        var plotBuffer = targetPlot.Entity.ReadBuffer<AttachedBuffer>();

        var entitiesToAdd = new[] { storageEntity, standEntity, coffinEntity, traderModel.Trader }
          .Where(e => e != Entity.Null && !BufferContainsEntity(plotBuffer, e));

        foreach (var entity in entitiesToAdd) {
          plotBuffer.Add(new AttachedBuffer { Entity = entity });
        }

        // Register in dictionaries
        TraderEntities[traderModel.Trader] = traderModel;
        StorageEntities[traderModel.StorageChest] = traderModel;
        StandEntities[traderModel.Stand] = traderModel;
        TraderById[player.PlatformId] = traderModel;
      } else {
        Log.Warning($"Could not find suitable plot for orphan trader of {player.Name}");
      }
    }
  }

  private static bool BufferContainsEntity(DynamicBuffer<AttachedBuffer> buffer, Entity entity) {
    for (int i = 0; i < buffer.Length; i++) {
      if (buffer[i].Entity == entity) {
        return true;
      }
    }
    return false;
  }

  private static void RegisterGhostsFromEntities(Dictionary<Entity, Entity> ghostStorages, Dictionary<Entity, Entity> ghostTraders, Dictionary<Entity, Entity> ghostCoffins) {
    foreach (var plot in Plots) {
      if (!ghostStorages.TryGetValue(plot.Entity, out var storageEntity) ||
          !ghostTraders.TryGetValue(plot.Entity, out var traderEntity) ||
          !ghostCoffins.TryGetValue(plot.Entity, out var coffinEntity)) {
        plot.GhostPlaceholder ??= new GhostTraderModel(plot);
        continue;
      }

      var ghostTrader = new GhostTraderModel(plot, storageEntity, traderEntity, coffinEntity);
      plot.GhostPlaceholder = ghostTrader;
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
      trader.RespawnTrader();
      trader.AlignToPlotRotation();
      plot.Hide();
    }
  }

  public static int ClearEmptyTraders() {
    var tradersToRemove = TraderEntities.Values.Where(trader => trader.IsEmpty()).ToList();

    foreach (var trader in tradersToRemove) {
      TraderEntities.Remove(trader.Trader);
      StorageEntities.Remove(trader.StorageChest);
      StandEntities.Remove(trader.Stand);
      TraderById.Remove(trader.Owner.PlatformId);
      trader.Plot.Show();
      trader.Destroy();
    }

    return tradersToRemove.Count;
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

  public static void ClearAll() {
    EntityQueryBuilder queryBuilder = new(Allocator.Temp);
    queryBuilder.AddAll(ComponentType.ReadOnly<NameableInteractable>());
    queryBuilder.WithOptions(EntityQueryOptions.IncludeDisabled);

    var query = GameSystems.EntityManager.CreateEntityQuery(ref queryBuilder).ToEntityArray(Allocator.Temp);

    foreach (var entity in query) {
      if (!entity.Has<NameableInteractable>()) continue;
      if (
        entity.IdEquals(Ids.Trader) || entity.IdEquals(Ids.Storage) || entity.IdEquals(Ids.Coffin) ||
        entity.IdEquals(Ids.Stand) || entity.IdEquals(Ids.Inspect) || entity.IdEquals(Ids.Plot) ||
        entity.IdEquals(Ids.GhostTrader) || entity.IdEquals(Ids.GhostStorage) || entity.IdEquals(Ids.GhostCoffin)
      ) {
        entity.Destroy();
      }
    }

    TraderEntities.Clear();
    StorageEntities.Clear();
    StandEntities.Clear();
    TraderById.Clear();
    Plots.Clear();
    OrphanCandidates.Clear();
  }

  private static void SendSCT(PlayerData player, string message) {
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

  public static void SendSuccessSCT(PlayerData player, string message) => SendSCT(player, message);

  public static void SendErrorSCT(PlayerData player, string message) => SendSCT(player, message);

  public static void Reload() {
    TraderEntities.Clear();
    StorageEntities.Clear();
    StandEntities.Clear();
    TraderById.Clear();
    Plots.Clear();
    OrphanCandidates.Clear();

    RegisterOnLoad();
  }
}
