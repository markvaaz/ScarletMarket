using HarmonyLib;
using ProjectM;
using ProjectM.Gameplay.Systems;
using ProjectM.Shared;
using ScarletCore;
using ScarletMarket.Services;
using Unity.Collections;
using Unity.Entities;

namespace ScarletMarket.Patches;

[HarmonyPatch(typeof(InteractValidateAndStopSystemServer), nameof(InteractValidateAndStopSystemServer.OnUpdate))]
internal static class InteractPatch {
  [HarmonyPrefix]
  public static void Prefix(InteractValidateAndStopSystemServer __instance) {
    var query = __instance.__query_195794971_3.ToEntityArray(Allocator.Temp);

    foreach (var entity in query) {
      if (entity.GetPrefabGuid() != INTERACT_INSPECT) continue;
      var playerEntity = entity.Read<EntityOwner>().Owner;

      if (playerEntity.IsNull() || !playerEntity.Has<PlayerCharacter>()) continue;

      var player = playerEntity.GetPlayerData();

      if (player == null) continue;

      var inspect = playerEntity.Read<Interactor>().Target;

      if (!inspect.Has<Follower>()) continue;

      var plot = inspect.Read<Follower>().Followed._Value;

      if (plot.IsNull() || !plot.Exists() || !plot.IdEquals(PLOT_ID)) continue;

      TraderService.TryBuyPlot(player);
    }
  }
}