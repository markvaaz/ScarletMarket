using ProjectM;
using ProjectM.Tiles;
using ScarletCore;
using ScarletCore.Services;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ScarletMarket.Models;

internal class GhostTraderModel {
  public Entity StorageChest { get; private set; }
  public Entity Trader { get; private set; }
  public Entity Coffin { get; private set; }
  public float3 Position { get; private set; }
  public PlotModel Plot { get; set; } = null;
  public static float3 StorageOffset => TraderModel.StorageOffset;
  public static float3 TraderAndStandOffset => TraderModel.TraderAndStandOffset;
  public static float3 TraderLookAtOffset => TraderModel.TraderLookAtOffset;
  public string Name = "Empty Plot";

  private readonly PrefabGUID[] ServantPermaBuffs = [
    Buffs.Invulnerable,
    Buffs.DisableAggro,
    Buffs.Immaterial,
    Buffs.Ghost
  ];
  public GhostTraderModel(PlotModel plot) {
    Plot = plot;
    Position = plot.Position;
    StorageChest = UnitSpawnerService.ImmediateSpawn(Spawnable.StorageChest, Position + StorageOffset, 0f, 0f, -1f);
    Trader = UnitSpawnerService.ImmediateSpawn(Spawnable.Trader, Position + TraderAndStandOffset, 0f, 0f, -1f);
    Coffin = UnitSpawnerService.ImmediateSpawn(Spawnable.Coffin, Position + new float3(0, COFFIN_HEIGHT, 0), 0f, 0f, -1f);

    SetupGhostCoffin();
    SetupGhostStorageChest();
    SetupGhostTrader();
    BindCoffinServant();
    AlignToPlotRotation();
  }

  public GhostTraderModel(PlotModel plot, Entity storageChest, Entity trader, Entity coffin) {
    Plot = plot;
    Position = plot.Position;
    StorageChest = storageChest;
    Trader = trader;
    Coffin = coffin;
    RespawnTrader();
    AlignToPlotRotation();
  }

  public void Destroy() {
    if (StorageChest != Entity.Null && StorageChest.Exists()) {
      StorageChest.Destroy();
    }
    if (Trader != Entity.Null && Trader.Exists()) {
      Trader.Destroy();
    }
    if (Coffin != Entity.Null && Coffin.Exists()) {
      Coffin.Destroy();
    }
  }

  public void Show() {
    if (StorageChest != Entity.Null && StorageChest.Exists() && BuffService.HasBuff(StorageChest, Buffs.Invisibility)) {
      BuffService.TryRemoveBuff(StorageChest, Buffs.Invisibility);
    }
    if (Trader != Entity.Null && Trader.Exists() && BuffService.HasBuff(Trader, Buffs.Invisibility)) {
      BuffService.TryRemoveBuff(Trader, Buffs.Invisibility);
    }
    if (Coffin != Entity.Null && Coffin.Exists() && BuffService.HasBuff(Coffin, Buffs.Invisibility)) {
      BuffService.TryRemoveBuff(Coffin, Buffs.Invisibility);
    }
  }

  public void Hide() {
    if (StorageChest != Entity.Null && StorageChest.Exists() && !BuffService.HasBuff(StorageChest, Buffs.Invisibility)) {
      BuffService.TryApplyBuff(StorageChest, Buffs.Invisibility, -1);
    }
    if (Trader != Entity.Null && Trader.Exists() && !BuffService.HasBuff(Trader, Buffs.Invisibility)) {
      BuffService.TryApplyBuff(Trader, Buffs.Invisibility, -1);
    }
    if (Coffin != Entity.Null && Coffin.Exists() && !BuffService.HasBuff(Coffin, Buffs.Invisibility)) {
      BuffService.TryApplyBuff(Coffin, Buffs.Invisibility, -1);
    }
  }

  private void Attach(Entity entity) {
    // Using Follower to mantain the consistency for getting the owner of any of the entities
    // since EntityOwner can not be added to the Stand and Attach makes the servant freak out
    entity.AddWith((ref Follower follower) => {
      follower.Followed._Value = Plot.Entity;
    });
  }

  private void SetupGhostCoffin() {
    Coffin.SetId(Ids.GhostCoffin);
    Attach(Coffin);
    foreach (var permaBuffGuid in ServantPermaBuffs) {
      BuffService.TryApplyBuff(Coffin, permaBuffGuid, -1);
    }
    DisableInteraction(Coffin);
  }

  private void SetupGhostStorageChest() {
    StorageChest.SetId(Ids.GhostStorage);
    Attach(StorageChest);

    StorageChest.Remove<DestroyWhenInventoryIsEmpty>();
    StorageChest.Remove<ShrinkInventoryWhenWithdrawn>();

    StorageChest.With((ref EditableTileModel editableTileModel) => {
      editableTileModel.CanDismantle = false;
      editableTileModel.CanMoveAfterBuild = false;
      editableTileModel.CanRotateAfterBuild = false;
    });

    foreach (var permaBuffGuid in ServantPermaBuffs) {
      BuffService.TryApplyBuff(StorageChest, permaBuffGuid, -1);
    }
    DisableInteraction(StorageChest);
  }

