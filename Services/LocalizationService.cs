using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using ScarletCore.Utils;
using Stunlock.Core;

namespace ScarletMarket.Services;

internal static class LocalizationService {
  public static Dictionary<int, string> PrefabNames { get; private set; } = [];
  public static void LoadPrefabNames() {
    var resourceName = "ScarletMarket.Localization.ItemPrefabNames.json";

    var assembly = Assembly.GetExecutingAssembly();
    var stream = assembly.GetManifestResourceStream(resourceName);
    if (stream != null) {
      using var reader = new StreamReader(stream);
      string jsonContent = reader.ReadToEnd();
      PrefabNames = JsonSerializer.Deserialize<Dictionary<int, string>>(jsonContent);
    } else {
      Log.Error($"Resource '{resourceName}' not found in assembly '{assembly.FullName}'");
    }
  }

  public static string Get(int prefabId) {
    if (PrefabNames.TryGetValue(prefabId, out var name)) {
      return name;
    }
    return $"Unknown Prefab {prefabId}";
  }

  public static string Get(PrefabGUID prefabId) {
    return Get(prefabId.GuidHash);
  }
}
