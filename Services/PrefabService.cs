using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using ScarletCore.Utils;
using Stunlock.Core;

namespace ScarletMarket.Services;

internal static class PrefabService {
  public static string FileName = "ItemPrefabNames";

  public static Dictionary<int, string> AllItemPrefabNames { get; private set; } = [];

  public static Dictionary<int, string> ItemPrefabNames { get; private set; } = [];

  public static List<int> ServantPrefabs { get; private set; } = [];

  public static void Initialize() {
    LoadAllItemPrefabNames();
    LoadItemPrefabNames();
  }

  public static void LoadServantPrefabs() {
    var resourceName = "ScarletMarket.Localization.ServantPrefabs.json";
    var jsonContent = LoadResource(resourceName);

    ServantPrefabs = JsonSerializer.Deserialize<List<int>>(jsonContent);
  }

  public static void LoadAllItemPrefabNames() {
    var resourceName = "ScarletMarket.Localization.ItemPrefabNames.json";
    var jsonContent = LoadResource(resourceName);

    AllItemPrefabNames = JsonSerializer.Deserialize<Dictionary<int, string>>(jsonContent);
  }

  public static void LoadItemPrefabNames() {
    if (Plugin.Database.Has(FileName)) {
      ItemPrefabNames = Plugin.Database.Get<Dictionary<int, string>>(FileName);
      return;
    }

    var resourceName = "ScarletMarket.Localization.ItemPrefabNames.json";
    var jsonContent = LoadResource(resourceName);

    ItemPrefabNames = JsonSerializer.Deserialize<Dictionary<int, string>>(jsonContent);
    Plugin.Database.Save(FileName, ItemPrefabNames);
  }

  public static string LoadResource(string resourceName) {
    var assembly = Assembly.GetExecutingAssembly();
    var stream = assembly.GetManifestResourceStream(resourceName);
    string jsonContent = null;
    if (stream != null) {
      using var reader = new StreamReader(stream);
      jsonContent = reader.ReadToEnd();

    } else {
      Log.Error($"Resource '{resourceName}' not found in assembly '{assembly.FullName}'");
    }

    return jsonContent;
  }

  public static string GetItem(int prefabId) {
    if (ItemPrefabNames.TryGetValue(prefabId, out var name)) {
      return name;
    }
    return $"Unknown Prefab {prefabId}";
  }

  public static string GetItem(PrefabGUID prefabId) {
    return GetItem(prefabId.GuidHash);
  }

  public static string GetAllItem(int prefabId) {
    if (AllItemPrefabNames.TryGetValue(prefabId, out var name)) {
      return name;
    }
    return $"Unknown Prefab {prefabId}";
  }

  public static string GetAllItem(PrefabGUID prefabId) {
    return GetAllItem(prefabId.GuidHash);
  }

  public static bool IsValidServant(PrefabGUID prefabGUID) {
    return IsValidServant(prefabGUID.GuidHash);
  }

  public static bool IsValidServant(int prefabGUID) {
    if (ServantPrefabs.Count == 0) {
      LoadServantPrefabs();
    }
    return ServantPrefabs.Contains(prefabGUID);
  }
}
