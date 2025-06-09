using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;
using System.IO;
using System.CodeDom;

namespace RepairWrench
{
  public static class RepairWrenchGlobal
  {
    public static bool IsFadeWindowRunning = false;
    public static GameObject RepairEffectPrefab;
    public static Dictionary<int, GameObject> ActiveRepairEffects = new Dictionary<int, GameObject>();
    public static void RunVehicleRepair(ItemActionData _actionData, bool _bReleased)
    {
      int greaseMonkeyProgression = GameManager.Instance.myEntityPlayerLocal.Progression.GetProgressionValue("perkGreaseMonkey").level; // (1-5)
      bool vehicleFound = false;
      EntityAlive Player = GameManager.Instance.myEntityPlayerLocal;
      List<EntityAlive> EntitiesInBounds = GameManager.Instance.World.GetLivingEntitiesInBounds(_actionData.invData.holdingEntity, new Bounds(_actionData.invData.holdingEntity.position, Vector3.one * 2f * RepairConfig.RepairRange));

      EntitiesInBounds.ForEach(entityFound =>
      {
        if (entityFound != null && entityFound is EntityVehicle vehicle)
        {
          if (vehicleFound == false) vehicleFound = true;

          if (VehiclesBeingHealed.ContainsKey(entityFound.entityId))
          {
            DisplayStatusMessage("Vehicle is under repairs");
            return;
          }

          if (!VehicleNeedsRepairs(entityFound.Health, entityFound.GetMaxHealth()))
          {
            DisplayStatusMessage("Vehicle doesn't need repairs");
            return;
          }

          bool consumedResources = ConsumeRepairResources(false, vehicleFound);
          if (consumedResources)
          {
            entityFound.AddHealth(CalculateRepairAmount(entityFound));
            AttachRepairEffect(entityFound, false);
            PlayWrenchingAnimation();
          }
        }
      });
    }

    public static void RunVehicleRepairOT(ItemActionData _actionData, bool _bReleased)
    {
      int greaseMonkeyProgression = GameManager.Instance.myEntityPlayerLocal.Progression.GetProgressionValue("perkGreaseMonkey").level; // (1-5)
      bool vehicleFound = false;
      EntityAlive player = GameManager.Instance.myEntityPlayerLocal;
      List<EntityAlive> EntitiesInBounds = GameManager.Instance.World.GetLivingEntitiesInBounds(_actionData.invData.holdingEntity, new Bounds(_actionData.invData.holdingEntity.position, Vector3.one * 2f * RepairConfig.RepairRange));


      EntitiesInBounds.ForEach(entityFound =>
      {
        if (entityFound != null && entityFound is EntityVehicle vehicle)
        {
          if (vehicleFound == false) vehicleFound = true;

          if (VehiclesBeingHealed.ContainsKey(entityFound.entityId))
          {
            DisplayStatusMessage("Vehicle is under repairs");
            return;
          }

          if(!VehicleNeedsRepairs(entityFound.Health, entityFound.GetMaxHealth()))
          {
            DisplayStatusMessage("Vehicle doesn't need repairs");
            return;
          }

          bool consumedResources = ConsumeRepairResources(false, vehicleFound);
          if (consumedResources == false) return;

          GameObject healerGO = new GameObject($"VehicleHealer_{entityFound.entityId}");
          HealVehicleOT healer = healerGO.AddComponent<HealVehicleOT>();
          VehiclesBeingHealed[entityFound.entityId] = healerGO;
          healer.StartCoroutine(healer.Heal(entityFound, entityFound.entityId));
          AttachRepairEffect(entityFound, true);
          PlayWrenchingAnimation();
        }
      });
    }

