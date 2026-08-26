using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

[Category("ClassFix30")]
public sealed class ClassFix30FollowupPlayModeTests
{
    private struct IntPref
    {
        public bool existed;
        public int value;
    }

    [UnityTest]
    public IEnumerator ClassUpgradeStorage_IsolatedForEveryUpgradeAndPurchase()
    {
        PlayerClass originalClass = PlayerClassSelection.Current;
        Dictionary<string, IntPref> snapshot = CaptureUpgradePreferences();
        Dictionary<string, int> basicExpected = new Dictionary<string, int>();
        try
        {
            int index = 0;
            foreach (UpgradeDefinition upgrade in UpgradeDatabase.Upgrades)
            {
                if (upgrade == null) continue;
                int level = Mathf.Min(upgrade.maxLevel, 1 + index % 4);
                upgrade.level = level;
                basicExpected[upgrade.id] = level;
                index++;
            }
            SaveSystem.SaveUpgradeLevelsForClass(PlayerClass.Basic);

            SaveSystem.LoadUpgradeLevelsForClass(PlayerClass.Melee, false);
            AssertEveryUpgrade(0, "New Melee save must start at level zero.");
            SaveSystem.LoadUpgradeLevelsForClass(PlayerClass.Gun, false);
            AssertEveryUpgrade(0, "New Gun save must start at level zero.");

            SaveSystem.LoadUpgradeLevelsForClass(PlayerClass.Basic, true);
            AssertUpgradeMap(basicExpected, "Basic levels must survive switching through two other classes.");

            SaveSystem.LoadUpgradeLevelsForClass(PlayerClass.Melee, true);
            UpgradeDefinition purchased = FirstPurchasableUpgrade();
            Assert.NotNull(purchased);
            int currency = int.MaxValue;
            Assert.IsTrue(UpgradeDatabase.TryPurchase(purchased.id, ref currency));
            SaveSystem.SaveUpgradeLevelsForClass(PlayerClass.Melee);
            int meleePurchasedLevel = purchased.level;

            SaveSystem.LoadUpgradeLevelsForClass(PlayerClass.Basic, true);
            AssertUpgradeMap(basicExpected, "Buying a Melee upgrade must not change Basic levels.");
            SaveSystem.LoadUpgradeLevelsForClass(PlayerClass.Melee, true);
            Assert.AreEqual(meleePurchasedLevel, UpgradeDatabase.GetLevel(purchased.id),
                "The purchased Melee level must not snap back after reload.");

            yield return null;
        }
        finally
        {
            RestoreUpgradePreferences(snapshot);
            PlayerClassSelection.Current = originalClass;
            SaveSystem.LoadUpgradeLevelsForClass(originalClass, true);
        }
    }

