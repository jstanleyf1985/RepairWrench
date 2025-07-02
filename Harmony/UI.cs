using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RepairWrench
{
  public static class UIStatic
  {
    public static bool PlayerLoaded = false;
    public static List<XUiView> Views;
    public static List<XUiView> Labels;
    public static XUiView WindowRepairWrenchMessages;
    public static bool IsFadeWindowRunning = false;
    public static Coroutine ActiveUICoroutine;
    public static void HideRepairWrenchMessages()
    {
      XUiV_Window craftingMessageWindow = (XUiV_Window)UIStatic.WindowRepairWrenchMessages;
      craftingMessageWindow.TargetAlpha = 0f;
      craftingMessageWindow.ForceHide = true;
      craftingMessageWindow.ForceVisible(0);
      craftingMessageWindow.UpdateData();
    }

  }

  public class UICoroutines : MonoBehaviour
  {
    public static IEnumerator OnPlayerLoggedIn()
    {
      for (var i = 0; i < 9999999; i++)
      {
        if (GameManager.Instance?.myEntityPlayerLocal?.entityId != -1 && GameManager.Instance?.myEntityPlayerLocal?.PlayerUI?.xui?.xuiViewList != null)
        {
          UIStatic.PlayerLoaded = true;
          UIStatic.Views = GameManager.Instance?.myEntityPlayerLocal?.PlayerUI?.xui?.xuiViewList;
          UIStatic.WindowRepairWrenchMessages = UIStatic.Views.Find(x => x.id == "RepairWrenchStatusWindow");
          UIStatic.Labels = UIStatic.Views.FindAll(x => x is XUiV_Label);
          UIStatic.HideRepairWrenchMessages();
          RepairWrenchGlobal.PlayerItems = new XUiM_PlayerInventory(GameManager.Instance.myEntityPlayerLocal.playerUI.xui, GameManager.Instance.myEntityPlayerLocal);

          GameManager.Instance.StartCoroutine(FadeWindow((XUiV_Window)UIStatic.WindowRepairWrenchMessages, 1f, false));

          // Fix for game bug if adding mod to an existing game where player has already progressed, remains unlocked
          var harvestingToolsCheck = GameManager.Instance.myEntityPlayerLocal?.Progression;
          if (harvestingToolsCheck != null)
          {
            int harvestingToolsLevel = harvestingToolsCheck.GetProgressionValue("craftingHarvestingTools").level;
            if(harvestingToolsLevel > 1) GameManager.Instance.myEntityPlayerLocal.SetCVar("meleeToolRepairingWrench", 1);
          }

          GameManager.Instance?.StopCoroutine("OnPlayerLoggedIn");
          break;
        }

        yield return new WaitForSeconds(1f);
      }
    }

    public static IEnumerator FadeWindow(XUiV_Window window, float duration, bool fadeIn)
    {
      float interval = 0.02f;
      float startAlpha = window.TargetAlpha;
      float endAlpha = fadeIn ? 1f : 0f;
      float time = 0f;

      while (time < duration)
      {
        time += interval;
        float t = Mathf.Clamp01(time / duration);
        window.TargetAlpha = Mathf.Lerp(startAlpha, endAlpha, t);
        window.UpdateData();
        yield return new WaitForSeconds(interval);
      }

      window.TargetAlpha = endAlpha;
      window.UpdateData();
    }

    public static IEnumerator FadeOutAfterDelay(XUiV_Window window, float delaySeconds, float fadeSeconds)
    {
      yield return new WaitForSeconds(delaySeconds);

      if (UIStatic.IsFadeWindowRunning == false)
      {
        UIStatic.IsFadeWindowRunning = true;
        GameManager.Instance.StartCoroutine(FadeWindow(window, fadeSeconds, false));
      }
    }

    public static IEnumerator ShowAndAutoHide(XUiV_Window window, float fadeInSeconds, float delaySeconds, float fadeOutSeconds)
    {
      window.ForceHide = false;
      window.ForceVisible(1);
      window.OnOpen();

      yield return FadeWindow(window, fadeInSeconds, true);
      yield return new WaitForSeconds(delaySeconds);
      yield return FadeWindow(window, fadeOutSeconds, false);

      window.ForceHide = true;
      window.ForceVisible(0);
      window.UpdateData();

      UIStatic.ActiveUICoroutine = null;
    }

    public static IEnumerator WaitSeconds(float seconds, IEnumerator coroutineToRunOnComplete)
    {
      for (var i = 0; i < seconds; i++)
      {
        yield return new WaitForSeconds(1f);
      }

      if (UIStatic.IsFadeWindowRunning == false)
      {
        UIStatic.IsFadeWindowRunning = true;
        GameManager.Instance.StartCoroutine(coroutineToRunOnComplete);
      }

      GameManager.Instance.StopCoroutine("WaitSeconds");
    }

    [HarmonyPatch(typeof(EffectManager), nameof(EffectManager.GetValue))]
    public class Patch_EffectManager_GetValue
    {
      static void Postfix(
          PassiveEffects _passiveEffect,
          ItemValue _originalItemValue,
          float _originalValue,
          EntityAlive _entity,
          Recipe _recipe,
          FastTags<TagGroup.Global> tags,
          bool calcEquipment,
          bool calcHoldingItem,
          bool calcProgression,
          bool calcBuffs,
          bool calcChallenges,
          int craftingTier,
          bool useMods,
          bool _useDurability,
          ref float __result
      )
      {
        if (_passiveEffect != PassiveEffects.CraftingTier) return;
        if (!tags.Test_AnySet(FastTags<TagGroup.Global>.Parse("meleeToolRepairingWrench"))) return;

        // Fix for game bug if adding mod to an existing game where player has already progressed, remains unlocked
        var harvestingToolsCheck = GameManager.Instance.myEntityPlayerLocal?.Progression;
        if (harvestingToolsCheck != null)
        {
          int harvestingToolsLevel = harvestingToolsCheck.GetProgressionValue("craftingHarvestingTools").level;
          if (harvestingToolsLevel <= 5) __result = 1;
          if (harvestingToolsLevel > 5 && harvestingToolsLevel <= 10) __result = 2;
          if (harvestingToolsLevel > 10 && harvestingToolsLevel <= 15) __result = 3;
          if (harvestingToolsLevel > 15 && harvestingToolsLevel <= 20) __result = 4;
          if (harvestingToolsLevel > 20 && harvestingToolsLevel <= 30) __result = 5;
          if (harvestingToolsLevel > 30) __result = 6;
        }
      }
    }
  }
}
