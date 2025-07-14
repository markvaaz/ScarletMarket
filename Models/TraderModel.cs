using System;
using System.Collections.Generic;
using ProjectM;
using ProjectM.Network;
using ProjectM.Tiles;
using ScarletCore;
using ScarletCore.Data;
using ScarletCore.Services;
using ScarletCore.Systems;
using ScarletCore.Utils;
using ScarletMarket.Services;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ScarletMarket.Models;

internal class TraderModel {
  public string Name { get; private set; }
  public Entity StorageChest { get; private set; }
  public Entity Stand { get; private set; }
  public Entity Trader { get; private set; }
  public Entity Coffin { get; private set; }
  public Entity DefaultStandEntity => TraderService.DefaultStandEntity;
  public PlayerData Owner { get; private set; }
  public PrefabGUID State { get; private set; }
  public float3 Position { get; private set; }
  public PlotModel Plot { get; set; } = null;
  public List<int> BlockedSlots = [];
  private readonly PrefabGUID[] ServantPermaBuffs = [
    Buffs.Invulnerable,
    Buffs.DisableAggro,
    Buffs.Immaterial
  ];

  // For creating new ones
  public TraderModel(PlayerData player, float3 position) {
    Owner = player;
    Name = $"{Owner.Name}'s Shop";
    Position = position;
    StorageChest = UnitSpawnerService.ImmediateSpawn(Spawnable.StorageChest, Position + new float3(0, 0, -0.65f), 0f, 0f, -1f, Owner.UserEntity);
    Stand = UnitSpawnerService.ImmediateSpawn(Spawnable.StandChest, Position + new float3(0, 0, 0.65f), 0f, 0f, -1f);
    Trader = UnitSpawnerService.ImmediateSpawn(Spawnable.Trader, Position + new float3(0, 0, 0.65f), 0f, 0f, -1f, Owner.UserEntity);
    Coffin = UnitSpawnerService.ImmediateSpawn(Spawnable.Coffin, Position + new float3(0, COFFIN_HEIGHT, 0), 0f, 0f, -1f, Owner.UserEntity);
    SetState(TraderState.WaitingForItem);
    SetupCoffin();
    SetupStorageChest();
    SetupTrader();
    SetupStand();
    BindCoffinServant();
  }

  // For loading existing ones
  public TraderModel(PlayerData player, Entity storageChest, Entity stand, Entity trader, Entity coffin) {
    Owner = player;
    Name = $"{Owner.Name}'s Shop";
    StorageChest = storageChest;
    Stand = stand;
    Trader = trader;
    Coffin = coffin;
    Position = GetCenterPosition();
    SetState(GetCurrentState());
  }

  public void Destroy() {
    if (StorageChest != Entity.Null && StorageChest.Exists()) {
      StorageChest.Destroy();
    }
    if (Stand != Entity.Null && Stand.Exists()) {
      Stand.Destroy();
    }
    if (Trader != Entity.Null && Trader.Exists()) {
      Trader.Destroy();
    }
    if (Coffin != Entity.Null && Coffin.Exists()) {
      Coffin.Destroy();
    }
  }

  public void SetState(PrefabGUID newState) {
    if (!TraderState.IsValid(newState)) {
      Log.Error($"Invalid trader state: {newState}");
      return;
    }

    ClearAllStateBuffs();

    BuffService.TryApplyBuff(Trader, newState);

    State = newState;

    if (State != TraderState.Ready) {
      MakeStandPrivate();
      BuffService.TryApplyBuff(Trader, Buffs.ClosedVisualClue);
      SetTraderName($"{Owner.Name}'s Shop (Closed)");
    } else {
      MakeStandPublic();
      SetTraderName($"{Owner.Name}'s Shop");
      BuffService.TryRemoveBuff(Trader, Buffs.ClosedVisualClue);
    }
  }

  public PrefabGUID GetCurrentState() {
    // Check buffs to determine current state
    if (BuffService.HasBuff(Trader, TraderState.WaitingForCost)) {
      return TraderState.WaitingForCost;
    }

    if (BuffService.HasBuff(Trader, TraderState.ReceivedCost)) {
      return TraderState.ReceivedCost;
    }

    if (BuffService.HasBuff(Trader, TraderState.Ready)) {
      return TraderState.Ready;
    }

    // Fallback to inventory-based state determination
    return DetermineStateFromInventory();
  }

  private PrefabGUID DetermineStateFromInventory() {
    if (HasItemWithNoCost()) {
      return TraderState.WaitingForCost;
    }

    if (HasAnyValidTradePairs()) {
      return TraderState.Ready;
    }

    return TraderState.WaitingForItem;
  }

