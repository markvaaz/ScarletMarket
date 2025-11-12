using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;
using ScarletCore;
using ScarletCore.Systems;
using ScarletCore.Utils;
using ScarletMarket.Services;
using ScarletMarket.Models;
using ScarletCore.Services;

namespace ScarletMarket.Patches;

[HarmonyPatch]
internal static class InventoryPatches
{
  [HarmonyPatch(typeof(MoveItemBetweenInventoriesSystem), nameof(MoveItemBetweenInventoriesSystem.OnUpdate))]
  [HarmonyPrefix]
  public static void Prefix(MoveItemBetweenInventoriesSystem __instance)
  {
    if (!GameSystems.Initialized) return;
    var entities = __instance.__query_133601321_0.ToEntityArray(Allocator.Temp);

    try
    {
      foreach (var entity in entities)
      {
        var moveItemEvent = entity.Read<MoveItemBetweenInventoriesEvent>();
        var niem = GameSystems.NetworkIdSystem._NetworkIdLookupMap._NetworkIdToEntityMap;

        // Validate inventory entities exist
        if (!niem.TryGetValue(moveItemEvent.ToInventory, out Entity toInv) ||
            !niem.TryGetValue(moveItemEvent.FromInventory, out Entity fromInv)) continue;

        // Check if inventories are relevant to trader system
        var toInvIsStand = toInv.IdEquals(Ids.Stand);
        var toInvIsStorage = toInv.IdEquals(Ids.Storage);
        var fromInvIsStand = fromInv.IdEquals(Ids.Stand);
        var fromInvIsStorage = fromInv.IdEquals(Ids.Storage);

        if (!fromInvIsStand && !toInvIsStand && !toInvIsStorage && !fromInvIsStorage) continue;

        var fromCharacter = entity.Read<FromCharacter>();

        // Validate player
        if (!fromCharacter.Character.Has<PlayerCharacter>() || !fromCharacter.User.Has<User>()) continue;

        var player = fromCharacter.Character.GetPlayerData();

        // If player is removing item from their own storage allow it
        if (fromInvIsStorage && fromInv.Read<Follower>().Followed._Value == player.UserEntity)
        {
          continue;
        }

        // Block same inventory movements
        if (toInv == fromInv)
        {
          TraderService.SendErrorSCT(player, SCTMessages.CannotMove);
          entity.Destroy(true);
          continue;
        }

        // Get trader from either inventory
        var trader = TraderService.GetTrader(fromInv) ?? TraderService.GetTrader(toInv);

        if (trader == null)
        {
          TraderService.SendErrorSCT(player, SCTMessages.CannotDo);
          player.SendMessage("~Something went wrong~. This trader is not registered.".FormatError());
          player.SendMessage("Please contact an administrator.");
          entity.Destroy(true);
          continue;
        }

        var playerOwnsTrader = trader.Owner == player;
        var isRemovingItem = fromInv != toInv && fromInvIsStand;
        var isAddingItem = toInvIsStand;

        // BUYING LOGIC - Player doesn't own trader and is removing item from stand
        if (!playerOwnsTrader && isRemovingItem && TraderModel.IsValidSlot(moveItemEvent.FromSlot))
        {
          if (!trader.TryBuyItem(player, moveItemEvent.FromSlot))
          {
            Log.Info($"Player {player.Name} tried to remove an item from a trader they do not own.");
            TraderService.SendErrorSCT(player, SCTMessages.CannotDo);
            entity.Destroy(true);
          }
          continue;
        }

        // SETUP LOGIC - Player owns trader
        var traderIsWaitingForItem = trader.State == TraderState.WaitingForItem || trader.State == TraderState.ReceivedCost;

        // Adding item to own stand
        if (playerOwnsTrader && isAddingItem && traderIsWaitingForItem && InventoryService.TryGetItemAtSlot(fromInv, moveItemEvent.FromSlot, out _))
        {
          if (moveItemEvent.ToSlot != -1 && !TraderModel.IsValidSlot(moveItemEvent.ToSlot))
          {
            entity.Destroy(true);
            TraderService.SendErrorSCT(player, SCTMessages.CannotDo);
            continue;
          }

          trader.HandleAddingItem();

          continue;
        }

        // Removing item from own stand
        if (playerOwnsTrader && isRemovingItem && TraderModel.IsValidSlot(moveItemEvent.FromSlot))
        {
          trader.RemoveCostItem(moveItemEvent.FromSlot + 7);
          continue;
        }

        if (playerOwnsTrader && isAddingItem && trader.State == TraderState.Ready)
        {
          trader.SendErrorSCT(SCTMessages.Disabled);
        }

        entity.Destroy(true);
      }
    }
    catch (System.Exception ex)
    {
      Log.Error($"Error in MoveItemBetweenInventoriesSystemPatch: {ex.Message}");
    }
    finally
    {
      entities.Dispose();
    }
  }

