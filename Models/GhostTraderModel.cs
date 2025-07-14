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
    StorageChest = UnitSpawnerService.ImmediateSpawn(Spawnable.StorageChest, Position + new float3(0, 0, -0.65f), 0f, 0f, -1f);
    Trader = UnitSpawnerService.ImmediateSpawn(Spawnable.Trader, Position + new float3(0, 0, 0.65f), 0f, 0f, -1f);
    Coffin = UnitSpawnerService.ImmediateSpawn(Spawnable.Coffin, Position + new float3(0, COFFIN_HEIGHT, 0), 0f, 0f, -1f);

    SetupGhostCoffin();
    SetupGhostStorageChest();
    SetupGhostTrader();
    BindCoffinServant();
  }

  // For loading existing ghost entities from save
  public GhostTraderModel(PlotModel plot, Entity storageChest, Entity trader, Entity coffin) {
    Plot = plot;
    Position = plot.Position;
    StorageChest = storageChest;
    Trader = trader;
    Coffin = coffin;
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
    if (StorageChest != Entity.Null && StorageChest.Exists()) {
      BuffService.TryRemoveBuff(StorageChest, Buffs.Invisibility);
    }
    if (Trader != Entity.Null && Trader.Exists()) {
      BuffService.TryRemoveBuff(Trader, Buffs.Invisibility);
    }
    if (Coffin != Entity.Null && Coffin.Exists()) {
      BuffService.TryRemoveBuff(Coffin, Buffs.Invisibility);
    }
  }

  public void Hide() {
    if (StorageChest != Entity.Null && StorageChest.Exists()) {
      BuffService.TryApplyBuff(StorageChest, Buffs.Invisibility);
    }
    if (Trader != Entity.Null && Trader.Exists()) {
      BuffService.TryApplyBuff(Trader, Buffs.Invisibility);
    }
    if (Coffin != Entity.Null && Coffin.Exists()) {
      BuffService.TryApplyBuff(Coffin, Buffs.Invisibility);
    }
  }

  public void AlignToPlotRotation(quaternion rotation) {
    var forward = math.mul(rotation, new float3(0, 0, 1));
    var threshold = 0.4f;

    int plotRotationStep = 0;
    if (forward.z > threshold) plotRotationStep = 0;
    else if (forward.x < -threshold) plotRotationStep = 1;
    else if (forward.z < -threshold) plotRotationStep = 2;
    else if (forward.x > threshold) plotRotationStep = 3;

    var currentRotation = CalculateCurrentRotation();
    var rotationsNeeded = (plotRotationStep - currentRotation + 4) % 4;

    for (int i = 0; i < rotationsNeeded; i++) {
      RotateGhost();
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
    Coffin.SetId(GHOST_COFFIN_ID);
    Attach(Coffin);
    foreach (var permaBuffGuid in ServantPermaBuffs) {
      BuffService.TryApplyBuff(Coffin, permaBuffGuid);
    }
    DisableInteraction(Coffin);
  }

  private void SetupGhostStorageChest() {
    StorageChest.SetId(GHOST_STORAGE_ID);
    Attach(StorageChest);

    StorageChest.Remove<DestroyWhenInventoryIsEmpty>();
    StorageChest.Remove<ShrinkInventoryWhenWithdrawn>();

    StorageChest.With((ref EditableTileModel editableTileModel) => {
      editableTileModel.CanDismantle = false;
      editableTileModel.CanMoveAfterBuild = false;
      editableTileModel.CanRotateAfterBuild = false;
    });

    foreach (var permaBuffGuid in ServantPermaBuffs) {
      BuffService.TryApplyBuff(StorageChest, permaBuffGuid);
    }
    DisableInteraction(StorageChest);
  }

  private void SetupGhostTrader() {
    Trader.SetId(GHOST_TRADER_ID);
    Attach(Trader);

    Trader.With((ref EntityInput lookAtTarget) => {
      lookAtTarget.SetAllAimPositions(Position + new float3(0, 0, 1f));
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
      BuffService.TryApplyBuff(Trader, permaBuffGuid);
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

  private void DisableInteraction(Entity entity) {
    entity.HasWith((ref Interactable interactable) => {
      interactable.Disabled = true;
    });
  }

  private void RotateGhost() {
    RotateTile(StorageChest, Position);
    RotateTraderUnit(Position);
    UpdateTraderLookDirection();
  }

  private void RotateTile(Entity tileEntity, float3 centerPoint) {
    if (tileEntity.IsNull() || !tileEntity.Exists()) return;

    var currentPos = tileEntity.Position();
    var relativePos = currentPos - centerPoint;

    var rotatedRelativePos = new float3(
      -relativePos.z,
      relativePos.y,
      relativePos.x
    );

    var newPos = centerPoint + rotatedRelativePos;
    tileEntity.SetPosition(newPos);

    if (tileEntity.Has<TilePosition>()) {
      var currentRotation = CalculateCurrentRotation();
      var tilePos = tileEntity.Read<TilePosition>();
      tilePos.TileRotation = (TileRotation)currentRotation;
      tileEntity.Write(tilePos);

      if (tileEntity.Has<StaticTransformCompatible>()) {
        var stc = tileEntity.Read<StaticTransformCompatible>();
        stc.NonStaticTransform_Rotation = tilePos.TileRotation;
        tileEntity.Write(stc);
      }

      tileEntity.Write(new Rotation { Value = quaternion.RotateY(math.radians(-90 * currentRotation)) });
    }
  }

  private void RotateTraderUnit(float3 centerPoint) {
    if (Trader.IsNull() || !Trader.Exists()) return;

    var currentPos = Trader.Position();
    var relativePos = currentPos - centerPoint;

    var rotatedRelativePos = new float3(
      -relativePos.z,
      relativePos.y,
      relativePos.x
    );

    var newPos = centerPoint + rotatedRelativePos;
    Trader.SetPosition(newPos);

    var currentRotation = CalculateCurrentRotation();
    var rotation = quaternion.RotateY(math.radians(-90 * currentRotation));
    Trader.Write(new Rotation { Value = rotation });
  }

  private void UpdateTraderLookDirection() {
    if (Trader.IsNull() || !Trader.Exists()) return;

    var traderPos = Trader.Position();
    var currentRotation = CalculateCurrentRotation();

    var lookOffset = new float3(0, 0, 1f);

    switch (currentRotation) {
      case 0:
        lookOffset = new float3(0, 0, 1f);
        break;
      case 1:
        lookOffset = new float3(-1f, 0, 0);
        break;
      case 2:
        lookOffset = new float3(0, 0, -1f);
        break;
      case 3:
        lookOffset = new float3(1f, 0, 0);
        break;
    }

    var lookAtPosition = traderPos + lookOffset;

    if (Trader.Has<EntityInput>()) {
      Trader.With((ref EntityInput lookAtTarget) => {
        lookAtTarget.SetAllAimPositions(lookAtPosition);
      });
    }
  }

  private int CalculateCurrentRotation() {
    if (StorageChest.IsNull() || !StorageChest.Exists()) return 0;

    var centerPos = Position;
    var chestPos = StorageChest.Position();
    var relativePos = chestPos - centerPos;

    var threshold = 0.4f;

    if (relativePos.z < -threshold) return 0;
    if (relativePos.x > threshold) return 1;
    if (relativePos.z > threshold) return 2;
    if (relativePos.x < -threshold) return 3;

    return 0;
  }
}
