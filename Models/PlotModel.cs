using ProjectM;
using ProjectM.Gameplay;
using ScarletCore;
using ScarletCore.Services;
using ScarletCore.Systems;
using ScarletCore.Utils;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ScarletMarket.Models;

internal class PlotModel {
  public Entity Entity { get; set; } = Entity.Null;
  public Entity Inspect { get; private set; } = Entity.Null;
  public float3 Position => Entity.Position();
  public float Radius = PLOT_RADIUS;
  public quaternion Rotation => Entity.Read<Rotation>().Value;
  public TraderModel Trader { get; set; } = null;
  public GhostTraderModel GhostPlaceholder { get; set; }
  public bool IsVisible => GetCircleRadius() > 0f;

  public PlotModel(float3 position) {
    if (!PrefabGuidToEntityMap.TryGetValue(Spawnable.DuelCircle, out var prefab)) {
      Log.Error($"Failed to find prefab for Duel Circle with GUID: {Spawnable.DuelCircle.GuidHash}");
      return;
    }
    Entity = GameSystems.EntityManager.Instantiate(prefab);
    Entity.SetId(Ids.Plot);
    Inspect = UnitSpawnerService.ImmediateSpawn(Spawnable.Inspect, position, 0f, 0f, -1f);
    Inspect.SetId(Ids.Inspect);

    Inspect.AddWith((ref Follower follower) => {
      follower.Followed._Value = Entity;
    });

    Inspect.Write(new Script_InspectTarget_Data());

    MoveAreaTo(position);
    Entity.Write(new Rotation { Value = quaternion.identity });
    GhostPlaceholder = new GhostTraderModel(this);
    SetRadius(0);
    Show();
  }

  // For loading existing plot with inspect entity
  public PlotModel(Entity entity, Entity inspectEntity) {
    if (entity.IsNull() || !entity.Exists()) {
      Log.Error("Cannot create PlotModel: Entity is null or does not exist.");
      return;
    }
    Entity = entity;
    Inspect = inspectEntity;
    MoveAreaTo(Position);
    SetRadius(0);
  }

  public bool IsInside(float3 position) {
    return MathUtility.Distance(position, Position) <= PLOT_RADIUS;
  }

  public void Show() {
    if (Entity == Entity.Null) {
      Log.Error("Cannot show ArrivalArea: Entity is null.");
      return;
    }
    GhostPlaceholder.Show();
    if (Inspect != Entity.Null && Inspect.Exists()) {
      Inspect.SetPosition(Position);
    }
  }

  public void ShowPlot() {
    PlayAnimation(0f, PLOT_RADIUS, SetRadius);
  }

  public void Hide() {
    if (Entity == Entity.Null) {
      Log.Error("Cannot hide ArrivalArea: Entity is null.");
      return;
    }
    GhostPlaceholder.Hide();
    if (Inspect != Entity.Null && Inspect.Exists()) {
      Inspect.SetPosition(Position + new float3(0, COFFIN_HEIGHT, 0));
    }
  }

  public void HidePlot() {
    PlayAnimation(PLOT_RADIUS, 0f, SetRadius);
  }

  public void Destroy() {
    // Destroy ghost placeholder if it exists
    GhostPlaceholder?.Destroy();
    GhostPlaceholder = null;

    if (Entity != Entity.Null && Entity.Exists()) {
      Entity.Destroy();
      Entity = Entity.Null;
    }

    if (Inspect != Entity.Null && Inspect.Exists()) {
      Inspect.Destroy();
      Inspect = Entity.Null;
    }
  }

  public void MoveAreaTo(float3 position) {
    // Align the plot to the tile grid
    var offset = -0.25f;
    var snapped = new float3(math.round(position.x * 2) / 2f, position.y, math.round(position.z * 2) / 2f);
    var rotation = Rotation;
    var forward = math.mul(rotation, new float3(1, 0, 0));
    var pos = snapped + forward * offset;
    TeleportService.TeleportToPosition(Entity, pos);
    TeleportService.TeleportToPosition(Inspect, pos);
  }

  public void MovePlotTo(float3 position) {
    if (Entity.IsNull() || !Entity.Exists()) return;

    MoveAreaTo(position);
    GhostPlaceholder?.AlignToPlotRotation();
    Trader?.AlignToPlotRotation();
  }

  public int GetCurrentRotationDegrees() {
    return CalculateCurrentRotation() * 90;
  }

  public void Rotate() {
    var currentRotation = CalculateCurrentRotation();
    var newRotation = (currentRotation + 1) % 4;
    Entity.Write(new Rotation { Value = quaternion.RotateY(math.radians(-90 * newRotation)) });
    GhostPlaceholder?.AlignToPlotRotation();
    Trader?.AlignToPlotRotation();
  }

  private int CalculateCurrentRotation() {
    if (Entity.IsNull() || !Entity.Exists()) return 0;

    var rotation = Entity.Read<Rotation>().Value;
    var forward = math.mul(rotation, new float3(0, 0, 1));

    var threshold = 0.4f;

    if (forward.z > threshold) return 0;
    if (forward.x < -threshold) return 1;
    if (forward.z < -threshold) return 2;
    if (forward.x > threshold) return 3;

    return 0;
  }

  public void SetRadius(float radius) {
    if (Entity.IsNull() || !Entity.Exists()) return;

    Entity.HasWith((ref DuelArea da) => {
      da.Radius = radius;
    });
  }

  public float GetCircleRadius() {
    if (Entity.IsNull() || !Entity.Exists()) return 0f;

    return Entity.Read<DuelArea>().Radius;
  }

  private static void PlayAnimation(float from, float to, System.Action<float> onUpdate, int steps = 30) {
    int currentExecution = 0;

    ActionScheduler.OncePerFrame((end) => {
      float progress = (float)currentExecution / (steps - 1);
      float lerpedValue = math.lerp(from, to, progress);
      onUpdate(lerpedValue);
      currentExecution++;

      if (currentExecution >= steps) {
        end();
      }
    }, steps);
  }
}