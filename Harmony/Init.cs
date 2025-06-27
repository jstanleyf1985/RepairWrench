using HarmonyLib;
using System.Reflection;
using System.IO;

namespace RepairWrench
{
  public class RepairWrench : IModApi
  {
    public static void RepairWrenchStart(ref ModEvents.SGameStartDoneData _data)
    {
      RepairConfig.Load(Path.Combine(ModManager.GetMod("VehicleRepairWrench")?.Path, "Configuration.xml"));
      RepairWrenchGlobal.LoadRepairEffect();

      GameManager.Instance.StartCoroutine(UICoroutines.OnPlayerLoggedIn());
    }

    public static void RepairWrenchStop(ref ModEvents.SWorldShuttingDownData _data)
    {
      GameManager.Instance.StopCoroutine(UICoroutines.OnPlayerLoggedIn());
      UIStatic.PlayerLoaded = false;
    }

    public void InitMod(Mod _modInstance)
    {
      Log.Out(" Loading Patch: " + GetType());

      var harmony = new Harmony(GetType().ToString());
      harmony.PatchAll(Assembly.GetExecutingAssembly());

      ModEvents.GameStartDone.RegisterHandler(RepairWrenchStart);
      ModEvents.WorldShuttingDown.RegisterHandler(RepairWrenchStop);
    }
  }
}