    public static int CalculateRepairAmount(EntityAlive entityFound)
    {
      int healthRemaining = Mathf.Clamp(entityFound.GetMaxHealth() - entityFound.Health, 0, int.MaxValue);
      int quality = GameManager.Instance.myEntityPlayerLocal.inventory.holdingItemItemValue.Quality;
      int greaseMonkey = GameManager.Instance.myEntityPlayerLocal.Progression.GetProgressionValue("perkGreaseMonkey").level;

      if (RepairConfig.RepairByPercentage)
      {
        float qualityPercent = quality * RepairConfig.RepairQualityMultiplier * RepairConfig.RepairPercentage;
        float perkPercent = greaseMonkey * RepairConfig.RepairPercentage;
        float totalPercent = Mathf.Clamp01(qualityPercent + perkPercent);

        int percentRepair = Mathf.RoundToInt(entityFound.GetMaxHealth() * totalPercent);
        return Mathf.Min(percentRepair, healthRemaining);
      }
      else
      {
        int baseAmount = RepairConfig.RepairAmountBase;
        int qualityBonus = quality * RepairConfig.RepairPerQualityFlat;
        int perkBonus = greaseMonkey * RepairConfig.RepairPerPerkFlat;

        int flatRepair = baseAmount + qualityBonus + perkBonus;

        return Mathf.Min(flatRepair, healthRemaining);
      }
    }

    public static bool ConsumeRepairResources(bool healOverTime, bool vehicleFound)
    {
      List<InventoryBagItem> inventoryItems = new List<InventoryBagItem>();
      List<InventoryBagItem> bagItems = new List<InventoryBagItem>();
      EntityAlive player = GameManager.Instance.myEntityPlayerLocal;
      ItemStack[] invSlots = player.inventory.GetSlots();
      ItemStack[] bagSlots = player.bag.GetSlots();
      ItemValue itemValue = null;
      int invCountTotal = 0;
      int bagCountTotal = 0;
      int consumableAmountRequired = healOverTime ? RepairConfig.RepairHOTConsumableAmount : RepairConfig.RepairConsumableAmount;
      string consumableName = healOverTime ? RepairConfig.RepairHOTConsumableItemName : RepairConfig.RepairConsumableItemName;

      for (int i = 0; i < invSlots.Length; i++)
      {
        if (invSlots[i].itemValue?.ItemClass?.Name != consumableName) continue;
        inventoryItems.Add(new InventoryBagItem() { Name = consumableName, Index = i, ItemValue = invSlots[i].itemValue });
        if (itemValue == null) itemValue = invSlots[i].itemValue;
      }

      for (int x = 0; x < bagSlots.Length; x++)
      {
        if (bagSlots[x].itemValue?.ItemClass?.Name != consumableName) continue;
        bagItems.Add(new InventoryBagItem() { Name = consumableName, Index = x, ItemValue = bagSlots[x].itemValue });
        if (itemValue == null) itemValue = bagSlots[x].itemValue;
      }

      invCountTotal = itemValue == null ? 0 : player.inventory.GetItemCount(itemValue);
      bagCountTotal = itemValue == null ? 0 : player.bag.GetItemCount(itemValue);


      if(itemValue == null || (invCountTotal + bagCountTotal) < consumableAmountRequired)
      {
        // Missing required items
        DisplayStatusMessage("Missing required resources");
        return false;
      }
        
      PlayerItems.RemoveItems(new List<ItemStack>() { new ItemStack(itemValue, 1) }, consumableAmountRequired);
      GameManager.Instance.PlaySoundAtPositionServer(player.position, "VehicleRepairWrenchSound", AudioRolloffMode.Linear, 20);
      return true;
    }

    public static Dictionary<int, GameObject> VehiclesBeingHealed = new Dictionary<int, GameObject>();

    public static XUiM_PlayerInventory PlayerItems;
    public static void PlayWrenchingAnimation()
    {
      GameManager.Instance.myEntityPlayerLocal.StartHarvestingAnim();
      GameManager.Instance.myEntityPlayerLocal.FireEvent(MinEventTypes.onSelfPrimaryActionStart, true);
    }

