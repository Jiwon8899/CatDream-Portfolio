using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

[Category("ExternalSkillVFX")]
public sealed class BlenderSkillVfxPlayModeTests
{
    [UnityTest]
    public IEnumerator PublicKeyboardAndMouseFlow_PlaysAllEightExternalEffects()
    {
        PlayerClass originalClass = PlayerClassSelection.Current;
        UpgradeDefinition bodyUpgrade = UpgradeDatabase.Get("body_size");
        int originalBodyLevel = bodyUpgrade != null ? bodyUpgrade.level : 0;
        int originalTutorial = PlayerPrefs.GetInt("CatChaos.CatHouseTutorialCompleted", 0);
        int originalIntro = PlayerPrefs.GetInt("CatChaos.IntroWatched", 0);
        Camera evidenceCamera = null;
        Camera originalCamera = null;
        PlayerPrefs.SetInt("CatChaos.CatHouseTutorialCompleted", 1);
        PlayerPrefs.SetInt("CatChaos.IntroWatched", 1);
        PlayerPrefs.Save();
        // Load an authored story stage directly. SceneLoader intentionally debounces
        // repeated gameplay requests, but that static debounce can survive between PlayMode
        // test runs when domain reload is disabled and leave the test initializer scene active.
        AsyncOperation stageLoad = SceneManager.LoadSceneAsync(
            SceneLoader.Stage1SceneName, LoadSceneMode.Single);
        Assert.NotNull(stageLoad, "The authored story stage must be loadable for validation.");
        while (!stageLoad.isDone) yield return null;
        yield return WaitForScene(SceneLoader.Stage1SceneName, 30f);
        GameManager manager = Object.FindObjectOfType<GameManager>();
        Assert.NotNull(manager, "The authored story stage requires GameManager.");
        if (manager.CurrentState != GameState.Playing)
        {
            manager.StartStageInCurrentScene(1);
            yield return null;
        }
        if (manager.IsStageOpeningActive)
        {
            manager.RequestSkipStageOpening();
        }
        yield return WaitFor(() => manager.CurrentState == GameState.Playing
            && !manager.IsStageOpeningActive, 12f, "story stage must finish opening");
        GameObject player = GameObject.Find("CatPlayer");
        Assert.NotNull(player, "The authored stage player is required for public-input validation.");
        CatController controller = player.GetComponent<CatController>();
        CatSkillEffectRuntime mana = player.GetComponent<CatSkillEffectRuntime>();
        Assert.NotNull(controller);
        Assert.NotNull(mana);
        controller.SetAutoControl(false);
        controller.SetManualInputEnabled(true);
        CursorStateDirector.ApplyNow("blender_skill_vfx_public_input_ready");
        yield return WaitFor(() => !CursorStateDirector.LastPointerRequired, 2f,
            "gameplay cursor state must release modal input blocking");
        if (bodyUpgrade != null) bodyUpgrade.level = Mathf.Max(3, bodyUpgrade.level);

        HashSet<int> observedCodes = new HashSet<int>();
        try
        {
            evidenceCamera = CreateEvidenceCamera(player.transform, out originalCamera);
            PlayerClassSelection.Current = PlayerClass.Melee;
            PlayerClassRuntime meleeClass = PlayerClassRuntime.Ensure(player, PlayerClass.Melee);
            Assert.NotNull(meleeClass);
            MeleeCatCombatRuntime melee = meleeClass.MeleeCombat;
            Assert.NotNull(melee);
            mana.ResetRuntimeStateForStageStart();
            melee.ResetActionStateForStageStart();
            yield return new WaitForSecondsRealtime(0.45f);
            AssertNaturalMeleeIdlePose(player.transform);
            yield return CaptureIdleEvidence(evidenceCamera, player.transform);
            Assert.IsTrue(controller.enabled, "CatController must run the public key route.");
            Assert.IsTrue(controller.ManualInputEnabledForValidation, "Manual input must be enabled.");
            Assert.IsFalse(controller.AutoControlEnabledForValidation, "Auto control must be disabled.");
            Assert.IsTrue(melee.IsClassActive, "Melee runtime must own class input.");
            Assert.IsTrue(CatSkillUnlocks.IsSkillUnlocked(CatSkillUnlocks.Skill1GroundSlam),
                "Melee skill 1 must be unlocked.");
            Assert.IsTrue(controller.ClassSkillInputContextReadyForValidation,
                "The complete public class-skill input context must be ready.");

            int skill1InputBefore = controller.ClassSkillInputSequenceForValidation;
            int skill1VfxBeforeActivation = BlenderSkillVfxRuntime.SpawnCount;
            yield return PressKey(Key.Digit1);
            yield return WaitFor(() => controller.ClassSkillInputSequenceForValidation > skill1InputBefore,
                1f, "CatController must observe melee skill 1 public key");
            yield return WaitFor(() => !melee.IsActionLocked && melee.EmpoweredSlashReadyForValidation,
                4f, "melee skill 1 must unlock with three empowered attacks primed");
            Assert.AreEqual(skill1VfxBeforeActivation, BlenderSkillVfxRuntime.SpawnCount,
                "Melee skill 1 activation primes attacks; it must not launch all waves immediately.");
            int empoweredBefore = melee.EmpoweredSlashCountForValidation;
            int vfxBefore = BlenderSkillVfxRuntime.SpawnCount;
            yield return PressMouse(MouseButton.Left);
            yield return WaitFor(() => melee.EmpoweredSlashCountForValidation > empoweredBefore
                && BlenderSkillVfxRuntime.SpawnCount > vfxBefore, 2f,
                "melee skill 1 follow-up left click must emit one external-asset wave");
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.AreEqual(ClassSkillEffectPool.MeleeEventBase, BlenderSkillVfxRuntime.LastEventCode);
            observedCodes.Add(ClassSkillEffectPool.MeleeEventBase);
            AssertActiveQualityLayers(ClassSkillEffectPool.MeleeEventBase);
            yield return CaptureEvidence("unity_melee_skill1_external_wave.png");
            yield return CaptureAlternateAngle(evidenceCamera, "unity_melee_skill1_external_wave.png");
            Assert.Greater(melee.EmpoweredSlashCountForValidation, empoweredBefore,
                "The next public left-click after melee skill 1 must launch one empowered wave.");

            mana.RestoreManaForTests(100f);
            yield return PressSkillAndCapture(Key.Digit2, ClassSkillEffectPool.MeleeEventBase + 1,
                "unity_melee_skill2_impact.png", observedCodes, evidenceCamera);
            Assert.That(melee.LastSkillFirstHitElapsed, Is.InRange(0.34f, 0.52f),
                "Melee skill 2 impact VFX and damage must occur on landing, not visibly after it.");
            yield return WaitFor(() => !melee.IsActionLocked, 4f, "melee skill 2 must finish");
            mana.RestoreManaForTests(100f);
            yield return PressSkillAndCapture(Key.Digit3, ClassSkillEffectPool.MeleeEventBase + 2,
                "unity_melee_skill3_spin.png", observedCodes, evidenceCamera);
            yield return WaitFor(() => !melee.IsActionLocked, 4f, "melee skill 3 must finish");
            mana.RestoreManaForTests(100f);
            yield return PressSkillAndCapture(Key.Digit4, ClassSkillEffectPool.MeleeEventBase + 3,
                "unity_melee_skill4_thrust.png", observedCodes, evidenceCamera);
            yield return WaitFor(() => !melee.IsActionLocked, 4f, "melee skill 4 must finish");

            PlayerClassSelection.Current = PlayerClass.Gun;
            PlayerClassRuntime gunClass = PlayerClassRuntime.Ensure(player, PlayerClass.Gun);
            Assert.NotNull(gunClass);
            GunCatCombatRuntime gun = gunClass.GunCombat;
            Assert.NotNull(gun);
            mana.ResetRuntimeStateForStageStart();
            gun.ResetActionStateForStageStart();
            yield return null;

            yield return PressSkillAndCapture(Key.Digit1, ClassSkillEffectPool.GunEventBase,
                "unity_gun_skill1_rapid.png", observedCodes, evidenceCamera);
            yield return WaitFor(() => !gun.IsActionLocked, 4f, "gun skill 1 must finish");
            mana.RestoreManaForTests(100f);
            yield return PressSkillAndCapture(Key.Digit2, ClassSkillEffectPool.GunEventBase + 1,
                "unity_gun_skill2_aimed.png", observedCodes, evidenceCamera);
            yield return WaitFor(() => !gun.IsActionLocked, 4f, "gun skill 2 must finish");
            mana.RestoreManaForTests(100f);
            yield return PressSkillAndCapture(Key.Digit3, ClassSkillEffectPool.GunEventBase + 2,
                "unity_gun_skill3_spin.png", observedCodes, evidenceCamera);
            yield return WaitFor(() => !gun.IsActionLocked, 4f, "gun skill 3 must finish");
            mana.RestoreManaForTests(100f);
            yield return PressSkillAndCapture(Key.Digit4, ClassSkillEffectPool.GunEventBase + 3,
                "unity_gun_skill4_barrage.png", observedCodes, evidenceCamera);
            yield return WaitFor(() => !gun.IsActionLocked, 5f, "gun skill 4 must finish");

            Assert.AreEqual(8, observedCodes.Count, "All eight class-skill event codes must render.");
            Assert.AreEqual(0, BlenderSkillVfxRuntime.ImportedModelCount,
                "Class skill presentation must not load Blender-authored effect meshes.");
            Assert.IsFalse(BlenderSkillVfxRuntime.UsesBlenderAuthoredModels);
            Assert.AreEqual(48, BlenderSkillVfxRuntime.PoolObjectCount);
            Assert.AreEqual(6, BlenderSkillVfxRuntime.HitEffectsPrefabCount,
                "All licensed Hit Effects FREE source prefabs must load from Resources.");
            Assert.AreEqual(10, BlenderSkillVfxRuntime.ExternalTextureCount,
                "All licensed Unity VFX Graph sample textures must be available at runtime.");
            Assert.AreEqual(3, BlenderSkillVfxRuntime.ExternalGpuVfxPrefabCount,
                "The official Unity GPU Sparks, Smoke, and Lightning prefabs must load at runtime.");
            Assert.IsTrue(BlenderSkillVfxRuntime.QualityPostProcessingReady,
                "Skill-only HDR bloom support must be active on the gameplay camera.");
        }
        finally
        {
            if (evidenceCamera != null) Object.Destroy(evidenceCamera.gameObject);
            if (originalCamera != null) originalCamera.enabled = true;
            if (bodyUpgrade != null) bodyUpgrade.level = originalBodyLevel;
            PlayerPrefs.SetInt("CatChaos.CatHouseTutorialCompleted", originalTutorial);
            PlayerPrefs.SetInt("CatChaos.IntroWatched", originalIntro);
            PlayerPrefs.Save();
            PlayerClassSelection.Current = originalClass;
            if (player != null) PlayerClassRuntime.Ensure(player, originalClass);
        }
    }

