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

    if (!entity.Has<NameableInteractable>()) {
      entity.AddWith((ref NameableInteractable nameable) => {
        nameable.Name = new FixedString64Bytes(id);
        nameable.OnlyAllyRename = true;
        nameable.OnlyAllySee = true;
      });
    } else {
      entity.With((ref NameableInteractable nameable) => {
        nameable.Name = new FixedString64Bytes(id);
        nameable.OnlyAllyRename = true;
        nameable.OnlyAllySee = true;
      });
    }
  }

  public static bool IdEquals(this Entity entity, string id) {
    if (entity == Entity.Null || !entity.Has<NameableInteractable>()) return false;
    return entity.Read<NameableInteractable>().Name.Value == id;
  }
}