    public static bool VehicleNeedsRepairs(int currentHealth, int maxHealth)
    {
      return currentHealth < maxHealth ? true : false;
    }
    public static void DisplayStatusMessage(string message)
    {
      ((XUiV_Label)UIStatic.Labels.Find(x => x.id == "repairWrenchStatusLabel")).SetTextImmediately(message);

      if (RepairConfig.PlayStatusMessageSFX) GameManager.Instance.PlaySoundAtPositionServer(GameManager.Instance.myEntityPlayerLocal.position, "VehicleRepairWrenchError", AudioRolloffMode.Linear, 20);
      if (!RepairConfig.DisplayMessagesInUI) return;

      if (UIStatic.ActiveUICoroutine != null)
      {
        GameManager.Instance.StopCoroutine(UIStatic.ActiveUICoroutine);
        UIStatic.ActiveUICoroutine = null;
      }

      UIStatic.ActiveUICoroutine = GameManager.Instance.StartCoroutine(
          UICoroutines.ShowAndAutoHide(
              (XUiV_Window)UIStatic.WindowRepairWrenchMessages,
              0.2f,   // fade in time
              4f,     // delay visible
              1f      // fade out time
          )
      );
    }
    public static void LoadRepairEffect()
    {
      string bundlePath = Path.Combine(ModManager.GetMod("VehicleRepairWrench")?.Path, "Resources/HealingRegenPrefab.unity3d");

      if (!File.Exists(bundlePath))
      {
        Log.Warning($"HealingRegenPrefab.unity3d not found: {bundlePath}");
        return;
      }

      AssetBundle assetBundle = AssetBundle.LoadFromFile(bundlePath);
      if (assetBundle == null)
      {
        Log.Warning("Failed to load HealingRegenPrefab.unity3d.");
        return;
      }

      RepairEffectPrefab = assetBundle.LoadAsset<GameObject>("HealingRegenModel");

      if (RepairEffectPrefab == null)
      {
        Log.Warning("Failed to load 'HealingRegenModel' prefab from bundle.");
      }
      else
      {
        Log.Out("Successfully loaded HealingRegenModel particle prefab.");
      }
    }
    public static void AttachRepairEffect(EntityAlive entityFound, bool isHealOverTime)
    {
      if (RepairEffectPrefab != null)
      {
        int duration = isHealOverTime ? (int)Math.Round((float)RepairConfig.RepairHOTTicks * RepairConfig.RepairHOTTickTime) : 3;
        float sizeMultiplier = 1;

        switch(entityFound.GetType().ToString())
        {
          case "EntityBicycle":
          case "EntityMinibike":
            sizeMultiplier = 3;
            break;
          case "EntityMotorcycle":
            sizeMultiplier = 2;
            break;
          case "EntityVJeep":
          case "EntityVGyroCopter":
          case "EntityVHelicopter":
          case "EntityVBlimp":
            sizeMultiplier = 1;
            break;
          default:
            sizeMultiplier = 1;
            break;
        }

        GameObject effect = GameObject.Instantiate(RepairEffectPrefab);
        effect.transform.SetParent(entityFound.transform);
        effect.transform.localPosition = Vector3.zero;
        effect.transform.localRotation = Quaternion.identity;

        Bounds bounds = entityFound.getBoundingBox();
        float scaleFactor = Mathf.Clamp(bounds.size.magnitude / sizeMultiplier, 1f, 10f);
        effect.transform.localScale = Vector3.one * scaleFactor;

        GameObject.Destroy(effect, duration);
      }
    }
  }

  public static class RepairConfig
  {
    public static bool RepairRequiresConsumable { get; private set; }
    public static bool RepairByPercentage { get; private set; }
    public static bool RepairParticlesEnabled { get; private set; }
    public static bool RepairWrenchDurabilityEnabled { get; private set; }
    public static bool RepairHealOverTimeEnabled { get; private set; }
    public static bool RepairHealOverTimePercentage {  get; private set; }
    public static bool DisplayMessagesInUI { get; private set; }
    public static bool PlayStatusMessageSFX { get; private set; }

    public static int RepairAmountBase { get; private set; }
    public static float RepairPercentage { get; private set; }
    public static string RepairConsumableItemName { get; private set; }
    public static int RepairConsumableAmount { get; private set; }
    public static float RepairRange { get; private set; }
    public static float RepairQualityMultiplier { get; private set; }
    public static int RepairPerQualityFlat {  get; private set; }
    public static int RepairPerPerkFlat {  get; private set; }
    public static int RepairHOTTicks { get; private set; }
    public static float RepairHOTTickTime { get; private set; }
    public static float RepairHOTPercentage { get; private set; }
    public static int RepairHOTFlat {  get; private set; }
    public static string RepairHOTConsumableItemName { get; private set; }
    public static int RepairHOTConsumableAmount { get;private set; }

