using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Android suspension policy, using temporary controllers and an isolated profile.</summary>
public static class AndroidLifecycleChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

    [MenuItem("Tools/Lindavista/Verify Android lifecycle (Play Mode)")]
    public static void Verify() => Debug.Log(RunRuntime());

    public static string RunRuntime()
    {
        if (!Application.isPlaying || GameFlowController.IsPaused || GameFlowController.IsMatchEnded ||
            BossIntroduction.IsActive || PistolUpgradeShop.IsOpen)
            throw new InvalidOperationException("Run in an active match after the introduction, with the shop closed.");

        int assertions = 0;
        void Check(bool ok, string message)
        {
            assertions++;
            if (!ok) throw new InvalidOperationException("Android lifecycle: " + message);
        }
        FieldInfo flowActive = typeof(GameFlowController).GetField("active", StaticPrivate);
        FieldInfo introActive = typeof(BossIntroduction).GetField("active", StaticPrivate);
        FieldInfo shopActive = typeof(PistolUpgradeShop).GetField("active", StaticPrivate);
        object previousFlow = flowActive.GetValue(null);
        object previousIntro = introActive.GetValue(null);
        object previousShop = shopActive.GetValue(null);
        float previousScale = Time.timeScale;
        bool previousAudio = AudioListener.pause;
        GameObject previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        string directory = Path.Combine("Temp", "AndroidLifecycleChecks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        GameObject host = null, introHost = null, shopHost = null;
        using (ProgressionService.UseVerificationRepository(new ProgressionRepository(Path.Combine(directory, "profile.json"))))
        try
        {
            MobileControlsHUD.CancelActiveTouches();
            host = new GameObject("Android Lifecycle Verification");
            var flow = host.AddComponent<GameFlowController>();
            introHost = new GameObject("Introduction State Verification");
            introHost.SetActive(false);
            var intro = introHost.AddComponent<BossIntroduction>();
            shopHost = new GameObject("Shop State Verification");
            shopHost.SetActive(false);
            var shop = shopHost.AddComponent<PistolUpgradeShop>();

            Time.timeScale = .5f;
            AudioListener.pause = false;
            Set(flow, "applicationUnfocused", true);
            Call(flow, "UpdateApplicationSuspension");
            Check(GameFlowController.IsPaused && Time.timeScale == 0f && AudioListener.pause,
                "ordinary gameplay opens pause and freezes audio/time");
            Set(flow, "applicationSuspended", true);
            Set(flow, "applicationUnfocused", false);
            Call(flow, "UpdateApplicationSuspension");
            Check(GameFlowController.IsPaused && Time.timeScale == 0f,
                "focus alone cannot resume a suspended match");
            Set(flow, "applicationSuspended", false);
            Call(flow, "UpdateApplicationSuspension");
            Check(GameFlowController.IsPaused, "returning leaves the match paused for the player");
            flow.ResumeGame();
            Check(Time.timeScale == .5f && !AudioListener.pause, "manual resume restores the original cadence/audio");

            introActive.SetValue(null, intro);
            Set(intro, "presenting", true);
            Time.timeScale = .65f;
            Set(flow, "applicationUnfocused", true);
            Call(flow, "UpdateApplicationSuspension");
            Check((bool)Get(flow, "suspensionFrozen") && !GameFlowController.IsPaused &&
                Time.timeScale == 0f && AudioListener.pause, "dialogue freezes without covering its continue controls");
            Check((bool)Get(flow, "pauseAfterIntroduction"), "dialogue schedules a later visible pause");
            Set(flow, "applicationSuspended", true);
            Call(flow, "UpdateApplicationSuspension");
            Set(flow, "applicationUnfocused", false);
            Call(flow, "UpdateApplicationSuspension");
            Check(Time.timeScale == 0f && AudioListener.pause, "dual lifecycle callbacks do not restore too early");
            Set(flow, "applicationSuspended", false);
            Call(flow, "UpdateApplicationSuspension");
            Check(!(bool)Get(flow, "suspensionFrozen") && Time.timeScale == .65f && !AudioListener.pause,
                "returning restores the exact pre-dialogue suspension state");
            Call(flow, "Update");
            Check(!GameFlowController.IsPaused, "dialogue remains visible until it completes");
            Set(intro, "presenting", false);
            introActive.SetValue(null, null);
            Call(flow, "Update");
            Check(GameFlowController.IsPaused && Time.timeScale == 0f && AudioListener.pause &&
                !(bool)Get(flow, "pauseAfterIntroduction"), "dialogue completion opens the queued pause once");
            flow.ResumeGame();
            Check(Time.timeScale == .65f && !AudioListener.pause, "queued dialogue pause resumes correctly");

            shopActive.SetValue(null, shop);
            Time.timeScale = .8f;
            AudioListener.pause = true;
            Set(flow, "applicationSuspended", true);
            Call(flow, "UpdateApplicationSuspension");
            Check((bool)Get(flow, "suspensionFrozen") && !GameFlowController.IsPaused && Time.timeScale == 0f,
                "an open shop also freezes while suspended");
            Set(flow, "applicationSuspended", false);
            Call(flow, "UpdateApplicationSuspension");
            Call(flow, "Update");
            Check(!GameFlowController.IsPaused && Time.timeScale == .8f && AudioListener.pause,
                "an open shop restores the previous audio state and keeps its close controls available");
            shopActive.SetValue(null, null);
            Call(flow, "Update");
            Check(GameFlowController.IsPaused && Time.timeScale == 0f, "closing the shop exposes the queued pause");
            flow.ResumeGame();
            Check(Time.timeScale == .8f && AudioListener.pause, "shop pause preserves an already-paused audio listener");

            introActive.SetValue(null, intro);
            Set(intro, "presenting", true);
            Time.timeScale = .75f;
            AudioListener.pause = false;
            Set(flow, "applicationSuspended", true);
            Call(flow, "UpdateApplicationSuspension");
            UnityEngine.Object.DestroyImmediate(host);
            host = null;
            Check(Time.timeScale == .75f && !AudioListener.pause,
                "destroying a suspended controller does not leave the next scene frozen");
            return "ANDROID_LIFECYCLE_OK: " + assertions + " assertions; temporary profile, original scene preserved.";
        }
        finally
        {
            introActive.SetValue(null, null);
            shopActive.SetValue(null, null);
            if (host != null) UnityEngine.Object.DestroyImmediate(host);
            if (introHost != null) UnityEngine.Object.DestroyImmediate(introHost);
            if (shopHost != null) UnityEngine.Object.DestroyImmediate(shopHost);
            flowActive.SetValue(null, previousFlow);
            introActive.SetValue(null, previousIntro);
            shopActive.SetValue(null, previousShop);
            Time.timeScale = previousScale;
            AudioListener.pause = previousAudio;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(previousSelection);
            Directory.Delete(directory, true);
        }
    }

    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    private static void Call(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);
}
