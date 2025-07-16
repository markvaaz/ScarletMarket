using ProjectM;
using ProjectM.Tiles;
using ScarletCore;
using ScarletCore.Services;
using ScarletCore.Utils;
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
    StorageChest = UnitSpawnerService.ImmediateSpawn(Spawnable.StorageChest, Position + new float3(0, 0, -1f), 0f, 0f, -1f);
    Trader = UnitSpawnerService.ImmediateSpawn(Spawnable.Trader, Position + new float3(0, 0, 1.5f), 0f, 0f, -1f);
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
      BuffService.TryApplyBuff(StorageChest, Buffs.Invisibility);
    }
    if (Trader != Entity.Null && Trader.Exists() && !BuffService.HasBuff(Trader, Buffs.Invisibility)) {
      BuffService.TryApplyBuff(Trader, Buffs.Invisibility);
    }
    if (Coffin != Entity.Null && Coffin.Exists() && !BuffService.HasBuff(Coffin, Buffs.Invisibility)) {
      BuffService.TryApplyBuff(Coffin, Buffs.Invisibility);
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

  public void AlignToPlotRotation() {
    var center = Plot.Position;
    var plotRotation = Plot.Rotation;

    // Definir quaternions para cada direção cardeal
    var quaternions = new quaternion[] {
      quaternion.identity,                          // Norte (0°)
      quaternion.RotateY(math.radians(90f)),        // Leste (90°)
      quaternion.RotateY(math.radians(180f)),       // Sul (180°)
      quaternion.RotateY(math.radians(270f))        // Oeste (270°)
    };

    // Determinar qual direção cardeal baseado no forward do plot
    var forward = math.mul(plotRotation, new float3(0, 0, 1));
    var threshold = 0.4f;

    int rotationStep = 0;
    if (forward.z > threshold) rotationStep = 0;       // Norte
    else if (forward.x > threshold) rotationStep = 1;  // Leste
    else if (forward.z < -threshold) rotationStep = 2; // Sul
    else if (forward.x < -threshold) rotationStep = 3; // Oeste

    var targetRotation = quaternions[rotationStep];

    // Posições relativas ao centro (antes da rotação)
    var storageOffset = new float3(0, 0, -1f);   // Atrás do centro
    var standOffset = new float3(0, 0, 1.5f);    // Na frente do centro
    var traderOffset = new float3(0, 0, 1.5f);   // Na frente do centro

    // Aplicar rotação orbital às posições
    var rotatedStoragePos = center + math.mul(targetRotation, storageOffset);
    var rotatedStandPos = center + math.mul(targetRotation, standOffset);
    var rotatedTraderPos = center + math.mul(targetRotation, traderOffset);

    // Mover e rotacionar StorageChest (tile)
    if (StorageChest.Exists()) {
      StorageChest.SetPosition(rotatedStoragePos);
      RotateTile(StorageChest, rotationStep);
    }

    // Mover e rotacionar Trader (unidade)
    if (Trader.Exists()) {
      Trader.SetPosition(rotatedTraderPos);
      // Fazer o trader "olhar" para a direção cardeal
      var lookAtDirection = math.mul(targetRotation, new float3(0, 0, 1));
      var lookAtTarget = rotatedTraderPos + lookAtDirection;

      Trader.With((ref EntityInput lookAtInput) => {
        lookAtInput.SetAllAimPositions(lookAtTarget);
      });
    }
  }

  private void RotateTile(Entity tileEntity, int rotationStep) {
    // rotationStep: 0=Norte, 1=Leste, 2=Sul, 3=Oeste

    // Enum values baseado na análise dos dados
    var tileRotations = new[] {
      TileRotation.None,           // Norte (0°)
      TileRotation.Clockwise_90,   // Leste (90°)
      TileRotation.Clockwise_180,  // Sul (180°)
      TileRotation.Clockwise_270   // Oeste (270°)
    };

    // Quaternions para cada direção
    var quaternions = new quaternion[] {
      quaternion.identity,                          // Norte (0°)
      quaternion.RotateY(math.radians(90f)),        // Leste (90°)
      quaternion.RotateY(math.radians(180f)),       // Sul (180°)
      quaternion.RotateY(math.radians(270f))        // Oeste (270°)
    };

    var tileRotation = tileRotations[rotationStep];
    var newRotation = quaternions[rotationStep];
    var tilePosition = tileEntity.Read<TilePosition>();

    // 1. Atualizar TilePosition

    tileEntity.With((ref TilePosition tilePos) => {
      tilePos.TileRotation = tileRotation;
    });


    // 2. Atualizar TileModelSpatialData

    tileEntity.With((ref TileModelSpatialData spatialData) => {
      spatialData.LastTilePosition = tilePosition;
    });


    // 3. Atualizar StaticTransformCompatible

    tileEntity.With((ref StaticTransformCompatible compatible) => {
      compatible.NonStaticTransform_Rotation = tileRotation;
    });


    // 4. Atualizar Rotation

    tileEntity.Write(new Rotation { Value = newRotation });


    // 5. Atualizar LocalTransform

    tileEntity.With((ref LocalTransform localTransform) => {
      localTransform.Rotation = newRotation;
    });


    // 6. Atualizar LocalToWorld

    tileEntity.With((ref LocalToWorld localToWorld) => {
      // Calcular Right e Forward baseados na rotação
      var right = math.mul(newRotation, new float3(1f, 0f, 0f));
      var forward = math.mul(newRotation, new float3(0f, 0f, 1f));
      var up = new float3(0f, 1f, 0f);  // Up sempre para cima

      // Obter posição atual
      var currentPosition = localToWorld.Position;

      // Criar matrix de transformação no formato correto
      localToWorld.Value = new float4x4(
        new float4(right.x, up.x, forward.x, currentPosition.x),
        new float4(right.y, up.y, forward.y, currentPosition.y),
        new float4(right.z, up.z, forward.z, currentPosition.z),
        new float4(0f, 0f, 0f, 1f)
      );
    });
  }
}