    [UnityTest]
    public IEnumerator Hats_FollowAllClassesAndBodyGrowthInTheSameFrame()
    {
        PlayerClass originalClass = PlayerClassSelection.Current;
        string originalHat = PlayerPrefs.GetString("CatCosmetic.EquippedSkin", string.Empty);
        UpgradeDefinition body = UpgradeDatabase.Get("body_size");
        int originalBody = body != null ? body.level : 0;
        PlayerPrefs.SetString("CatCosmetic.EquippedSkin", CatSkinIds.Clover);
        PlayerPrefs.Save();
        SceneLoader.LoadMainLobby();
        yield return WaitForScene(SceneLoader.GomyammiHouseSceneName, 25f);
        GameObject player = GameObject.Find("CatPlayer");
        Assert.NotNull(player);
        CatController cat = player.GetComponent<CatController>();
        Assert.NotNull(cat);
        CatSkinAttachment attachment = player.GetComponent<CatSkinAttachment>() ?? player.AddComponent<CatSkinAttachment>();
        try
        {
            foreach (PlayerClass playerClass in new[] { PlayerClass.Basic, PlayerClass.Melee, PlayerClass.Gun })
            {
                PlayerClassSelection.Current = playerClass;
                PlayerClassRuntime.Ensure(player, playerClass);
                for (int pass = 0; pass < 2; pass++)
                {
                    body.level = pass == 0 ? 0 : body.maxLevel;
                    cat.ApplyUpgradeStats();
                    attachment.Apply(CatSkinIds.Clover);
                    yield return new WaitForEndOfFrame();

                    Transform hat = FindDescendant(player.transform, "EquippedHat_" + CatSkinIds.Clover);
                    Assert.NotNull(hat, playerClass + " hat must remain attached after growth changes.");
                    Bounds hatBounds = VisibleBounds(hat);
                    Bounds bodyBounds = ActiveClassBodyBounds(player.transform, playerClass, hat);
                    float ratio = MaxDimension(hatBounds.size) / Mathf.Max(0.001f, bodyBounds.size.y);
                    Assert.That(ratio, Is.InRange(0.30f, 0.34f),
                        playerClass + " hat must scale proportionally with body growth in the same rendered frame.");
                    Assert.That(hat.parent, Is.Not.Null, playerClass + " hat must remain parented to its head socket.");
                    float overlapRatio = (bodyBounds.max.y - hatBounds.min.y) / Mathf.Max(0.001f, bodyBounds.size.y);
                    Assert.That(overlapRatio, Is.InRange(0.04f, 0.12f),
                        playerClass + " hat brim must stay seated on the grown head without floating.");
                }
            }
            yield return new WaitForEndOfFrame();
            body.level = Mathf.Min(4, body.maxLevel);
            cat.ApplyUpgradeStats();
            attachment.Apply(CatSkinIds.Clover);
            yield return new WaitForEndOfFrame();
            Transform evidenceHat = FindDescendant(player.transform, "EquippedHat_" + CatSkinIds.Clover);
            Bounds evidenceBounds = ActiveClassBodyBounds(player.transform, PlayerClass.Gun, evidenceHat);
            evidenceBounds.Encapsulate(VisibleBounds(evidenceHat));
            Camera evidenceCamera = CreateEvidenceCamera(evidenceBounds);
            yield return CaptureEvidence("hat_gun_growth_level4.png");
            Object.Destroy(evidenceCamera.gameObject);
        }
        finally
        {
            body.level = originalBody;
            PlayerPrefs.SetString("CatCosmetic.EquippedSkin", originalHat);
            PlayerPrefs.Save();
            PlayerClassSelection.Current = originalClass;
            PlayerClassRuntime.Ensure(player, originalClass);
            cat.ApplyUpgradeStats();
            attachment.Apply(originalHat);
        }
    }

    [UnityTest]
    public IEnumerator MeleeShield_FirstRightClickOnlyGrabs_SecondRightClickDashes()
    {
        PlayerClass originalClass = PlayerClassSelection.Current;
        IntPref tutorial = CaptureInt("CatChaos.CatHouseTutorialCompleted");
        IntPref intro = CaptureInt("CatChaos.IntroWatched");
        PlayerPrefs.SetInt("CatChaos.CatHouseTutorialCompleted", 1);
        PlayerPrefs.SetInt("CatChaos.IntroWatched", 1);
        PlayerPrefs.Save();
        SceneLoader.LoadMainLobby();
        yield return WaitForScene(SceneLoader.GomyammiHouseSceneName, 25f);
        GameObject player = GameObject.Find("CatPlayer");
        Assert.NotNull(player);
        PlayerClassSelection.Current = PlayerClass.Melee;
        PlayerClassRuntime runtime = PlayerClassRuntime.Ensure(player, PlayerClass.Melee);
        CatController controller = player.GetComponent<CatController>();
        CatCarryThrow carry = player.GetComponent<CatCarryThrow>();
        Assert.NotNull(runtime);
        Assert.NotNull(controller);
        Assert.NotNull(carry);
        controller.SetAutoControl(false);
        controller.SetManualInputEnabled(true);

        GameObject shield = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shield.name = "ClassFix30Shield";
        shield.transform.position = player.transform.position + player.transform.forward * 1.25f + Vector3.up * 0.45f;
        shield.transform.localScale = new Vector3(0.7f, 0.85f, 0.18f);
        BreakableObject breakable = shield.AddComponent<BreakableObject>();
        breakable.maxHealth = breakable.health = breakable.currentHealth = 100000f;
        Rigidbody shieldBody = shield.GetComponent<Rigidbody>() ?? shield.AddComponent<Rigidbody>();
        shieldBody.useGravity = false;
        Physics.SyncTransforms();
        try
        {
            Vector3 beforeGrab = player.transform.position;
            yield return PressMouse(MouseButton.Right);
            yield return WaitFor(() => carry.IsShieldMode, 3f, "First RMB did not enter shield mode.");
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(5, carry.HeldDurability,
                "The RMB press that grabs a shield must not consume dash durability.");
            Assert.That(Vector3.Distance(beforeGrab, player.transform.position), Is.LessThan(0.15f),
                "The RMB press that grabs a shield must not start a dash.");

            Vector3 beforeDash = player.transform.position;
            yield return PressMouse(MouseButton.Right);
            yield return WaitFor(() => runtime.MeleeCombat.NetworkState == "Melee_ShieldDash", 1f,
                "Second RMB did not start the shield dash.");
            Animator dashAnimator = player.GetComponentInChildren<Animator>(false);
            Assert.NotNull(dashAnimator);
            Assert.That(dashAnimator.speed,
                Is.EqualTo(MeleeCatCombatRuntime.ShieldDashAnimationSpeedForValidation).Within(0.05f),
                "Shield dash must visibly play the locomotion animation at a fast rate.");
            Assert.That(dashAnimator.GetFloat("Speed"), Is.GreaterThan(0.6f),
                "Shield dash must drive the walking locomotion blend while moving.");
            yield return new WaitForEndOfFrame();
            yield return CaptureEvidence("melee_shield_fast_walk_mid_dash.png");
            yield return WaitFor(() => carry.HeldDurability == 4, 3f,
                "Shield dash did not finish and consume exactly one durability.");
            Assert.That(Vector3.Distance(beforeDash, player.transform.position), Is.GreaterThan(0.25f));
            yield return new WaitForEndOfFrame();
            yield return CaptureEvidence("melee_shield_second_click_dash.png");
        }
        finally
        {
            carry.DetachShield();
            Object.Destroy(shield);
            PlayerClassSelection.Current = originalClass;
            PlayerClassRuntime.Ensure(player, originalClass);
            RestoreInt("CatChaos.CatHouseTutorialCompleted", tutorial);
            RestoreInt("CatChaos.IntroWatched", intro);
            PlayerPrefs.Save();
        }
    }