  private void SetupGhostTrader() {
    Trader.SetId(Ids.GhostTrader);
    Attach(Trader);

    Trader.With((ref EntityInput lookAtTarget) => {
      lookAtTarget.SetAllAimPositions(Position + TraderLookAtOffset);
    });

    Trader.With((ref AggroConsumer aggroConsumer) => {
      aggroConsumer.Active._Value = false;
    });

    Trader.With((ref Aggroable aggroable) => {
      aggroable.Value._Value = false;
      aggroable.DistanceFactor._Value = 0f;
      aggroable.AggroFactor._Value = 0f;
    });

    Trader.With((ref FactionReference factionReference) => {
      factionReference.FactionGuid._Value = NEUTRAL_FACTION;
    });

    foreach (var permaBuffGuid in ServantPermaBuffs) {
      BuffService.TryApplyBuff(Trader, permaBuffGuid, -1);
    }

    DisableInteraction(Trader);
  }

  private void BindCoffinServant() {
    Coffin.AddWith((ref ServantCoffinstation coffinStation) => {
      coffinStation.ConnectedServant = NetworkedEntity.ServerEntity(Trader);
      coffinStation.ServantName = new FixedString64Bytes(Name);
      coffinStation.ServantGearLevel = 100;
      coffinStation.State = ServantCoffinState.ServantAlive;
    });

    Trader.AddWith((ref ServantConnectedCoffin servantConnectedCoffin) => {
      servantConnectedCoffin.CoffinEntity = NetworkedEntity.ServerEntity(Coffin);
    });
  }

  private void UnbindCoffinServant() {
    Trader.With((ref ServantConnectedCoffin servantConnectedCoffin) => {
      servantConnectedCoffin.CoffinEntity = NetworkedEntity.ServerEntity(Entity.Null);
    });

    Coffin.With((ref ServantCoffinstation coffinStation) => {
      coffinStation.ConnectedServant = NetworkedEntity.ServerEntity(Entity.Null);
      coffinStation.State = ServantCoffinState.Empty;
    });
  }

  private void DisableInteraction(Entity entity) {
    entity.HasWith((ref Interactable interactable) => {
      interactable.Disabled = true;
    });
  }

  public void AlignToPlotRotation() {
    var center = Plot.Position;
    var plotRotation = Plot.Rotation;

    var quaternions = new quaternion[] {
      quaternion.identity,
      quaternion.RotateY(math.radians(90f)),
      quaternion.RotateY(math.radians(180f)),
      quaternion.RotateY(math.radians(270f))
    };

    var forward = math.mul(plotRotation, new float3(0, 0, 1));
    var threshold = 0.4f;

    int rotationStep = 0;
    if (forward.z > threshold) rotationStep = 0;
    else if (forward.x > threshold) rotationStep = 1;
    else if (forward.z < -threshold) rotationStep = 2;
    else if (forward.x < -threshold) rotationStep = 3;

    var targetRotation = quaternions[rotationStep];
    int storageRotationStep = (rotationStep + TraderModel.StorageRotationOffset) % 4;
    var storageRotation = quaternions[storageRotationStep];
    var rotatedStoragePos = center + math.mul(storageRotation, StorageOffset);
    var rotatedStandPos = center + math.mul(targetRotation, TraderAndStandOffset);
    var rotatedTraderPos = center + math.mul(targetRotation, TraderAndStandOffset);

    if (StorageChest.Exists()) {
      StorageChest.SetPosition(rotatedStoragePos);
      RotateTile(StorageChest, storageRotationStep);
    }

    if (Trader.Exists()) {
      Trader.SetPosition(rotatedTraderPos);
      var lookAtDirection = math.mul(targetRotation, new float3(0, 0, 1));
      var lookAtTarget = rotatedTraderPos + lookAtDirection;

      Trader.With((ref EntityInput lookAtInput) => {
        lookAtInput.SetAllAimPositions(lookAtTarget);
      });
    }
  }

  private void RotateTile(Entity tileEntity, int rotationStep) {
    TraderModel.RotateTile(tileEntity, rotationStep);
  }

  public void RespawnTrader() {
    if (Trader != Entity.Null && Trader.Exists()) {
      UnbindCoffinServant();
      Trader.Destroy();
    }

    var center = Plot?.Position ?? Position;
    var targetPos = center + TraderAndStandOffset;
    Trader = UnitSpawnerService.ImmediateSpawn(Spawnable.Trader, targetPos, 0f, 0f, -1f);

    SetupGhostTrader();
    BindCoffinServant();
    AlignToPlotRotation();
  }
}