  // EVERY PATCH FROM HERE ONWARDS WILL ONLY CANCEL THE EVENTS

  [HarmonyPatch(typeof(DropInventoryItemSystem), nameof(DropInventoryItemSystem.OnUpdate))]
  [HarmonyPrefix]
  public static void Prefix(DropInventoryItemSystem __instance)
  {
    if (!GameSystems.Initialized) return;
    var entities = __instance.__query_1470978904_0.ToEntityArray(Allocator.Temp);

    try
    {
      foreach (var entity in entities)
      {
        var dropItemEvent = entity.Read<DropInventoryItemEvent>();

        var niem = GameSystems.NetworkIdSystem._NetworkIdLookupMap._NetworkIdToEntityMap;

        if (!niem.TryGetValue(dropItemEvent.Inventory, out Entity inv)) continue;

        if (!inv.IdEquals(Ids.Stand) && !inv.IdEquals(Ids.Storage)) continue;

        var fromCharacter = entity.Read<FromCharacter>();

        if (!fromCharacter.Character.Has<PlayerCharacter>() || !fromCharacter.User.Has<User>()) continue;

        var player = fromCharacter.Character.GetPlayerData();

        // If player is removing item from their own storage allow it
        if (inv.IdEquals(Ids.Storage) && inv.Read<Follower>().Followed._Value == player.UserEntity)
        {
          continue;
        }

        TraderService.SendErrorSCT(player, SCTMessages.CannotMove);

        entity.Destroy(true);
      }
    }
    catch (System.Exception ex)
    {
      Log.Error($"Error in DropInventoryItemSystemPatch: {ex.Message}");
    }
    finally
    {
      entities.Dispose();
    }
  }

  [HarmonyPatch(typeof(SortSingleInventorySystem), nameof(SortSingleInventorySystem.OnUpdate))]
  [HarmonyPrefix]
  public static void Prefix(SortSingleInventorySystem __instance)
  {
    if (!GameSystems.Initialized) return;
    var entities = __instance._EventQuery.ToEntityArray(Allocator.Temp);

    try
    {
      foreach (var entity in entities)
      {
        var sortEvent = entity.Read<SortSingleInventoryEvent>();
        var niem = GameSystems.NetworkIdSystem._NetworkIdLookupMap._NetworkIdToEntityMap;

        if (!niem.TryGetValue(sortEvent.Inventory, out Entity inv)) continue;

        if (!inv.IdEquals(Ids.Storage) && !inv.IdEquals(Ids.Stand)) continue;

        var fromCharacter = entity.Read<FromCharacter>();

        if (!fromCharacter.Character.Has<PlayerCharacter>() || !fromCharacter.User.Has<User>()) continue;

        var player = fromCharacter.Character.GetPlayerData();

        if (inv.Read<Follower>().Followed._Value != player.UserEntity)
        {
          TraderService.SendErrorSCT(player, SCTMessages.CannotDo);
          entity.Destroy(true);
          continue;
        }

        var trader = TraderService.GetTrader(inv);

        if (trader == null)
        {
          TraderService.SendErrorSCT(player, SCTMessages.CannotDo);
          player.SendMessage("~Something went wrong~. This trader is not registered.".FormatError());
          player.SendMessage("Please contact an administrator.");
          entity.Destroy(true);
          continue;
        }

        trader.SetAsNotReady();

        entity.Destroy(true);
      }
    }
    catch (System.Exception ex)
    {
      Log.Error($"Error in SortSingleInventorySystemPatch: {ex.Message}");
    }
    finally
    {
      entities.Dispose();
    }
  }