  public bool HasItemWithNoCost() {
    var itemWithNoCost = GetItemWithNoCost();
    return itemWithNoCost.Key.ItemType.GuidHash != 0;
  }

  public bool HasAnyValidTradePairs() {
    // Check slots 0-6 and their corresponding cost slots 7-13
    for (int i = 0; i < 7; i++) {
      if (TryGetItemAtSlot(Stand, i, out _) && TryGetItemAtSlot(Stand, i + 7, out _)) {
        return true;
      }
    }

    // Check slots 21-27 and their corresponding cost slots 28-34
    for (int i = 21; i < 28; i++) {
      if (TryGetItemAtSlot(Stand, i, out _) && TryGetItemAtSlot(Stand, i + 7, out _)) {
        return true;
      }
    }

    return false;
  }

  private void ClearAllStateBuffs() {
    BuffService.TryRemoveBuff(Trader, TraderState.WaitingForItem);
    BuffService.TryRemoveBuff(Trader, TraderState.WaitingForCost);
    BuffService.TryRemoveBuff(Trader, TraderState.ReceivedCost);
    BuffService.TryRemoveBuff(Trader, TraderState.Ready);
  }

  public void SetAsReady() {
    if (State == TraderState.Ready) {
      SendErrorSCT(ALREADY_ASSIGNED_MESSAGE);
      return;
    }

    var hasAnyValidTradePairs = HasAnyValidTradePairs();
    var hasItemWithNoCost = HasItemWithNoCost();

    if (hasAnyValidTradePairs && !hasItemWithNoCost) {
      SendSucessSCT(OPEN_MESSAGE);
      SetState(TraderState.Ready);
      return;
    }

    if (hasItemWithNoCost) {
      SendErrorSCT(CANNOT_DO_MESSAGE);
      SendMessage(Owner, "Set a price for your item before opening the shop.");
      return;
    }

    if (!hasAnyValidTradePairs) {
      SendErrorSCT(CANNOT_DO_MESSAGE);
      SendMessage("Add an item to your shop first!");
      return;
    }
  }

  public void SetAsNotReady() {
    if (State == TraderState.WaitingForItem || State == TraderState.WaitingForCost) {
      SendErrorSCT(ALREADY_ASSIGNED_MESSAGE);
      return;
    }
    SendSucessSCT(CLOSE_MESSAGE);
    SetState(TraderState.WaitingForItem);
  }

  public void HandleAddingItem() {
    SetState(TraderState.WaitingForCost);
    ForceAllSlotsMaxAmount();
    SendMessage("Use \".market addcost (itemName) (amount)\" to set the price.");
  }

  public void SendMessage(string messageText) {
    SendMessage(Owner, messageText);
  }

  public void SendMessage(PlayerData playerData, string messageText) {
    var toUser = playerData.User.Index;
    var message = new FixedString512Bytes(messageText);
    var fromUser = Trader.Read<NetworkId>();
    var fromCharacter = Trader.Read<NetworkId>();

    ServerChatUtils.SendChatMessage(
      GameSystems.EntityManager,
      ref toUser,
      ref message,
      ref fromUser,
      ref fromCharacter,
      ServerChatMessageType.Local,
      DateTime.UtcNow.Ticks
    );
  }

  public void RemoveCostItem(int slot) {
    if (State == TraderState.WaitingForCost)
      SetState(TraderState.WaitingForItem);

    RemoveItemOnSlot(slot);
  }

  public void RemoveCostWithoutItem() {
    var noItemCost = GetCostWithoutItem();
    var slot = noItemCost.Value;

    RemoveItemOnSlot(slot);

    Log.Info(HasAnyValidTradePairs());

    if (!HasAnyValidTradePairs()) SetState(TraderState.WaitingForItem);
  }

  public void RemoveItemOnSlot(int slot) {
    if (!TryGetItemAtSlot(Stand, slot, out var item) || (slot >= 14 && slot < 21)) return;

    InventoryUtilitiesServer.TryRemoveItemAtIndex(GameSystems.EntityManager, Stand, item.ItemType, item.Amount, slot, true);
  }

  public bool IsEmpty() {
    for (int i = 0; i < 7; i++) {
      if (TryGetItemAtSlot(Stand, i, out _)) {
        return false;
      }
    }

    for (int i = 21; i < 28; i++) {
      if (TryGetItemAtSlot(Stand, i, out _)) {
        return false;
      }
    }

    if (!InventoryUtilities.IsInventoryEmpty(GameSystems.EntityManager, StorageChest)) {
      return false;
    }

    return true;
  }

  public bool TryAddCostItem(PrefabGUID costItem, int costAmount) {
    var noCostItem = GetItemWithNoCost();
    var itemPrefab = noCostItem.Key.ItemType;

    if (costItem == itemPrefab) {
      return false;
    }

    var slot = noCostItem.Value;
    SetState(TraderState.ReceivedCost);
    AddWithMaxAmount(Stand, slot + 7, costItem, costAmount, costAmount);
    ForceAllSlotsMaxAmount();

    return true;
  }