    public static void Load(string path)
    {
      var doc = XDocument.Load(path);
      var settings = new Dictionary<string, string>();

      foreach (var setting in doc.Descendants("setting"))
      {
        var key = setting.Attribute("key")?.Value;
        var value = setting.Value;
        if (!string.IsNullOrEmpty(key))
        {
          settings[key] = value;
        }
      }

      RepairRequiresConsumable = bool.Parse(settings["RepairRequiresConsumable"].Trim());
      RepairByPercentage = bool.Parse(settings["RepairByPercentage"].Trim());
      RepairParticlesEnabled = bool.Parse(settings["RepairParticlesEnabled"].Trim());
      RepairWrenchDurabilityEnabled = bool.Parse(settings["RepairWrenchDurabilityEnabled"].Trim());
      RepairHealOverTimeEnabled = bool.Parse(settings["RepairHealOverTimeEnabled"].Trim());
      RepairHealOverTimePercentage = bool.Parse(settings["RepairHealOverTimePercentage"].Trim());
      DisplayMessagesInUI = bool.Parse(settings["DisplayMessagesInUI"].Trim());
      PlayStatusMessageSFX = bool.Parse(settings["PlayStatusMessageSFX"].Trim());

      RepairAmountBase = int.Parse(settings["RepairAmountBase"].Trim());
      RepairPercentage = float.Parse(settings["RepairPercentage"].Trim());
      RepairConsumableItemName = settings["RepairConsumableItemName"].Trim();
      RepairConsumableAmount = int.Parse(settings["RepairConsumableAmount"].Trim());
      RepairRange = int.Parse(settings["RepairRange"].Trim());
      RepairQualityMultiplier = float.Parse(settings["RepairQualityMultiplier"].Trim());
      RepairPerQualityFlat = int.Parse(settings["RepairPerQualityFlat"].Trim());
      RepairPerPerkFlat = int.Parse(settings["RepairPerPerkFlat"].Trim());
      RepairHOTTicks = int.Parse(settings["RepairHOTTicks"].Trim());
      RepairHOTTickTime = float.Parse(settings["RepairHOTTickTime"].Trim());
      RepairHOTPercentage = float.Parse(settings["RepairHOTPercentage"].Trim());
      RepairHOTFlat = int.Parse(settings["RepairHOTFlat"].Trim());
      RepairHOTConsumableItemName = settings["RepairHOTConsumableItemName"].Trim();
      RepairHOTConsumableAmount = int.Parse(settings["RepairHOTConsumableAmount"].Trim());
    }
  }

  public class InventoryBagItem
  {
    public string Name { get; set; }
    public int Index { get; set; }
    public ItemValue ItemValue { get; set; }
  }