    private static IEnumerator PressSkillAndCapture(Key key, int expectedCode, string fileName,
        HashSet<int> observedCodes, Camera evidenceCamera)
    {
        int before = BlenderSkillVfxRuntime.SpawnCount;
        CatController controller = Object.FindFirstObjectByType<CatController>();
        Assert.NotNull(controller);
        int inputSequenceBefore = controller.ClassSkillInputSequenceForValidation;
        yield return PressKey(key);
        yield return WaitFor(() => controller.ClassSkillInputSequenceForValidation > inputSequenceBefore,
            1f, "CatController must observe public key " + key);
        if (expectedCode >= ClassSkillEffectPool.MeleeEventBase
            && expectedCode <= ClassSkillEffectPool.MeleeEventBase + 3)
        {
            MeleeCatCombatRuntime runtime = controller.GetComponent<MeleeCatCombatRuntime>();
            CatSkillEffectRuntime mana = controller.GetComponent<CatSkillEffectRuntime>();
            Assert.IsTrue(runtime != null && runtime.IsActionLocked,
                "Melee public key was routed but did not start its skill. state="
                + (runtime != null ? runtime.NetworkState : "missing") + " mana="
                + (mana != null ? mana.CurrentMana.ToString("F1") : "missing") + " cooldown="
                + (runtime != null ? runtime.GetSkillCooldown(expectedCode - ClassSkillEffectPool.MeleeEventBase).ToString("F2") : "missing"));
        }
        else if (expectedCode >= ClassSkillEffectPool.GunEventBase
                 && expectedCode <= ClassSkillEffectPool.GunEventBase + 3)
        {
            GunCatCombatRuntime runtime = controller.GetComponent<GunCatCombatRuntime>();
            CatSkillEffectRuntime mana = controller.GetComponent<CatSkillEffectRuntime>();
            Assert.IsTrue(runtime != null && runtime.IsActionLocked,
                "Gun public key was routed but did not start its skill. state="
                + (runtime != null ? runtime.NetworkState : "missing") + " mana="
                + (mana != null ? mana.CurrentMana.ToString("F1") : "missing") + " cooldown="
                + (runtime != null ? runtime.GetSkillCooldown(expectedCode - ClassSkillEffectPool.GunEventBase).ToString("F2") : "missing"));
        }
        yield return WaitFor(() => BlenderSkillVfxRuntime.SpawnCount > before, 3f,
            "public key " + key + " must spawn external-asset event " + expectedCode);
        Assert.AreEqual(expectedCode, BlenderSkillVfxRuntime.LastEventCode);
        observedCodes.Add(expectedCode);
        AssertActiveQualityLayers(expectedCode);
        float keyFrameDelay = expectedCode == ClassSkillEffectPool.MeleeEventBase
            || expectedCode == ClassSkillEffectPool.GunEventBase ? 0.075f
            : expectedCode == ClassSkillEffectPool.GunEventBase + 3 ? 0.08f : 0.04f;
        yield return new WaitForSecondsRealtime(keyFrameDelay);
        yield return CaptureEvidence(fileName);
        yield return CaptureAlternateAngle(evidenceCamera, fileName);
    }