  public bool TryBuyItem(PlayerData player, int slot) {
    if (!IsValidSlot(slot)) {
      SendMessage(player, "This item isn't for sale.");
      return false;
    }

    if (!TryGetItemAtSlot(Stand, slot, out var item)) {
      SendMessage(player, "Couldn't find that item in the shop.");
      return false;
    }

    if (!TryGetItemAtSlot(Stand, slot + 7, out var costItem)) {
      SendMessage(player, "This item doesn't have a price set.");
      return false;
    }

    // Get item names for better messages with fallback
    var itemResult = ItemSearchService.FindByPrefabGUID(item.ItemType);
    var costItemResult = ItemSearchService.FindByPrefabGUID(costItem.ItemType);

    // Extract names or use fallback
    var itemName = itemResult?.Name ?? "item";
    var costItemName = costItemResult?.Name ?? "items";

    if (!InventoryService.HasAmount(player.CharacterEntity, costItem.ItemType, costItem.Amount)) {
      if (itemResult == null || costItemResult == null) {
        SendMessage(player, "You don't have enough resources to buy this item.");
      } else {
        SendMessage(player, $"You need {costItem.Amount}x {costItemName} to buy this {itemName}.");
      }
      return false;
    }

    // Execute the trade
    InventoryService.RemoveItem(player.CharacterEntity, costItem.ItemType, costItem.Amount);
    InventoryService.AddItem(StorageChest, costItem.ItemType, costItem.Amount);
    RemoveCostItem(slot + 7);

    // Success messages with fallback for missing names
    if (itemResult == null || costItemResult == null) {
      MessageService.Send(player, "Purchase successful!".FormatSuccess());
      MessageService.Send(Owner, $"~{player.Name}~ bought something from your shop.".FormatSuccess());
    } else {
      MessageService.Send(player, $"Successfully bought ~{itemName}~ for ~{costItem.Amount}x {costItemName}~!".FormatSuccess());
      MessageService.Send(Owner, $"~{player.Name}~ bought ~{itemName}~ from your shop for ~{costItem.Amount}x {costItemName}~.".FormatSuccess());
    }

    return true;
  }

  public void ForceAllSlotsMaxAmount() {
    var items = InventoryService.GetInventoryItems(Stand);
    for (int i = 0; i < items.Length; i++) {
      var existingItem = items[i];
      existingItem.MaxAmountOverride = existingItem.Amount;
      items[i] = existingItem;
    }
  }

  public KeyValuePair<InventoryBuffer, int> GetItemWithNoCost() {
    for (int i = 0; i < 7; i++) {
      if (TryGetItemAtSlot(Stand, i, out var item)) {
        if (!TryGetItemAtSlot(Stand, i + 7, out _)) return new(item, i);
      }
    }

    for (int i = 21; i < 28; i++) {
      if (TryGetItemAtSlot(Stand, i, out var item)) {
        if (!TryGetItemAtSlot(Stand, i + 7, out _)) return new(item, i);
      }
    }

    return default;
  }

  public KeyValuePair<InventoryBuffer, int> GetCostWithoutItem() {
    for (int i = 7; i < 14; i++) {
      if (TryGetItemAtSlot(Stand, i, out var item)) {
        if (!TryGetItemAtSlot(Stand, i - 7, out _)) return new(item, i);
      }
    }

    for (int i = 28; i < 35; i++) {
      if (TryGetItemAtSlot(Stand, i, out var item)) {
        if (!TryGetItemAtSlot(Stand, i - 7, out _)) return new(item, i);
      }
    }

    return default;
  }

  public static bool IsValidSlot(int slot) {
    if (slot < 0 || slot >= 35) {
      return false;
    }

    if ((slot >= 0 && slot < 7) || (slot >= 21 && slot < 28)) {
      return true;
    }

    return false;
  }

  public static bool TryGetItemAtSlot(Entity entity, int slot, out InventoryBuffer item) {
    if (InventoryUtilities.TryGetItemAtSlot(GameSystems.EntityManager, entity, slot, out item)) {
      return true;
    }
    return false;
  }

  public void AddWithMaxAmount(Entity entity, int slot, PrefabGUID prefabGUID, int amount, int maxAmount) {
    var response = GameSystems.ServerGameManager.TryAddInventoryItem(entity, prefabGUID, 1, new(slot), false);
    var slotIndex = response.Slot;
    var items = InventoryService.GetInventoryItems(entity);
    var item = items[slotIndex];
    item.MaxAmountOverride = maxAmount;
    item.Amount = amount;
    items[slotIndex] = item;
  }