    [UnityTest]
    public IEnumerator AmmoPickupIcon_IsTwoMetresAndReadable()
    {
        GameObject source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            MeshFilter filter = source.GetComponent<MeshFilter>();
            Renderer renderer = source.GetComponent<Renderer>();
            GunCatAmmoSlot slot = new GunCatAmmoSlot
            {
                type = "class_fix30_visible_pickup",
                displayName = "검증 탄약",
                maxDimension = 1f,
                mesh = filter.sharedMesh,
                materials = renderer.sharedMaterials
            };
            Sprite icon = GunCatAmmoIconCache.GetOrCreate(slot);
            Assert.NotNull(icon);
            Assert.That(icon.bounds.size.x, Is.EqualTo(GunCatAmmoIconCache.WorldIconSizeForValidation).Within(0.01f));
            Assert.That(icon.bounds.size.y, Is.EqualTo(GunCatAmmoIconCache.WorldIconSizeForValidation).Within(0.01f));
            GameObject visual = new GameObject("ClassFix30AmmoPickup2m");
            visual.layer = 30;
            SpriteRenderer spriteRenderer = visual.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = icon;
            Camera camera = CreateIsolatedSpriteCamera();
            yield return CaptureEvidence("ammo_pickup_2m_world_icon.png");
            Object.Destroy(camera.gameObject);
            Object.Destroy(visual);
        }
        finally
        {
            Object.Destroy(source);
        }
    }

    [UnityTest]
    public IEnumerator TitleSfxAndBgmSliders_DragThroughActualMouseInput()
    {
        float oldSfx = SaveSystem.GetFloatSetting("SfxVolume", 1f);
        float oldBgm = SaveSystem.GetFloatSetting("BgmVolume", 1f);
        SceneLoader.LoadTitle();
        yield return WaitForScene(SceneLoader.TitleSceneName, 20f);
        yield return null;
        Button settings = FindActive<Button>("TitleSettingsButton");
        Assert.NotNull(settings);
        settings.onClick.Invoke();
        yield return null;
        yield return WaitFor(() => GameObject.Find("SettingsPanel") != null && GameObject.Find("SettingsPanel").activeInHierarchy, 5f);
        Canvas.ForceUpdateCanvases();
        Slider sfx = FindActive<Slider>("SfxVolumeSlider");
        Slider bgm = FindActive<Slider>("BgmVolumeSlider");
        Assert.NotNull(sfx);
        Assert.NotNull(bgm);
        Assert.NotNull(sfx.handleRect);
        Assert.NotNull(bgm.handleRect);
        Assert.NotNull(sfx.handleRect.GetComponent<TitleSettingsSliderDragRelay>(),
            "The SFX Handle itself must relay pointer drag events to the Slider.");
        Assert.NotNull(bgm.handleRect.GetComponent<TitleSettingsSliderDragRelay>(),
            "The BGM Handle itself must relay pointer drag events to the Slider.");
        try
        {
            yield return DragSlider(sfx, 0.21f);
            yield return DragSlider(bgm, 0.79f);
            Assert.That(sfx.normalizedValue, Is.EqualTo(0.21f).Within(0.04f));
            Assert.That(bgm.normalizedValue, Is.EqualTo(0.79f).Within(0.04f));
            Assert.That(SaveSystem.GetFloatSetting("SfxVolume", -1f), Is.EqualTo(sfx.value).Within(0.001f));
            Assert.That(SaveSystem.GetFloatSetting("BgmVolume", -1f), Is.EqualTo(bgm.value).Within(0.001f));
            Canvas.ForceUpdateCanvases();
            AssertSliderFillFollowsHandle(sfx);
            AssertSliderFillFollowsHandle(bgm);
            yield return new WaitForEndOfFrame();
            yield return CaptureEvidence("title_actual_mouse_slider_drag.png");
        }
        finally
        {
            SaveSystem.SetFloatSetting("SfxVolume", oldSfx);
            SaveSystem.SetFloatSetting("BgmVolume", oldBgm);
        }
    }

    private static void AssertSliderFillFollowsHandle(Slider slider)
    {
        Assert.NotNull(slider.fillRect, slider.name + " must retain its Fill Rect after later layout passes.");
        Assert.NotNull(slider.handleRect);
        Vector3[] fillCorners = new Vector3[4];
        Vector3[] handleCorners = new Vector3[4];
        slider.fillRect.GetWorldCorners(fillCorners);
        slider.handleRect.GetWorldCorners(handleCorners);
        float fillRight = Mathf.Max(fillCorners[0].x, Mathf.Max(fillCorners[1].x,
            Mathf.Max(fillCorners[2].x, fillCorners[3].x)));
        float handleCenter = (handleCorners[0].x + handleCorners[2].x) * 0.5f;
        Assert.That(fillRight, Is.EqualTo(handleCenter).Within(3f),
            slider.name + " orange fill must end at the current handle position.");
    }

    private static Dictionary<string, IntPref> CaptureUpgradePreferences()
    {
        Dictionary<string, IntPref> result = new Dictionary<string, IntPref>();
        foreach (PlayerClass playerClass in new[] { PlayerClass.Basic, PlayerClass.Melee, PlayerClass.Gun })
        {
            foreach (UpgradeDefinition upgrade in UpgradeDatabase.Upgrades)
            {
                if (upgrade == null) continue;
                string key = UpgradeKey(playerClass, upgrade.id);
                result[key] = new IntPref { existed = PlayerPrefs.HasKey(key), value = PlayerPrefs.GetInt(key, 0) };
            }
        }
        return result;
    }

    private static IntPref CaptureInt(string key)
        => new IntPref { existed = PlayerPrefs.HasKey(key), value = PlayerPrefs.GetInt(key, 0) };

    private static void RestoreInt(string key, IntPref value)
    {
        if (value.existed) PlayerPrefs.SetInt(key, value.value);
        else PlayerPrefs.DeleteKey(key);
    }

    private static void RestoreUpgradePreferences(Dictionary<string, IntPref> snapshot)
    {
        foreach (KeyValuePair<string, IntPref> pair in snapshot)
        {
            if (pair.Value.existed) PlayerPrefs.SetInt(pair.Key, pair.Value.value);
            else PlayerPrefs.DeleteKey(pair.Key);
        }
        PlayerPrefs.Save();
    }

    private static string UpgradeKey(PlayerClass playerClass, string id)
        => "Story.Upgrade.Single." + playerClass + "." + id;

    private static void AssertEveryUpgrade(int expected, string message)
    {
        foreach (UpgradeDefinition upgrade in UpgradeDatabase.Upgrades)
            if (upgrade != null) Assert.AreEqual(expected, upgrade.level, message + " id=" + upgrade.id);
    }

    private static void AssertUpgradeMap(Dictionary<string, int> expected, string message)
    {
        foreach (KeyValuePair<string, int> pair in expected)
            Assert.AreEqual(pair.Value, UpgradeDatabase.GetLevel(pair.Key), message + " id=" + pair.Key);
    }

    private static UpgradeDefinition FirstPurchasableUpgrade()
    {
        foreach (UpgradeDefinition upgrade in UpgradeDatabase.Upgrades)
            if (upgrade != null && upgrade.maxLevel > 0) return upgrade;
        return null;
    }

    private static IEnumerator PressMouse(MouseButton button)
    {
        Mouse mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
        InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(button));
        yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState());
        yield return null;
    }

    private static IEnumerator DragSlider(Slider slider, float target)
    {
        RectTransform rect = slider.transform as RectTransform;
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Canvas canvas = slider.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 left = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
        Vector2 right = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
        float y = (left.y + right.y) * 0.5f;
        Vector2 start = RectTransformUtility.WorldToScreenPoint(camera, slider.handleRect.position);
        Vector2 end = new Vector2(Mathf.Lerp(left.x, right.x, target), y);
        Mouse mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
        InputSystem.QueueStateEvent(mouse, new MouseState { position = start });
        yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = start }.WithButton(MouseButton.Left));
        yield return null;
        for (int i = 1; i <= 8; i++)
        {
            Vector2 position = Vector2.Lerp(start, end, i / 8f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
            yield return null;
        }
        InputSystem.QueueStateEvent(mouse, new MouseState { position = end });
        yield return null;
    }

    private static IEnumerator WaitForScene(string sceneName, float timeout)
    {
        float deadline = Time.realtimeSinceStartup + timeout;
        while (SceneManager.GetActiveScene().name != sceneName && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name);
        yield return null;
    }

    private static IEnumerator WaitFor(System.Func<bool> predicate, float timeout, string message = "Timed out waiting for runtime state.")
    {
        float deadline = Time.realtimeSinceStartup + timeout;
        while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.IsTrue(predicate(), message);
    }

    private static T FindActive<T>(string name) where T : Component
    {
        T[] all = Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++) if (all[i] != null && all[i].name == name) return all[i];
        return null;
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++) if (all[i].name == name) return all[i];
        return null;
    }

    private static Bounds ActiveClassBodyBounds(Transform player, PlayerClass playerClass, Transform hat)
    {
        string rootName = playerClass == PlayerClass.Melee ? "MeleeClassVisual"
            : playerClass == PlayerClass.Gun ? "GunClassVisual" : "CatVisualRoot";
        Transform root = player.Find(rootName);
        SkinnedMeshRenderer[] renderers = root != null
            ? root.GetComponentsInChildren<SkinnedMeshRenderer>(false)
            : new SkinnedMeshRenderer[0];
        bool found = false;
        Bounds bounds = default;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (hat != null && renderers[i].transform.IsChildOf(hat)) continue;
            if (!found) { bounds = renderers[i].bounds; found = true; }
            else bounds.Encapsulate(renderers[i].bounds);
        }
        Assert.IsTrue(found, playerClass + " must have an active skinned body renderer.");
        return bounds;
    }

    private static Bounds VisibleBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(false);
        Assert.Greater(renderers.Length, 0);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static float MaxDimension(Vector3 size) => Mathf.Max(size.x, Mathf.Max(size.y, size.z));

    private static IEnumerator CaptureEvidence(string fileName)
    {
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ValidationReports", "ClassFix30", "screenshots"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, fileName);
        ScreenCapture.CaptureScreenshot(path);
        yield return new WaitForEndOfFrame();
        float deadline = Time.realtimeSinceStartup + 5f;
        while (!File.Exists(path) && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.IsTrue(File.Exists(path), "Screenshot was not written: " + path);
    }

    private static Camera CreateEvidenceCamera(Bounds bounds)
    {
        GameObject cameraObject = new GameObject("ClassFix30EvidenceCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        if (Camera.main != null) camera.CopyFrom(Camera.main);
        camera.depth = 100f;
        camera.nearClipPlane = 0.03f;
        float radius = Mathf.Max(0.5f, bounds.extents.magnitude);
        float distance = radius / Mathf.Tan(Mathf.Deg2Rad * Mathf.Max(20f, camera.fieldOfView) * 0.5f) * 1.35f;
        Vector3 direction = new Vector3(0.8f, 0.32f, -1f).normalized;
        camera.transform.position = bounds.center - direction * distance;
        camera.transform.LookAt(bounds.center);
        return camera;
    }

    private static Camera CreateIsolatedSpriteCamera()
    {
        GameObject cameraObject = new GameObject("ClassFix30AmmoEvidenceCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.depth = 101f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.035f, 0.055f, 0.09f, 1f);
        camera.cullingMask = 1 << 30;
        camera.orthographic = true;
        camera.orthographicSize = 1.35f;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        return camera;
    }

}