  [HarmonyPatch(typeof(SortAllInventoriesSystem), nameof(SortAllInventoriesSystem.OnUpdate))]
  [HarmonyPrefix]
  public static void Prefix(SortAllInventoriesSystem __instance)
  {
    if (!GameSystems.Initialized) return;
    var entities = __instance.__query_133601798_0.ToEntityArray(Allocator.Temp);

    try
    {
      foreach (var entity in entities)
      {
        var sortEvent = entity.Read<SortAllInventoriesEvent>();
        var niem = GameSystems.NetworkIdSystem._NetworkIdLookupMap._NetworkIdToEntityMap;

        if (!niem.TryGetValue(sortEvent.Inventory, out Entity inv)) continue;

        if (!inv.IdEquals(Ids.Storage) && !inv.IdEquals(Ids.Stand)) continue;

        entity.Destroy(true);
      }
    }
    catch (System.Exception ex)
    {
      Log.Error($"Error in SortAllInventoriesSystemPatch: {ex.Message}");
    }
    finally
    {
      entities.Dispose();
    }
  }

  [HarmonyPatch(typeof(MoveAllItemsBetweenInventoriesSystem), nameof(MoveAllItemsBetweenInventoriesSystem.OnUpdate))]
  [HarmonyPrefix]
  public static void Prefix(MoveAllItemsBetweenInventoriesSystem __instance)
  {
    if (!GameSystems.Initialized) return;
    var entities = __instance.__query_133601579_0.ToEntityArray(Allocator.Temp);

    try
    {
      foreach (var entity in entities)
      {
        var moveItemEvent = entity.Read<MoveAllItemsBetweenInventoriesEvent>();
        var niem = GameSystems.NetworkIdSystem._NetworkIdLookupMap._NetworkIdToEntityMap;

        if (!niem.TryGetValue(moveItemEvent.ToInventory, out Entity toInv) || !niem.TryGetValue(moveItemEvent.FromInventory, out Entity fromInv)) continue;

        if (!fromInv.IdEquals(Ids.Storage) && !fromInv.IdEquals(Ids.Stand) && !toInv.IdEquals(Ids.Stand) && !toInv.IdEquals(Ids.Storage)) continue;

        var fromCharacter = entity.Read<FromCharacter>();

        if (!fromCharacter.Character.Has<PlayerCharacter>() || !fromCharacter.User.Has<User>()) continue;

        var player = fromCharacter.Character.GetPlayerData();

        // If player is removing item from their own storage allow it
        if (fromInv.IdEquals(Ids.Storage) && fromInv.Read<Follower>().Followed._Value == player.UserEntity)
        {
          continue;
        }

        if (fromInv.Read<Follower>().Followed._Value != player.UserEntity)
        {
          TraderService.SendErrorSCT(player, SCTMessages.CannotDo);
          entity.Destroy(true);
          continue;
        }

        var trader = TraderService.GetTrader(fromInv);

        if (trader == null)
        {
          TraderService.SendErrorSCT(player, SCTMessages.CannotDo);
          entity.Destroy(true);
          continue;
        }

        trader.SetAsReady();

        entity.Destroy(true);
      }
    }
    catch (System.Exception ex)
    {
      Log.Error($"Error in MoveAllItemsBetweenInventoriesSystemPatch: {ex.Message}");
    }
    finally
    {
      entities.Dispose();
    }
  }

