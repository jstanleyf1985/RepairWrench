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
  }
}