  public void BlockSlots(Entity entity, int startSlot, int endSlot) {
    for (int i = startSlot; i < endSlot; i++) {
      BlockedSlots.Add(i);
      AddWithMaxAmount(entity, i, BLOCK_SLOT_ITEM, 1, 1);
    }
  }

  public void MakeStandPublic() {
    Stand.SetTeam(DefaultStandEntity);
  }

  public void MakeStandPrivate() {
    Stand.SetTeam(Owner.CharacterEntity);
  }

  public void DisableAllInteractions() {
    Stand.With((ref Interactable interactable) => {
      interactable.Disabled = true;
    });

    Trader.With((ref Interactable interactable) => {
      interactable.Disabled = true;
    });

    StorageChest.With((ref Interactable interactable) => {
      interactable.Disabled = true;
    });
  }

  public void SetTraderName(string name) {
    Name = name;
    Coffin.AddWith((ref ServantCoffinstation coffinStation) => {
      coffinStation.ServantName = new FixedString64Bytes(name);
    });
  }

  // Private methods to setup the trader model

  private void Attach(Entity entity) {
    // Using Follower to mantain the consistency for getting the owner of any of the entities
    // since EntityOwner can not be added to the Stand and Attach makes the servant freak out
    entity.AddWith((ref Follower follower) => {
      follower.Followed._Value = Owner.UserEntity;
    });
  }

  private void SetupCoffin() {
    Attach(Coffin);
    Coffin.SetId(COFFIN_ID);
  }

  private void SetupStorageChest() {
    Attach(StorageChest);
    StorageChest.SetId(STORAGE_ID);
    StorageChest.SetTeam(Owner.CharacterEntity);
    StorageChest.Remove<DestroyWhenInventoryIsEmpty>();
    StorageChest.Remove<ShrinkInventoryWhenWithdrawn>();
    StorageChest.AddBuffer<SyncToUserBuffer>();
    StorageChest.With((ref EditableTileModel editableTileModel) => {
      editableTileModel.CanDismantle = false;
      editableTileModel.CanMoveAfterBuild = false;
      editableTileModel.CanRotateAfterBuild = false;
    });

    var syncToUserBuffer = StorageChest.ReadBuffer<SyncToUserBuffer>();
    syncToUserBuffer.Clear();
    syncToUserBuffer.Add(new SyncToUserBuffer() {
      UserEntity = Owner.UserEntity
    });
  }

  private void SetupStand() {
    Attach(Stand);
    Stand.SetId(STAND_ID);
    Stand.With((ref EditableTileModel editableTileModel) => {
      editableTileModel.CanDismantle = false;
      editableTileModel.CanMoveAfterBuild = false;
      editableTileModel.CanRotateAfterBuild = false;
    });
    ActionScheduler.DelayedFrames((end) => {
      if (Stand.IsNull() || !Stand.Exists()) {
        end();
        return;
      }
      BlockSlots(Stand, 14, 21);
    }, 15);
    BuffService.TryApplyBuff(Stand, Buffs.Invisibility);
  }

  private void SetupTrader() {
    Attach(Trader);
    Trader.SetId(TRADER_ID);
    Trader.SetTeam(Owner.CharacterEntity);
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


    Trader.With((ref Interactable interactable) => {
      interactable.Disabled = true;
    });

    foreach (var permaBuffGuid in ServantPermaBuffs) {
      BuffService.TryApplyBuff(Trader, permaBuffGuid);
    }
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

  public void SendSucessSCT(string message) {
    SendSCT(message, new float3(0f, 1f, 0f));
  }

  public void SendErrorSCT(string message) {
    SendSCT(message, new float3(1f, 0f, 0f));
  }

  public void SendSCT(string message, float3 color) {
    ScrollingCombatTextMessage.Create(
      GameSystems.EntityManager,
      GameSystems.EndSimulationEntityCommandBufferSystem.CreateCommandBuffer(),
      AssetGuid.FromString(message),
      Trader.Position(),
      color,
      Owner.CharacterEntity,
      0,
      SCT_PREFAB,
      Owner.UserEntity
    );
  }

  public void RotateTrader() {
    RotateTile(StorageChest, Position);
    RotateTile(Stand, Position);

    RotateTraderUnit(Position);

    UpdateTraderLookDirection();
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
      RotateTrader();
    }
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

  private float3 GetCenterPosition() {
    if (Trader.IsNull() || !Trader.Exists() || StorageChest.IsNull() || !StorageChest.Exists()) {
      return Position;
    }

    var traderPos = Trader.Position();
    var chestPos = StorageChest.Position();

    var centerPos = (traderPos + chestPos) / 2f;

    return centerPos;
  }
}