  [HarmonyPatch(typeof(MoveAllItemsBetweenInventoriesV2System), nameof(MoveAllItemsBetweenInventoriesV2System.OnUpdate))]
  [HarmonyPrefix]
  public static void Prefix(MoveAllItemsBetweenInventoriesV2System __instance)
  {
    if (!GameSystems.Initialized) return;
    var entities = __instance.__query_133601631_0.ToEntityArray(Allocator.Temp);

    try
    {
      foreach (var entity in entities)
      {
        var moveItemEvent = entity.Read<MoveAllItemsBetweenInventoriesEventV2>();
        var niem = GameSystems.NetworkIdSystem._NetworkIdLookupMap._NetworkIdToEntityMap;

        if (!niem.TryGetValue(moveItemEvent.ToInventory, out Entity toInv) || !niem.TryGetValue(moveItemEvent.FromInventory, out Entity fromInv)) continue;

        if (!fromInv.IdEquals(Ids.Storage) && !fromInv.IdEquals(Ids.Stand) && !toInv.IdEquals(Ids.Stand) && !toInv.IdEquals(Ids.Storage)) continue;

        var fromCharacter = entity.Read<FromCharacter>();

        if (!fromCharacter.Character.Has<PlayerCharacter>() || !fromCharacter.User.Has<User>()) continue;

        var player = fromCharacter.Character.GetPlayerData();

        // If player is removing item from their own storage allow it
        if (fromInv.IdEquals(Ids.Storage) && fromInv.Read<Follower>().Followed._Value == player.UserEntity)
        {
          continue;
        }

        TraderService.SendErrorSCT(player, SCTMessages.CannotMove);

        entity.Destroy(true);
      }
    }
    catch (System.Exception ex)
    {
      Log.Error($"Error in MoveAllItemsBetweenInventoriesV2SystemPatch: {ex.Message}");
    }
    finally
    {
      entities.Dispose();
    }
  }

  [HarmonyPatch(typeof(SplitItemSystem), nameof(SplitItemSystem.OnUpdate))]
  [HarmonyPrefix]
  public static void Prefix(SplitItemSystem __instance)
  {
    if (!GameSystems.Initialized) return;
    var entities = __instance._Query.ToEntityArray(Allocator.Temp);

    try
    {
      foreach (var entity in entities)
      {
        var splitEvent = entity.Read<SplitItemEvent>();
        var niem = GameSystems.NetworkIdSystem._NetworkIdLookupMap._NetworkIdToEntityMap;

        if (!niem.TryGetValue(splitEvent.Inventory, out Entity inv)) continue;

        if (!inv.IdEquals(Ids.Storage) && !inv.IdEquals(Ids.Stand)) continue;

        var fromCharacter = entity.Read<FromCharacter>();

        if (!fromCharacter.Character.Has<PlayerCharacter>() || !fromCharacter.User.Has<User>()) continue;

        var player = fromCharacter.Character.GetPlayerData();

        TraderService.SendErrorSCT(player, SCTMessages.CannotDo);

        entity.Destroy(true);
      }
    }
    catch (System.Exception ex)
    {
      Log.Error($"Error in SplitItemSystemPatch: {ex.Message}");
    }
    finally
    {
      entities.Dispose();
    }
  }

  [HarmonyPatch(typeof(SplitItemV2System), nameof(SplitItemV2System.OnUpdate))]
  [HarmonyPrefix]
  public static void Prefix(SplitItemV2System __instance)
  {
    if (!GameSystems.Initialized) return;
    var entities = __instance._Query.ToEntityArray(Allocator.Temp);

    try
    {
      foreach (var entity in entities)
      {
        var splitEvent = entity.Read<SplitItemEventV2>();
        var niem = GameSystems.NetworkIdSystem._NetworkIdLookupMap._NetworkIdToEntityMap;

        if (!niem.TryGetValue(splitEvent.FromInventory, out Entity fromInv) || !niem.TryGetValue(splitEvent.ToInventory, out Entity toInv)) continue;

        if (!fromInv.IdEquals(Ids.Storage) && !fromInv.IdEquals(Ids.Stand) && !toInv.IdEquals(Ids.Storage) && !toInv.IdEquals(Ids.Stand)) continue;

        var fromCharacter = entity.Read<FromCharacter>();

        if (!fromCharacter.Character.Has<PlayerCharacter>() || !fromCharacter.User.Has<User>()) continue;

        var player = fromCharacter.Character.GetPlayerData();

        TraderService.SendErrorSCT(player, SCTMessages.CannotDo);

        entity.Destroy(true);
      }
    }
    catch (System.Exception ex)
    {
      Log.Error($"Error in SplitItemV2SystemPatch: {ex.Message}");
    }
    finally
    {
      entities.Dispose();
    }
  }

