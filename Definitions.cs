using ScarletCore.Systems;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;

namespace ScarletMarket;

internal static class Constants {
  public const int COFFIN_HEIGHT = 223;
  public const float PLOT_RADIUS = 2.3f;
  public static readonly PrefabGUID NEUTRAL_FACTION = new(-1430861195);
  public static readonly PrefabGUID BLOCK_SLOT_ITEM = new(1488205677); // -696770536
  public static readonly PrefabGUID SCT_PREFAB = new(-1404311249);
  public static readonly PrefabGUID INTERACT_INSPECT = new(222103866);
}

internal static class GameData {
  public static NativeParallelHashMap<PrefabGUID, Entity> PrefabGuidToEntityMap => GameSystems.PrefabCollectionSystem._PrefabGuidToEntityMap;
}


internal static class SCTMessages {
  public const string Disabled = "3bf7e066-4e49-4ae4-b7a3-6703b7a15dc1";
  public const string Enabled = "f0c8d1b2-3e4a-4c5b-8f6d-7e8f9a0b1c2d";
  public const string Done = "54d48cbf-6817-42e5-a23f-354ca531c514";
  public const string CannotDo = "45e3238f-36c1-427c-b21c-7d50cfbd77bc";
  public const string CannotMove = "298b546b-1686-414b-a952-09836842bedc";
  public const string ReadyToChange = "57b699b7-482c-4ad1-9ce3-867cd5cca3fb";
  public const string AlreadyAssigned = "d5e62c6c-751f-4629-bfc5-459fd79ea41a";
  public const string Private = "80e97474-e56f-4356-bc7d-698a807ac714";
  public const string Free = "fc3179c1-3f18-4044-b207-c1c148fb1cd4";
  public const string Open = "4ab8d098-2c0c-4719-bf0f-852522d2b424";
  public const string Close = "9b97e97d-7d95-4900-af81-1f8457c25182";
}

internal static class Ids {
  public const string Trader = "__ScarletMarket.Trader__";
  public const string Stand = "__ScarletMarket.Stand__";
  public const string Storage = "__ScarletMarket.Storage__";
  public const string Coffin = "__ScarletMarket.Coffin__";
  public const string Plot = "__ScarletMarket.Plot__";
  public const string GhostTrader = "__ScarletMarket.GTrader__";
  public const string GhostStand = "__ScarletMarket.GStand__";
  public const string GhostStorage = "__ScarletMarket.GStorage__";
  public const string GhostCoffin = "__ScarletMarket.GCoffin__";
  public const string GhostPlot = "__ScarletMarket.GPlot__";
  public const string Inspect = "__ScarletMarket.Inspect__";
}

internal static class Spawnable {
  public static readonly PrefabGUID StandChest = new(279811010);
  public static readonly PrefabGUID StorageChest = new(-220201461);
  public static readonly PrefabGUID Coffin = new(723455393);
  public static readonly PrefabGUID Trader = new(-823557242); // 1703325932 1502148822 40217214 -823557242
  public static readonly PrefabGUID DuelCircle = new(-893175652);
  public static readonly PrefabGUID Inspect = new(1727016613);
}

internal static class Buffs {
  public static readonly PrefabGUID Invulnerable = new(-480024072);
  public static readonly PrefabGUID DisableAggro = new(1934061152);
  public static readonly PrefabGUID Immaterial = new(1360141727);
  public static readonly PrefabGUID Invisibility = new(1880224358);
  public static readonly PrefabGUID ClosedVisualClue = new(647429443);
  public static readonly PrefabGUID Ghost = new(-259674366);
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