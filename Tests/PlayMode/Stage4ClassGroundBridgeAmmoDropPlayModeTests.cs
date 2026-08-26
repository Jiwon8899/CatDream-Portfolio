using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class Stage4ClassGroundBridgeAmmoDropPlayModeTests
{
    private const float GroundTolerance = 0.08f;
    private string evidenceDirectory;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        evidenceDirectory = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "ValidationReports", "ClassFix32", "Stage4GroundBridgeAmmo");
        Directory.CreateDirectory(evidenceDirectory);
        SaveSystem.IntroWatched = true;
        SaveSystem.UnlockedStage = Mathf.Max(4, SaveSystem.UnlockedStage);
        yield return null;
    }

    [UnityTest]
    public IEnumerator GunDrop_PublicLeftClick_ShowsDestroyedObjectAsFloating3DWithName()
    {
        yield return LoadAndStartStage4(PlayerClass.Gun);
        GameManager manager = GameManager.Instance;
        yield return SkipOpening(manager);
        CatController cat = FindCat();
        Assert.NotNull(cat);
        GunCatAmmoRuntime ammo = cat.GetComponent<GunCatAmmoRuntime>();
        Assert.NotNull(ammo);
        Assert.IsTrue(ammo.IsActive);

        Camera camera = Camera.main;
        Assert.NotNull(camera);
        ThirdPersonCamera cameraController = camera.GetComponent<ThirdPersonCamera>();
        if (cameraController != null) cameraController.enabled = false;
        // Keep the proof target outside the production pickup radius while its 3D presentation
        // is inspected.  The previous 3.2m placement sat inside the 3.25m radius, so the real
        // service correctly absorbed it during screenshot capture before the walking assertion.
        Vector3 aimPoint = cat.transform.position + cat.transform.forward * 5.2f + Vector3.up * 0.55f;
        camera.transform.position = cat.transform.position - cat.transform.forward * 4.5f + Vector3.up * 2.1f;
        camera.transform.LookAt(aimPoint);

        BreakableObject template = null;
        Mesh sourceMesh = null;
        Material[] sourceMaterials = null;
        Vector3 authoredScale = Vector3.one;
        Quaternion authoredRotation = Quaternion.identity;
        float bestVisualScore = float.MaxValue;
        foreach (BreakableObject candidate in UnityEngine.Object.FindObjectsByType<BreakableObject>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (candidate == null || IsCheonggyeBridge(candidate)
                || !GunCatAmmoDropService.TryResolveDestroyedObjectVisual(candidate, out Mesh candidateMesh,
                    out Material[] candidateMaterials, out Vector3 candidateScale,
                    out Quaternion candidateRotation, out Bounds candidateBounds)) continue;
            float maximum = Mathf.Max(candidateBounds.size.x,
                Mathf.Max(candidateBounds.size.y, candidateBounds.size.z));
            float score = Mathf.Abs(maximum - 1.8f)
                + (string.Equals(candidateMesh.name, "Cube", StringComparison.OrdinalIgnoreCase) ? 100f : 0f);
            if (score >= bestVisualScore) continue;
            bestVisualScore = score;
            template = candidate;
            sourceMesh = candidateMesh;
            sourceMaterials = candidateMaterials;
            authoredScale = candidateScale;
            authoredRotation = candidateRotation;
        }
        Assert.NotNull(template, "Stage4 needs an authored destroyed-object mesh for the pickup proof.");
        Material sourceMaterial = sourceMaterials[0];
        GameObject source = new GameObject("ClassFix32DestroyedAuthoredObject", typeof(MeshFilter),
            typeof(MeshRenderer), typeof(BoxCollider));
        source.GetComponent<MeshFilter>().sharedMesh = sourceMesh;
        source.GetComponent<MeshRenderer>().sharedMaterials = sourceMaterials;
        Vector3 positiveAuthoredScale = new Vector3(Mathf.Abs(authoredScale.x),
            Mathf.Abs(authoredScale.y), Mathf.Abs(authoredScale.z));
        Vector3 authoredSize = Vector3.Scale(sourceMesh.bounds.size, positiveAuthoredScale);
        float templateMaximum = Mathf.Max(0.001f, Mathf.Max(authoredSize.x,
            Mathf.Max(authoredSize.y, authoredSize.z)));
        source.transform.localScale = positiveAuthoredScale * (1.55f / templateMaximum);
        source.transform.rotation = authoredRotation;
        source.GetComponent<BoxCollider>().center = sourceMesh.bounds.center;
        Vector3 targetScale = source.transform.lossyScale;
        source.GetComponent<BoxCollider>().size = new Vector3(
            1.8f / Mathf.Max(0.001f, Mathf.Abs(targetScale.x)),
            1.8f / Mathf.Max(0.001f, Mathf.Abs(targetScale.y)),
            1.8f / Mathf.Max(0.001f, Mathf.Abs(targetScale.z)));
        source.transform.position = aimPoint - source.transform.TransformVector(sourceMesh.bounds.center);
        BreakableObject breakable = source.AddComponent<BreakableObject>();
        breakable.objectType = string.IsNullOrEmpty(template.objectType) ? "class_fix32_destroyed_object" : template.objectType;
        breakable.displayName = string.IsNullOrEmpty(template.displayName) ? "부순 사물" : template.displayName;
        string expectedDisplayName = breakable.displayName;
        breakable.maxHealth = breakable.health = breakable.currentHealth = 1f;
        breakable.damageOnlyFromCat = true;
        Physics.SyncTransforms();
        Ray centerRay = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        bool sourceUnderCrosshair = Physics.RaycastAll(centerRay, 60f, ~0, QueryTriggerInteraction.Ignore)
            .Any(hit => hit.collider != null
                && (hit.collider.transform == source.transform || hit.collider.transform.IsChildOf(source.transform)));
        Assert.IsTrue(sourceUnderCrosshair, "The authored-object proof target must be under the real gun crosshair.");

        GunCatAmmoDropService service = GunCatAmmoDropService.Ensure();
        service.ValidationForceDrop = true;
        int dropsBefore = service.ActiveDropCount;
        Mouse mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
        float attackDeadline = Time.realtimeSinceStartup + 3f;
        while (service.ActiveDropCount <= dropsBefore && Time.realtimeSinceStartup < attackDeadline)
        {
            yield return TapLeft(mouse);
            yield return new WaitForSecondsRealtime(0.08f);
        }

        Assert.Greater(service.ActiveDropCount, dropsBefore,
            "The production left-click gun route must destroy the object and create an ammo drop.");
        GunCatAmmoDropView view = UnityEngine.Object.FindObjectsByType<GunCatAmmoDropView>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .FirstOrDefault(value => value != null && value.gameObject.activeInHierarchy);
        Assert.NotNull(view);
        yield return null;
        Assert.IsTrue(view.HasThreeDimensionalVisual, "Ammo drop must use a live 3D MeshRenderer, not a flat sprite.");
        MeshFilter visualFilter = view.GetComponentInChildren<MeshFilter>(true);
        MeshRenderer visualRenderer = view.GetComponentInChildren<MeshRenderer>(true);
        Assert.NotNull(visualFilter);
        Assert.NotNull(visualRenderer);
        Assert.AreSame(sourceMesh, visualFilter.sharedMesh, "The floating pickup must keep the destroyed object's mesh.");
        Assert.Contains(sourceMaterial, visualRenderer.sharedMaterials,
            "The floating pickup must keep the destroyed object's material.");
        StringAssert.Contains(expectedDisplayName, view.LabelTextForValidation);
        StringAssert.Contains("탄약", view.LabelTextForValidation);
        Assert.That(view.HoverHeightForValidation, Is.InRange(0.65f, 0.99f));
        Assert.Greater(visualRenderer.bounds.size.magnitude, 1f, "The pickup must be clearly visible in the field.");

        camera.transform.position = view.transform.position + new Vector3(3.1f, 1.85f, -3.7f);
        camera.transform.LookAt(view.transform.position + Vector3.up * 0.55f);
        yield return Capture("gun_ammo_destroyed_object_3d.png");

        int queuedBefore = ammo.Queue.Count;
        int activeBeforePickup = service.ActiveDropCount;
        Vector3 pickupPosition = cat.transform.position;
        pickupPosition.x = view.transform.position.x;
        pickupPosition.z = view.transform.position.z;
        cat.transform.position = pickupPosition;
        Physics.SyncTransforms();
        float pickupDeadline = Time.realtimeSinceStartup + 2f;
        while (service.ActiveDropCount >= activeBeforePickup && Time.realtimeSinceStartup < pickupDeadline)
            yield return null;
        Assert.Less(service.ActiveDropCount, activeBeforePickup,
            "Walking the gun cat over a visible ammo object must absorb and remove it.");
        Assert.Greater(ammo.Queue.Count, queuedBefore,
            "Absorbed destroyed-object ammunition must enter the gun magazine queue.");
    }

    [UnityTest]
    public IEnumerator Stage4Intro_AllClasses_BodyAndVisibleFeetStartOnGround()
    {
        List<string> rows = new List<string> { "class,body_error,visual_error,x,y,z,status" };
        foreach (PlayerClass playerClass in new[] { PlayerClass.Basic, PlayerClass.Melee, PlayerClass.Gun })
        {
            yield return LoadAndStartStage4(playerClass);
            Assert.IsNull(GameObject.Find("SecondDevelopmentRuntime_Stage4"),
                "Authored Stage 4 must never receive the legacy generated runtime city root.");
            GameManager manager = GameManager.Instance;
            CatController cat = FindCat();
            Assert.NotNull(cat);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.IsTrue(manager.IsStageOpeningActive, playerClass + " opening must be active for the measurement.");
            Assert.IsTrue(TryMeasureGround(cat, playerClass, out float bodyError, out float visualError, out float groundY));
            string status = bodyError <= GroundTolerance && visualError <= GroundTolerance ? "Passed" : "Failed";
            rows.Add(playerClass + "," + bodyError.ToString("F3") + "," + visualError.ToString("F3")
                + "," + cat.transform.position.x.ToString("F3") + "," + cat.transform.position.y.ToString("F3")
                + "," + cat.transform.position.z.ToString("F3") + "," + status);
            File.WriteAllLines(Path.Combine(evidenceDirectory, "stage4_intro_ground.csv"), rows);
            Debug.Log("[ClassFix32Ground] class=" + playerClass + " position=" + cat.transform.position
                + " groundY=" + groundY.ToString("F3") + " bodyError=" + bodyError.ToString("F3")
                + " visualError=" + visualError.ToString("F3"));
            yield return Capture("intro_ground_" + playerClass.ToString().ToLowerInvariant() + ".png");
            Assert.LessOrEqual(bodyError, GroundTolerance,
                playerClass + " body collider must begin on Stage4 ground, groundY=" + groundY.ToString("F3"));
            Assert.LessOrEqual(visualError, GroundTolerance,
                playerClass + " visible feet must begin on Stage4 ground, groundY=" + groundY.ToString("F3"));
            yield return SkipOpening(manager);
            Assert.IsTrue(TryMeasureGround(cat, playerClass, out bodyError, out visualError, out groundY));
            Assert.LessOrEqual(bodyError, GroundTolerance);
            Assert.LessOrEqual(visualError, GroundTolerance);
            Camera camera = Camera.main;
            Assert.NotNull(camera);
            ThirdPersonCamera cameraController = camera.GetComponent<ThirdPersonCamera>();
            if (cameraController != null) cameraController.enabled = false;
            PositionGroundProofCamera(camera, cat);
            yield return Capture("intro_ground_" + playerClass.ToString().ToLowerInvariant() + "_closeup.png");
        }
        File.WriteAllLines(Path.Combine(evidenceDirectory, "stage4_intro_ground.csv"), rows);
    }

    [UnityTest]
    public IEnumerator Stage4Bridge_AllClasses_CrossRightToLeftWithoutJump()
    {
        List<string> rows = new List<string> { "class,bridge,travel,max_ground_error,min_body_y,elapsed,status" };
        foreach (PlayerClass playerClass in new[] { PlayerClass.Basic, PlayerClass.Melee, PlayerClass.Gun })
        {
            yield return LoadAndStartStage4(playerClass);
            Assert.IsNull(GameObject.Find("SecondDevelopmentRuntime_Stage4"),
                "The bridge path must remain free from legacy generated Stage 4 geometry.");
            GameManager manager = GameManager.Instance;
            yield return SkipOpening(manager);
            CatController cat = FindCat();
            Assert.NotNull(cat);
            BreakableObject bridge = UnityEngine.Object.FindObjectsByType<BreakableObject>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(IsCheonggyeBridge)
                .OrderBy(value => Mathf.Abs(value.transform.position.z + 47.5f))
                .FirstOrDefault();
            Assert.NotNull(bridge, "Stage4 needs an authored Cheonggyecheon bridge.");
            BoxCollider bridgeCollider = bridge.GetComponent<BoxCollider>();
            Assert.NotNull(bridgeCollider);
            Collider leftRoad = GameObject.Find("LeftUpperRoad").GetComponent<Collider>();
            Collider rightRoad = GameObject.Find("RightUpperRoad").GetComponent<Collider>();
            Assert.GreaterOrEqual(leftRoad.bounds.max.x - bridgeCollider.bounds.min.x, 0.45f,
                "Bridge collider must overlap the left upper road.");
            Assert.GreaterOrEqual(bridgeCollider.bounds.max.x - rightRoad.bounds.min.x, 0.45f,
                "Bridge collider must overlap the right upper road.");

            Vector3 start = new Vector3(20f, 8f, bridgeCollider.bounds.center.z);
            cat.transform.position = start;
            Rigidbody body = cat.GetComponent<Rigidbody>();
            if (body != null) body.position = start;
            Physics.SyncTransforms();
            Assert.IsTrue(cat.AlignToGround(20f, 40f));
            if (body != null) body.position = cat.transform.position;
            yield return new WaitForFixedUpdate();
            Vector3 settledStart = cat.transform.position;
            Vector3 target = new Vector3(-20f, settledStart.y, bridgeCollider.bounds.center.z);
            cat.SetManualInputEnabled(true);
            cat.SetAutoControl(true);
            yield return Capture("bridge_" + playerClass.ToString().ToLowerInvariant() + "_start.png");

            float startedAt = Time.realtimeSinceStartup;
            float deadline = startedAt + 12f;
            float maxGroundError = 0f;
            float minBodyY = float.MaxValue;
            bool midpointCaptured = false;
            while (Time.realtimeSinceStartup < deadline && cat.transform.position.x > -18f)
            {
                Vector3 direction = target - cat.transform.position;
                direction.y = 0f;
                cat.SetAutoMoveWorldDirection(direction.normalized);
                if (TryMeasureGround(cat, playerClass, out float bodyError, out _, out _))
                    maxGroundError = Mathf.Max(maxGroundError, bodyError);
                Collider catCollider = cat.GetComponent<Collider>();
                if (catCollider != null) minBodyY = Mathf.Min(minBodyY, catCollider.bounds.min.y);
                if (!midpointCaptured && cat.transform.position.x <= 0.5f)
                {
                    midpointCaptured = true;
                    yield return Capture("bridge_" + playerClass.ToString().ToLowerInvariant() + "_mid.png");
                }
                yield return null;
            }
            cat.SetAutoControl(false);
            float elapsed = Time.realtimeSinceStartup - startedAt;
            float travel = settledStart.x - cat.transform.position.x;
            bool passed = cat.transform.position.x <= -18f && midpointCaptured
                && maxGroundError <= 0.13f && minBodyY >= 1.95f;
            rows.Add(playerClass + "," + bridge.objectId + "," + travel.ToString("F2") + ","
                + maxGroundError.ToString("F3") + "," + minBodyY.ToString("F3") + ","
                + elapsed.ToString("F2") + "," + (passed ? "Passed" : "Failed"));
            File.WriteAllLines(Path.Combine(evidenceDirectory, "stage4_bridge_traversal.csv"), rows);
            File.WriteAllLines(Path.Combine(evidenceDirectory,
                    "bridge_" + playerClass.ToString().ToLowerInvariant() + "_contacts.txt"),
                DescribeBodyContacts(cat));
            Assert.IsTrue(passed, playerClass + " failed no-jump bridge traversal: x="
                + cat.transform.position.x.ToString("F2") + " groundError=" + maxGroundError.ToString("F3")
                + " minBodyY=" + minBodyY.ToString("F3"));
            yield return Capture("bridge_" + playerClass.ToString().ToLowerInvariant() + "_end.png");
        }
        File.WriteAllLines(Path.Combine(evidenceDirectory, "stage4_bridge_traversal.csv"), rows);
    }

    private static IEnumerable<string> DescribeBodyContacts(CatController cat)
    {
        Collider body = cat != null ? cat.GetComponent<Collider>() : null;
        if (body == null) return new[] { "missing body" };
        List<string> lines = new List<string> { "cat=" + cat.transform.position.ToString("F3") };
        Collider[] contacts = Physics.OverlapBox(body.bounds.center, body.bounds.extents * 1.03f,
            Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider candidate in contacts.OrderBy(value => value != null ? value.name : string.Empty))
        {
            if (candidate == null || candidate.transform.IsChildOf(cat.transform)) continue;
            lines.Add(candidate.name + " | root=" + candidate.transform.root.name
                + " | center=" + candidate.bounds.center.ToString("F3")
                + " | size=" + candidate.bounds.size.ToString("F3")
                + " | component=" + candidate.GetType().Name);
        }
        return lines;
    }

    private static void PositionGroundProofCamera(Camera camera, CatController cat)
    {
        Vector3 target = cat.transform.position + Vector3.up * 0.62f;
        Vector3[] directions =
        {
            Vector3.left, Vector3.back, Vector3.forward, Vector3.right,
            new Vector3(-1f, 0f, -1f).normalized, new Vector3(-1f, 0f, 1f).normalized,
            new Vector3(1f, 0f, -1f).normalized, new Vector3(1f, 0f, 1f).normalized
        };
        Vector3 chosen = target + Vector3.left * 3.4f + Vector3.up * 1.25f;
        for (int i = 0; i < directions.Length; i++)
        {
            Vector3 candidate = target + directions[i] * 3.4f + Vector3.up * 1.25f;
            Vector3 ray = candidate - target;
            RaycastHit[] hits = Physics.SphereCastAll(target, 0.18f, ray.normalized, ray.magnitude,
                ~0, QueryTriggerInteraction.Ignore);
            bool blocked = hits.Any(hit => hit.collider != null
                && !hit.collider.transform.IsChildOf(cat.transform));
            if (blocked) continue;
            chosen = candidate;
            break;
        }
        camera.transform.position = chosen;
        camera.transform.LookAt(target);
    }

    private static IEnumerator LoadAndStartStage4(PlayerClass playerClass)
    {
        PlayerClassSelection.Current = playerClass;
        AsyncOperation load = SceneManager.LoadSceneAsync(SceneLoader.Stage4SceneName, LoadSceneMode.Single);
        Assert.NotNull(load);
        while (!load.isDone) yield return null;
        float deadline = Time.realtimeSinceStartup + 20f;
        while ((GameManager.Instance == null || GameObject.Find("CatPlayer") == null)
               && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.NotNull(GameManager.Instance);
        CatController cat = FindCat();
        Assert.NotNull(cat);
        PlayerClassRuntime.Ensure(cat.gameObject, playerClass);
        GameManager.Instance.StartStage(4);
        deadline = Time.realtimeSinceStartup + 8f;
        while (!GameManager.Instance.IsStageOpeningActive && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.IsTrue(GameManager.Instance.IsStageOpeningActive);
    }

    private static IEnumerator SkipOpening(GameManager manager)
    {
        yield return new WaitForSecondsRealtime(0.35f);
        manager.RequestSkipStageOpening();
        float deadline = Time.realtimeSinceStartup + 8f;
        while (manager.IsStageOpeningActive && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.IsFalse(manager.IsStageOpeningActive, "Stage opening did not finish after the public skip request.");
        yield return new WaitForFixedUpdate();
        yield return null;
    }

    private static CatController FindCat()
    {
        GameObject cat = GameObject.Find("CatPlayer");
        return cat != null ? cat.GetComponent<CatController>() : null;
    }

    private static bool TryMeasureGround(CatController cat, PlayerClass playerClass,
        out float bodyError, out float visualError, out float groundY)
    {
        bodyError = visualError = float.MaxValue;
        groundY = 0f;
        if (cat == null) return false;
        Collider body = cat.GetComponent<Collider>();
        if (body == null) return false;
        Vector3 origin = new Vector3(body.bounds.center.x, body.bounds.max.y + 4f, body.bounds.center.z);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 50f, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        RaycastHit ground = default;
        bool found = false;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider candidate = hits[i].collider;
            BreakableObject candidateBreakable = candidate != null
                ? candidate.GetComponentInParent<BreakableObject>() : null;
            if (candidate == null || candidate.transform == cat.transform || candidate.transform.IsChildOf(cat.transform)
                || candidate.isTrigger || hits[i].normal.y < 0.55f
                || (candidateBreakable != null && !IsCheonggyeBridge(candidateBreakable))) continue;
            ground = hits[i];
            found = true;
            break;
        }
        if (!found)
        {
            // During bridge traversal, the authored bridge itself is the stable floor.
            for (int i = 0; i < hits.Length; i++)
            {
                Collider candidate = hits[i].collider;
                if (candidate == null || candidate.transform.IsChildOf(cat.transform)
                    || candidate.isTrigger || hits[i].normal.y < 0.55f) continue;
                ground = hits[i];
                found = true;
                break;
            }
        }
        if (!found) return false;
        groundY = ground.point.y;
        bodyError = Mathf.Abs(body.bounds.min.y - groundY);

        Transform visualRoot = playerClass == PlayerClass.Melee
            ? cat.transform.Find("MeleeClassVisual")
            : playerClass == PlayerClass.Gun ? cat.transform.Find("GunClassVisual") : cat.transform.Find("CatVisualRoot");
        if (visualRoot == null) return false;
        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        float bottom = float.MaxValue;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null && renderers[i].enabled && renderers[i].gameObject.activeInHierarchy)
                bottom = Mathf.Min(bottom, renderers[i].bounds.min.y);
        if (bottom == float.MaxValue) return false;
        visualError = Mathf.Abs(bottom - groundY);
        return true;
    }

    private static bool IsCheonggyeBridge(BreakableObject value)
    {
        return value != null && !string.IsNullOrEmpty(value.objectId)
            && value.objectId.IndexOf("CheonggyeBridge", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private IEnumerator Capture(string fileName)
    {
        string path = Path.Combine(evidenceDirectory, fileName);
        ScreenCapture.CaptureScreenshot(path);
        yield return new WaitForEndOfFrame();
        float deadline = Time.realtimeSinceStartup + 3f;
        while (!File.Exists(path) && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.IsTrue(File.Exists(path), "Screenshot was not written: " + path);
    }

    private static IEnumerator TapLeft(Mouse mouse)
    {
        InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
        yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState());
        yield return null;
    }
}
