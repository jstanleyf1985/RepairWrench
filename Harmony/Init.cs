using HarmonyLib;
using System.Reflection;
using UnityEngine;
using System.IO;

namespace RepairWrench
{
  public class RepairWrench : IModApi
  {
    public void InitMod(Mod _modInstance)
    {
      Log.Out(" Loading Patch: " + GetType());

      var harmony = new Harmony(GetType().ToString());
      harmony.PatchAll(Assembly.GetExecutingAssembly());
      ModEvents.GameStartDone.RegisterHandler(RepairWrenchStart);
      ModEvents.WorldShuttingDown.RegisterHandler(RepairWrenchStop);
    }

    public void RepairWrenchStart()
    {
      RepairConfig.Load(Path.Combine(ModManager.GetMod("VehicleRepairWrench")?.Path, "Configuration.xml"));
      RepairWrenchGlobal.LoadRepairEffect();

      GameManager.Instance.StartCoroutine(UICoroutines.OnPlayerLoggedIn());
    }

    public void RepairWrenchStop()
    {
      GameManager.Instance.StopCoroutine(UICoroutines.OnPlayerLoggedIn());
     UIStatic.PlayerLoaded = false;
    }
  }
}