    private static IEnumerator CaptureAlternateAngle(Camera camera, string primaryFileName)
    {
        if (camera == null) yield break;
        BlenderSkillVfxActor[] actors = System.Array.FindAll(
            Resources.FindObjectsOfTypeAll<BlenderSkillVfxActor>(),
            value => value != null && value.gameObject.activeInHierarchy);
        if (actors.Length == 0) yield break;
        BlenderSkillVfxActor actor = actors[actors.Length - 1];
        Vector3 savedPosition = camera.transform.position;
        Quaternion savedRotation = camera.transform.rotation;
        Vector3 effectPosition = actor.transform.position;
        Vector3 forward = BlenderSkillVfxRuntime.LastSpawnForward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        camera.transform.position = effectPosition - forward * 3.1f + right * 2.25f + Vector3.up * 2.65f;
        camera.transform.LookAt(effectPosition + Vector3.up * 0.28f);
        string angleName = Path.GetFileNameWithoutExtension(primaryFileName)
            + "_angle45" + Path.GetExtension(primaryFileName);
        yield return CaptureEvidence(angleName);
        camera.transform.SetPositionAndRotation(savedPosition, savedRotation);
    }

    private static IEnumerator CaptureIdleEvidence(Camera camera, Transform player)
    {
        if (camera == null || player == null) yield break;
        Vector3 savedPosition = camera.transform.position;
        Quaternion savedRotation = camera.transform.rotation;

        camera.transform.position = player.position - player.forward * 2.55f + Vector3.up * 1.18f;
        camera.transform.LookAt(player.position + Vector3.up * 0.28f);
        yield return CaptureEvidence("unity_melee_idle_natural_front.png");

        camera.transform.position = player.position - player.forward * 2.1f
            + player.right * 1.85f + Vector3.up * 1.16f;
        camera.transform.LookAt(player.position + Vector3.up * 0.3f);
        yield return CaptureEvidence("unity_melee_idle_natural_angle45.png");
        camera.transform.SetPositionAndRotation(savedPosition, savedRotation);
    }

