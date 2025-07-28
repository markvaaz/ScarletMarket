using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using ScarletCore.Data;
using ScarletCore.Events;
using ScarletCore.Systems;
using ScarletMarket.Services;
using VampireCommandFramework;

namespace ScarletMarket;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("markvaaz.ScarletCore")]
[BepInDependency("gg.deca.VampireCommandFramework")]
public class Plugin : BasePlugin {
  static Harmony _harmony;
  public static Harmony Harmony => _harmony;
  public static Plugin Instance { get; private set; }
  public static ManualLogSource LogInstance { get; private set; }
  public static Settings Settings { get; private set; }
  public static Database Database { get; private set; }

  public override void Load() {
    Instance = this;
    LogInstance = Log;

    Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} version {MyPluginInfo.PLUGIN_VERSION} is loaded!");

    _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
    _harmony.PatchAll(Assembly.GetExecutingAssembly());

    Settings = new Settings(MyPluginInfo.PLUGIN_GUID, Instance);
    Database = new Database(MyPluginInfo.PLUGIN_GUID);

    PrefabService.Initialize();

    LoadSettings();
    CommandRegistry.RegisterAll();

    if (GameSystems.Initialized) {
      TraderService.Initialize();
    } else {
      EventManager.OnInitialize += OnInitialize;
    }
  }

  public static void OnInitialize(object _, InitializeEventArgs args) {
    TraderService.Initialize();
    EventManager.OnInitialize -= OnInitialize;
  }

  public override bool Unload() {
    _harmony?.UnpatchSelf();
    CommandRegistry.UnregisterAssembly();
    ActionScheduler.UnregisterAssembly(Assembly.GetExecutingAssembly());
    return true;
  }

  public static void ReloadSettings() {
    Settings.Dispose();
    LoadSettings();
  }
  public static void LoadSettings() {
    Settings.Section("Trader")
      .Add("TraderPrefab", 40217214, "Trader prefab GUID. IMPORTANT: Only use characters whose prefab name ends with _Servant (e.g., CHAR_Bandit_Bomber_Servant). Using any other type will brick your save and it will be lost!");

    if (Settings.Get<int>("TraderPrefab") == 0 || !PrefabService.IsValidServant(Settings.Get<int>("TraderPrefab"))) {
      Settings.Set("TraderPrefab", 40217214);
      LogInstance.LogError("Trader prefab GUID is invalid or not a servant! Reverting to default.");
    }

    Settings.Section("Plot Purchase")
      .Add("PrefabGUID", 0, "Item GUID required to claim a plot. Set to 0 to make plots free.\nUse item GUIDs from the game or community databases.")
      .Add("Amount", 0, "Number of items required to claim a plot.\nIf set to 0, plots can be claimed without any cost.");

    Settings.Section("Trader Timeout")
      .Add("TraderTimeoutEnabled", true, "Enable/disable the trader timeout system entirely.\nWhen disabled, trader shops will never be automatically removed.")
      .Add("MaxInactiveDays", 15, "Maximum days a player can be offline before their trader shop is automatically removed.\nWarning: All items in the shop and storage will be permanently lost!")
      .Add("RemoveEmptyTradersOnStartup", true, "Clean up empty trader shops when the server starts.\nOnly removes shops with no items in both display and storage areas.");
  }
}
