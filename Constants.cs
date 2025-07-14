using ScarletCore.Systems;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;

namespace ScarletMarket;

internal static class Constants {
  public const string TRADER_ID = "__ScarletMarket.Trader__";
  public const string STAND_ID = "__ScarletMarket.Stand__";
  public const string STORAGE_ID = "__ScarletMarket.Storage__";
  public const string COFFIN_ID = "__ScarletMarket.Coffin__";
  public const string PLOT_ID = "__ScarletMarket.Plot__";
  public const string TEMP_ENTITY_ID = "__ScarletMarket.TempTrader__";
  public const string DISABLED_MESSAGE = "3bf7e066-4e49-4ae4-b7a3-6703b7a15dc1";
  public const string ENABLED_MESSAGE = "f0c8d1b2-3e4a-4c5b-8f6d-7e8f9a0b1c2d";
  public const string DONE_MESSAGE = "54d48cbf-6817-42e5-a23f-354ca531c514";
  public const string CANNOT_DO_MESSAGE = "45e3238f-36c1-427c-b21c-7d50cfbd77bc";
  public const string CANNOT_MOVE_MESSAGE = "298b546b-1686-414b-a952-09836842bedc";
  public const string READY_TO_CHANGE_MESSAGE = "57b699b7-482c-4ad1-9ce3-867cd5cca3fb";
  public const string ALREADY_ASSIGNED_MESSAGE = "d5e62c6c-751f-4629-bfc5-459fd79ea41a";
  public const string PRIVATE_MESSAGE = "80e97474-e56f-4356-bc7d-698a807ac714";
  public const string FREE_MESSAGE = "fc3179c1-3f18-4044-b207-c1c148fb1cd4";
  public const string OPEN_MESSAGE = "4ab8d098-2c0c-4719-bf0f-852522d2b424";
  public const string CLOSE_MESSAGE = "9b97e97d-7d95-4900-af81-1f8457c25182";
  public const int COFFIN_HEIGHT = 223;
  public const float PLOT_RADIUS = 2f;
  public static readonly PrefabGUID NEUTRAL_FACTION = new(-1430861195);
  public static readonly PrefabGUID BLOCK_SLOT_ITEM = new(-696770536);
  public static readonly PrefabGUID SCT_PREFAB = new(-1404311249);
  public static NativeParallelHashMap<PrefabGUID, Entity> PrefabGuidToEntityMap = GameSystems.PrefabCollectionSystem._PrefabGuidToEntityMap;
}

internal static class Spawnable {
  public static readonly PrefabGUID StandChest = new(279811010);
  public static readonly PrefabGUID StorageChest = new(-220201461);
  public static readonly PrefabGUID Coffin = new(723455393);
  public static readonly PrefabGUID Trader = new(-823557242); // 1703325932 1502148822 40217214 -823557242
  public static readonly PrefabGUID DuelCircle = new(-893175652);
}

internal static class Buffs {
  public static readonly PrefabGUID Invulnerable = new(-480024072);
  public static readonly PrefabGUID DisableAggro = new(1934061152);
  public static readonly PrefabGUID Immaterial = new(1360141727);
  public static readonly PrefabGUID Invisibility = new(1880224358);
  public static readonly PrefabGUID ClosedVisualClue = new(647429443);
}

internal static class TraderState {
  public static readonly PrefabGUID WaitingForItem = new(1237316881);
  public static readonly PrefabGUID WaitingForCost = new(1118893557);
  public static readonly PrefabGUID ReceivedCost = new(363438545);
  public static readonly PrefabGUID Ready = new(-301760618);
  public static bool IsValid(PrefabGUID state) {
    return state == WaitingForItem || state == WaitingForCost || state == ReceivedCost || state == Ready;
  }
}