    private static void AssertNaturalMeleeIdlePose(Transform player)
    {
        Transform leftUpperArm = FindBone(player, "LeftUpperArm", "upper_arm.L", "upperarm_l");
        Transform rightUpperArm = FindBone(player, "RightUpperArm", "upper_arm.R", "upperarm_r");
        Transform leftHand = FindBone(player, "LeftHand", "hand.L", "hand.L_end");
        Transform rightHand = FindBone(player, "RightHand", "hand.R", "hand.R_end");
        Assert.NotNull(leftUpperArm, "Melee idle validation requires the left upper-arm bone.");
        Assert.NotNull(rightUpperArm, "Melee idle validation requires the right upper-arm bone.");
        Assert.NotNull(leftHand, "Melee idle validation requires the left hand bone.");
        Assert.NotNull(rightHand, "Melee idle validation requires the right hand bone.");

        float leftDrop = leftUpperArm.position.y - leftHand.position.y;
        float rightDrop = rightUpperArm.position.y - rightHand.position.y;
        Debug.Log("[ExternalSkillVFX][Idle] leftHandDrop=" + leftDrop.ToString("F4")
            + " rightHandDrop=" + rightDrop.ToString("F4"));
        Assert.Greater(leftDrop, 0.02f,
            "Left hand must rest below its shoulder; a horizontal T-pose is not an acceptable idle.");
        Assert.Greater(rightDrop, 0.02f,
            "Right hand and equipped weapon must rest below the shoulder in idle.");
    }

