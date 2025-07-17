using ProjectM;
using ScarletCore;
using Unity.Collections;
using Unity.Entities;

namespace ScarletMarket;

internal static class IdHelper {
  public static string GetId(this Entity entity) {
    if (entity == Entity.Null || !entity.Has<NameableInteractable>()) return null;
    return entity.Read<NameableInteractable>().Name.Value;
  }

  public static void SetId(this Entity entity, string id) {
    if (entity == Entity.Null) return;

    entity.AddWith((ref NameableInteractable nameable) => {
      nameable.Name = new FixedString64Bytes(id);
    });
  }

  public static bool IdEquals(this Entity entity, string id) {
    if (entity == Entity.Null || !entity.Has<NameableInteractable>()) return false;
    return entity.Read<NameableInteractable>().Name.Value == id;
  }
}