  [HarmonyPatch(typeof(SmartMergeItemsBetweenInventoriesSystem), nameof(SmartMergeItemsBetweenInventoriesSystem.OnUpdate))]
  [HarmonyPrefix]
  public static void Prefix(SmartMergeItemsBetweenInventoriesSystem __instance)
  {
    if (!GameSystems.Initialized) return;
    var entities = __instance.__query_133601682_0.ToEntityArray(Allocator.Temp);

    try
    {
      foreach (var entity in entities)
      {
        var moveItemEvent = entity.Read<SmartMergeItemsBetweenInventoriesEvent>();
        var niem = GameSystems.NetworkIdSystem._NetworkIdLookupMap._NetworkIdToEntityMap;

        if (!niem.TryGetValue(moveItemEvent.ToInventory, out Entity toInv) || !niem.TryGetValue(moveItemEvent.FromInventory, out Entity fromInv)) continue;

        if (!toInv.IdEquals(Ids.Stand) && !toInv.IdEquals(Ids.Storage)) continue;

        var fromCharacter = entity.Read<FromCharacter>();

        if (!fromCharacter.Character.Has<PlayerCharacter>() || !fromCharacter.User.Has<User>()) continue;

        var player = fromCharacter.Character.GetPlayerData();

        TraderService.SendErrorSCT(player, SCTMessages.CannotDo);

        entity.Destroy(true);
      }
    }
    catch (System.Exception ex)
    {
      Log.Error($"Error in SmartMergeItemsBetweenInventoriesSystemPatch: {ex.Message}");
    }
    finally
    {
      entities.Dispose();
    }
  }

  [HarmonyPatch(typeof(EquipItemFromInventorySystem), nameof(EquipItemFromInventorySystem.OnUpdate))]
  [HarmonyPrefix]
  public static void Prefix(EquipItemFromInventorySystem __instance)
  {
    if (!GameSystems.Initialized) return;
    var entities = __instance._Query.ToEntityArray(Allocator.Temp);

    try
    {
      foreach (var entity in entities)
      {
        var equipEvent = entity.Read<EquipItemFromInventoryEvent>();
        var niem = GameSystems.NetworkIdSystem._NetworkIdLookupMap._NetworkIdToEntityMap;

        if (!niem.TryGetValue(equipEvent.FromInventory, out Entity fromInv)) continue;

        if (!fromInv.IdEquals(Ids.Storage) && !fromInv.IdEquals(Ids.Stand)) continue;

        var fromCharacter = entity.Read<FromCharacter>();

        if (!fromCharacter.Character.Has<PlayerCharacter>() || !fromCharacter.User.Has<User>()) continue;

        var player = fromCharacter.Character.GetPlayerData();

        TraderService.SendErrorSCT(player, SCTMessages.CannotDo);

        entity.Destroy(true);
      }
    }
    catch (System.Exception ex)
    {
      Log.Error($"Error in EquipItemSystemPatch: {ex.Message}");
    }
    finally
    {
      entities.Dispose();
    }
  }

  [HarmonyPatch(typeof(UnEquipItemSystem), nameof(UnEquipItemSystem.OnUpdate))]
  [HarmonyPrefix]
  public static void Prefix(UnEquipItemSystem __instance)
  {
    if (!GameSystems.Initialized) return;
    var entities = __instance._Query.ToEntityArray(Allocator.Temp);

    try
    {
      foreach (var entity in entities)
      {
        var unequipEvent = entity.Read<UnequipItemEvent>();
        var niem = GameSystems.NetworkIdSystem._NetworkIdLookupMap._NetworkIdToEntityMap;

        if (!niem.TryGetValue(unequipEvent.ToInventory, out Entity toInv)) continue;

        if (!toInv.IdEquals(Ids.Storage) && !toInv.IdEquals(Ids.Stand)) continue;

        var fromCharacter = entity.Read<FromCharacter>();

        if (!fromCharacter.Character.Has<PlayerCharacter>() || !fromCharacter.User.Has<User>()) continue;

        var player = fromCharacter.Character.GetPlayerData();

        TraderService.SendErrorSCT(player, SCTMessages.CannotDo);

        entity.Destroy(true);
      }
    }
    catch (System.Exception ex)
    {
      Log.Error($"Error in UnEquipItemSystemPatch: {ex.Message}");
    }
    finally
    {
      entities.Dispose();
    }
  }
}