    private static Transform FindBone(Transform root, params string[] aliases)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int aliasIndex = 0; aliasIndex < aliases.Length; aliasIndex++)
        {
            string wanted = NormalizeBoneName(aliases[aliasIndex]);
            for (int i = 0; i < all.Length; i++)
            {
                if (NormalizeBoneName(all[i].name) == wanted) return all[i];
            }
        }
        return null;
    }

    private static string NormalizeBoneName(string value)
    {
        return value.Replace(".", string.Empty).Replace("_", string.Empty)
            .Replace(":", string.Empty).ToLowerInvariant();
    }

    private static void AssertActiveQualityLayers(int expectedCode)
    {
        BlenderSkillVfxActor[] actors = System.Array.FindAll(
            Resources.FindObjectsOfTypeAll<BlenderSkillVfxActor>(),
            value => value != null && value.gameObject.activeInHierarchy);
        Assert.Greater(actors.Length, 0, "An active external effect must exist for code " + expectedCode);
        BlenderSkillVfxActor actor = actors[actors.Length - 1];
        Assert.GreaterOrEqual(actor.GetComponentsInChildren<ParticleSystem>(true).Length, 4,
            "AAA layering requires multiple authored particle systems.");
        Assert.NotNull(actor.transform.Find("Source_HitEffectsFree"));
        Assert.NotNull(actor.transform.Find("Source_UnityVFXGraphSamples"));
        Assert.NotNull(actor.transform.Find("EnergyLightPulse"));
    }

    private static IEnumerator PressKey(Key key)
    {
        // Use an isolated virtual device. Queuing onto the host keyboard can be
        // overwritten by the OS state before CatController.Update reads it.
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
        InputSystem.RemoveDevice(keyboard);
    }

    private static IEnumerator PressMouse(MouseButton button)
    {
        Mouse mouse = InputSystem.AddDevice<Mouse>();
        InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(button));
        yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState());
        yield return null;
        InputSystem.RemoveDevice(mouse);
    }

    private static IEnumerator WaitForScene(string sceneName, float timeout)
    {
        float deadline = Time.realtimeSinceStartup + timeout;
        while (SceneManager.GetActiveScene().name != sceneName && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name);
        yield return null;
    }

    private static IEnumerator WaitFor(System.Func<bool> predicate, float timeout,
        string expectation)
    {
        float deadline = Time.realtimeSinceStartup + timeout;
        while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.IsTrue(predicate(), "Timed out: " + expectation + ".");
    }

    private static IEnumerator CaptureEvidence(string fileName)
    {
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..",
            "ValidationReports", "SkillVFX", "Unity"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, fileName);
        if (File.Exists(path)) File.Delete(path);
        ScreenCapture.CaptureScreenshot(path, 1);
        yield return new WaitForEndOfFrame();
        float deadline = Time.realtimeSinceStartup + 5f;
        while (!File.Exists(path) && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.IsTrue(File.Exists(path), "Screenshot was not written: " + path);
    }

    private static Camera CreateEvidenceCamera(Transform player, out Camera original)
    {
        original = Camera.main;
        if (original != null) original.enabled = false;
        GameObject cameraObject = new GameObject("ExternalSkillVfxEvidenceCamera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        if (original != null) camera.CopyFrom(original);
        camera.depth = 200f;
        camera.fieldOfView = 48f;
        camera.nearClipPlane = 0.03f;
        camera.transform.position = player.position - player.forward * 3.65f
            + player.right * 2.15f + Vector3.up * 4.15f;
        camera.transform.LookAt(player.position + player.forward * 1.25f + Vector3.up * 0.5f);
        return camera;
    }
}