  public class ItemActionRepairVehicle : ItemActionDynamicMelee
  {
    public override void ExecuteAction(ItemActionData _actionData, bool _bReleased)
    {
      ItemActionDynamicMelee.ItemActionDynamicMeleeData itemActionDynamicMeleeData = _actionData as ItemActionDynamicMelee.ItemActionDynamicMeleeData;
      if (_bReleased)
      {
        itemActionDynamicMeleeData.HasReleased = true;
        this.SetAttackFinished(itemActionDynamicMeleeData);
        itemActionDynamicMeleeData.HasExecuted = false;
        return;
      }
      if (!this.canStartAttack(itemActionDynamicMeleeData))
      {
        itemActionDynamicMeleeData.HasReleased = false;
        return;
      }
      if (itemActionDynamicMeleeData.HasExecuted)
      {
        this.SetAttackFinished(itemActionDynamicMeleeData);
        itemActionDynamicMeleeData.HasExecuted = false;
      }
      itemActionDynamicMeleeData.lastUseTime = Time.time;
      itemActionDynamicMeleeData.lastWeaponHeadPosition = Vector3.zero;
      itemActionDynamicMeleeData.lastWeaponHeadPositionDebug = Vector3.zero;
      itemActionDynamicMeleeData.lastClipPercentage = -1f;
      itemActionDynamicMeleeData.alreadyHitEnts.Clear();
      itemActionDynamicMeleeData.alreadyHitBlocks.Clear();
      itemActionDynamicMeleeData.EventParms.Self = itemActionDynamicMeleeData.invData.holdingEntity;
      itemActionDynamicMeleeData.EventParms.Other = null;
      itemActionDynamicMeleeData.EventParms.ItemActionData = itemActionDynamicMeleeData;
      itemActionDynamicMeleeData.EventParms.ItemValue = itemActionDynamicMeleeData.invData.itemValue;
      _actionData.invData.holdingEntity.MinEventContext.Other = null;
      for (int i = 0; i < ItemActionDynamic.DebugDisplayHits.Count; i++)
      {
        UnityEngine.Object.DestroyImmediate(ItemActionDynamic.DebugDisplayHits[i]);
      }
      ItemActionDynamic.DebugDisplayHits.Clear();
      ItemActionAttack.AttackHitInfo attackHitInfo;
      itemActionDynamicMeleeData.IsHarvesting = this.checkHarvesting(_actionData, out attackHitInfo);
      EntityAlive holdingEntity = _actionData.invData.holdingEntity;
      AvatarController avatarController = holdingEntity.emodel.avatarController;
      avatarController.UpdateBool(AvatarController.harvestingHash, itemActionDynamicMeleeData.IsHarvesting, true);
      avatarController.UpdateBool("IsHarvesting", itemActionDynamicMeleeData.IsHarvesting, true);
      avatarController.UpdateInt(AvatarController.itemActionIndexHash, itemActionDynamicMeleeData.indexInEntityOfAction, true);
      string soundStart = this.soundStart;
      if (soundStart != null && !itemActionDynamicMeleeData.IsHarvesting)
      {
        //holdingEntity.PlayOneShot(soundStart, false, false, false);
      }
      if (!this.UsePowerAttackAnimation)
      {
        if (!itemActionDynamicMeleeData.IsHarvesting)
        {
          
        }
        else
        {
          
        }
      }
      else
      {
        //avatarController.TriggerEvent("PowerAttack");
      }
      if (!this.UsePowerAttackTriggers)
      {
        RepairWrenchGlobal.RunVehicleRepair(_actionData, false);
      }
      else
      {
        RepairWrenchGlobal.RunVehicleRepairOT(_actionData, false);
      }
      EntityPlayerLocal entityPlayerLocal = holdingEntity as EntityPlayerLocal;
      if (entityPlayerLocal != null && entityPlayerLocal.movementInput.lastInputController)
      {
        entityPlayerLocal.MoveController.FindCameraSnapTarget(eCameraSnapMode.MeleeAttack, this.Range + 1f);
      }
      itemActionDynamicMeleeData.Attacking = true;
      itemActionDynamicMeleeData.HasExecuted = true;
    }

    public override bool Raycast(ItemActionDynamic.ItemActionDynamicData _actionData)
    {
      return false;
    }

    public override bool GrazeCast(ItemActionDynamic.ItemActionDynamicData _actionData, float normalizedClipTime = -1f)
    {
      return false;
    }
  }

  [HarmonyPatch(typeof(Entity), nameof(Entity.PlayOneShot), typeof(string), typeof(bool), typeof(bool), typeof(bool))]
  public class Patch_Entity_PlayOneShot
  {
    static bool Prefix(Entity __instance, string clipName, bool sound_in_head, bool serverSignalOnly, bool isUnique)
    {
      if (!string.IsNullOrEmpty(clipName) && clipName.ToLower().Contains("wrench_harvest")) return false;

      return true;
    }
  }

  public class HealVehicleOT : MonoBehaviour
  {
    public IEnumerator Heal(EntityAlive vehicle, int entityId)
    {
      for (int i = 0; i <= RepairConfig.RepairHOTTicks; i++)
      {
        if (vehicle == null || vehicle.IsDead())
        {
          Log.Warning($"Vehicle {entityId} no longer valid or is dead.");
          break;
        }

        float currentHealth = vehicle.Health;
        float maxHealth = vehicle.GetMaxHealth();

        float healAmount = 0f;

        if (RepairConfig.RepairHealOverTimePercentage)
        {
          healAmount = maxHealth * RepairConfig.RepairHOTPercentage;
        } else
        {
          healAmount = RepairConfig.RepairHOTFlat;
        }

        float newHealth = Mathf.Min(currentHealth + healAmount, maxHealth);
        vehicle.Health = (int)newHealth;

        yield return new WaitForSeconds(RepairConfig.RepairHOTTickTime);

        if (Mathf.Approximately(newHealth, maxHealth)) break;
      }

      if (RepairWrenchGlobal.VehiclesBeingHealed.ContainsKey(entityId)) RepairWrenchGlobal.VehiclesBeingHealed.Remove(entityId);
      Destroy(this.gameObject);
    }
  }
}
