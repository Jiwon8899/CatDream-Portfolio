using System.Collections.Generic;
using UnityEngine;

public class StageManager : MonoBehaviour
{
    public bool useAuthoredStage1Template;
    public bool useInstalledVarcoBreakables = true;
    public bool autoSpawnFurnitureEnabled = false;

    public int CurrentStage { get; private set; } = 1;
    public string CurrentStageName => GetStageName(CurrentStage);
    public int CurrentTargetScore => GetTargetScore(CurrentStage);
    public Vector3 PlayerSpawnPoint => playerSpawnPoint;

    private readonly List<Vector3> usedPositions = new List<Vector3>();
    private Vector3 playerSpawnPoint = new Vector3(0f, 1.2f, 0f);

    public static string GetStageName(int stage)
    {
        switch (stage)
        {
            case 6: return LocalizationManager.Text("6스테이지: DDP 러브버그 최종 결전", "Stage 6: DDP Lovebug Final Battle");
            case 5: return LocalizationManager.Text("5\uC2A4\uD14C\uC774\uC9C0: \uC11C\uC6B8\uB85C 7017", "Stage 5: Seoullo 7017");
            case 2: return LocalizationManager.Text("2\uC2A4\uD14C\uC774\uC9C0: \uC11C\uC6B8 \uD55C\uAC15", "Stage 2: Seoul Hangang");
            case 3: return LocalizationManager.Text("3\uC2A4\uD14C\uC774\uC9C0: \uC11C\uC6B8\uAD11\uC7A5", "Stage 3: Seoul Plaza");
            case 4: return LocalizationManager.Text("4\uC2A4\uD14C\uC774\uC9C0: \uC11C\uC6B8 \uCCAD\uACC4\uCC9C", "Stage 4: Seoul Cheonggyecheon");
            default: return LocalizationManager.Text("1\uC2A4\uD14C\uC774\uC9C0: \uC6B0\uB9AC\uC9D1", "Stage 1: Home");
        }
    }

    public static int GetTargetScore(int stage)
    {
        switch (stage)
        {
            case 6: return 4200;
            case 2: return 900;
            case 3: return 1400;
            case 4: return 1900;
            case 5: return 2400;
            default: return 500;
        }
    }

    public void GenerateStage(int stage)
    {
        CurrentStage = Mathf.Clamp(stage, 1, SceneLoader.MaxStage);
        ClearGenerated();
        usedPositions.Clear();
        playerSpawnPoint = ResolvePlayerSpawnPoint(CurrentStage);
        if (!autoSpawnFurnitureEnabled)
        {
            RegisterCodexObjects();
            return;
        }

        if (CurrentStage == 1 && useAuthoredStage1Template && TryGenerateAuthoredStage1())
        {
            RegisterCodexObjects();
            return;
        }

        GameObject root = new GameObject("GeneratedStage");
        GameObject breakableRoot = new GameObject("GeneratedBreakables");
        breakableRoot.transform.SetParent(root.transform);

        GameObject[] installedBreakables = useInstalledVarcoBreakables ? Resources.LoadAll<GameObject>("InstalledBreakables") : null;
        if (installedBreakables != null && installedBreakables.Length > 0)
        {
            GenerateInstalledVarcoStage(root.transform, breakableRoot.transform, installedBreakables, CurrentStage);
            RegisterCodexObjects();
            return;
        }

        switch (CurrentStage)
        {
            case 5:
                GenerateSeoullo(root.transform, breakableRoot.transform);
                break;
            case 4:
                GenerateCheonggyecheon(root.transform, breakableRoot.transform);
                break;
            case 2:
                GenerateHangang(root.transform, breakableRoot.transform);
                break;
            case 3:
                GeneratePlaza(root.transform, breakableRoot.transform);
                break;
            default:
                GenerateHome(root.transform, breakableRoot.transform);
                break;
        }

        RegisterCodexObjects();
    }

    private void GenerateInstalledVarcoStage(Transform root, Transform breakableRoot, GameObject[] prefabs, int stage)
    {
        float halfX = stage == 5 ? 34f : stage == 4 ? 28f : stage == 3 ? 10f : stage == 2 ? 3f : 9.5f;
        float halfZ = stage == 5 ? 54f : stage == 4 ? 50f : stage == 2 ? 10.5f : stage == 3 ? 10f : 5.1f;
        if (stage == 5)
        {
            GenerateSeoulloShell(root);
        }
        else if (stage == 4)
        {
            GenerateCheonggyecheonShell(root);
        }
        else if (stage == 2)
        {
            GenerateHangangShell(root);
        }
        else if (stage == 3)
        {
            GeneratePlazaShell(root);
        }
        else
        {
            GenerateHomeShell(root);
        }

        int count = stage == 5 ? 64 : stage == 4 ? 52 : stage == 3 ? 32 : stage == 2 ? 28 : 42;
        for (int i = 0; i < count; i++)
        {
            GameObject prefab = prefabs[i % prefabs.Length];
            Vector3 position = FindSafePosition(halfX, halfZ);
            GameObject instance = Instantiate(prefab, breakableRoot);
            instance.name = prefab.name + "_" + (i + 1).ToString("00");
            instance.transform.position = new Vector3(position.x, 0.02f, position.z);
            instance.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            ResetInstalledBreakable(instance, i, stage);
        }
    }

    private void ResetInstalledBreakable(GameObject instance, int index, int stage)
    {
        SetHierarchyActive(instance.transform, true);
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = true;
            }
        }

        BreakableObject breakable = instance.GetComponent<BreakableObject>();
        if (breakable == null)
        {
            breakable = instance.AddComponent<BreakableObject>();
        }

        if (string.IsNullOrEmpty(breakable.objectRole))
        {
            breakable.objectRole = instance.name;
        }
        breakable.objectType = string.IsNullOrEmpty(breakable.objectType) ? breakable.objectRole : breakable.objectType;
        breakable.objectId = breakable.objectRole + "_" + index.ToString("00");
        breakable.health = breakable.maxHealth;
        breakable.currentHealth = breakable.maxHealth;
        breakable.EnsureCodexData();
        ApplyStageDifficulty(breakable, stage);

        Rigidbody rb = instance.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = instance.AddComponent<Rigidbody>();
        }
        ClearRigidbodyMotion(rb);
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        if (instance.GetComponent<Collider>() == null)
        {
            BoxCollider collider = instance.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.6f, 0f);
            collider.size = new Vector3(1.15f, 1.2f, 1.15f);
        }

        Physics.SyncTransforms();
        Bounds bounds = CalculateVisibleBounds(instance);
        float largestAxis = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (largestAxis > 0.0001f && largestAxis < 0.45f)
        {
            float scaleMultiplier = Mathf.Clamp(0.85f / largestAxis, 1f, 80f);
            instance.transform.localScale *= scaleMultiplier;
            Physics.SyncTransforms();
            bounds = CalculateVisibleBounds(instance);
        }

        if (bounds.size.sqrMagnitude > 0.0001f)
        {
            float lift = 0.04f - bounds.min.y;
            instance.transform.position += Vector3.up * lift;
        }

    }

    private static void SetHierarchyActive(Transform root, bool active)
    {
        if (root == null)
        {
            return;
        }

        root.gameObject.SetActive(active);
        for (int i = 0; i < root.childCount; i++)
        {
            SetHierarchyActive(root.GetChild(i), active);
        }
    }

    private static Bounds CalculateVisibleBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds(root.transform.position, Vector3.zero);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || !renderers[i].enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        if (!hasBounds)
        {
            Collider collider = root.GetComponentInChildren<Collider>(true);
            if (collider != null)
            {
                bounds = collider.bounds;
            }
        }

        return bounds;
    }

    private void GenerateHomeShell(Transform root)
    {
        CreateExpandedHomeShell(root);
    }

    private void GenerateCorridorShell(Transform root)
    {
        CreateStaticCube("CorridorFloorA", root, new Vector3(0f, -0.05f, 5f), new Vector3(6f, 0.1f, 24f), new Color(0.34f, 0.34f, 0.38f));
        CreateStaticCube("CorridorFloorB", root, new Vector3(6f, -0.05f, 15f), new Vector3(12f, 0.1f, 6f), new Color(0.34f, 0.34f, 0.38f));
        CreateStaticCube("LeftLongWall", root, new Vector3(-3f, 1.5f, 5f), new Vector3(0.35f, 3f, 24f), new Color(0.62f, 0.62f, 0.66f));
        CreateStaticCube("RightLongWall", root, new Vector3(3f, 1.5f, 4f), new Vector3(0.35f, 3f, 20f), new Color(0.62f, 0.62f, 0.66f));
        CreateStaticCube("CornerWallOuter", root, new Vector3(6f, 1.5f, 18f), new Vector3(12f, 3f, 0.35f), new Color(0.62f, 0.62f, 0.66f));
        CreateStaticCube("CornerWallInner", root, new Vector3(3f, 1.5f, 12f), new Vector3(0.35f, 3f, 6f), new Color(0.62f, 0.62f, 0.66f));
        CreateStaticCube("CorridorSouthEndWall", root, new Vector3(0f, 1.5f, -7f), new Vector3(6f, 3f, 0.35f), new Color(0.62f, 0.62f, 0.66f));
        CreateStaticCube("CorridorNorthEndWall", root, new Vector3(0f, 1.5f, 17f), new Vector3(6f, 3f, 0.35f), new Color(0.62f, 0.62f, 0.66f));
    }

    private void GenerateHangangShell(Transform root)
    {
        CreateStaticCube("HangangRiversideWalk", root, new Vector3(0f, -0.05f, 0f), new Vector3(22f, 0.1f, 8f), new Color(0.42f, 0.43f, 0.42f));
        CreateStaticCube("HangangBikeRoad", root, new Vector3(0f, -0.03f, -3.2f), new Vector3(22f, 0.08f, 1.6f), new Color(0.38f, 0.12f, 0.12f));
        CreateStaticCube("HangangRiver", root, new Vector3(0f, -0.08f, 6.5f), new Vector3(24f, 0.08f, 6f), new Color(0.12f, 0.42f, 0.72f));
        CreateStaticCube("HangangRailing", root, new Vector3(0f, 0.65f, 3.35f), new Vector3(22f, 1.3f, 0.18f), new Color(0.82f, 0.84f, 0.82f));
        CreateStaticCube("ApartmentComplexRoute", root, new Vector3(0f, -0.025f, -7.8f), new Vector3(9f, 0.08f, 8f), new Color(0.36f, 0.38f, 0.4f));
        CreateStaticCube("ApartmentBlockA", root, new Vector3(-4.2f, 2.4f, -10.8f), new Vector3(2.4f, 4.8f, 1.8f), new Color(0.52f, 0.58f, 0.6f));
        CreateStaticCube("ApartmentBlockB", root, new Vector3(3.8f, 2.9f, -10.6f), new Vector3(2.8f, 5.8f, 1.8f), new Color(0.46f, 0.53f, 0.58f));
        CreateStaticCube("HangangEntranceGate", root, new Vector3(0f, 1.1f, -3.9f), new Vector3(5.2f, 2.2f, 0.32f), new Color(0.2f, 0.64f, 0.38f));
        CreateStaticCube("SeoulSkylineA", root, new Vector3(-7f, 1.1f, 9.7f), new Vector3(2.1f, 2.2f, 0.45f), new Color(0.25f, 0.29f, 0.36f));
        CreateStaticCube("SeoulSkylineB", root, new Vector3(-3.8f, 1.6f, 9.7f), new Vector3(1.8f, 3.2f, 0.45f), new Color(0.22f, 0.25f, 0.32f));
        CreateStaticCube("SeoulNamsanTowerHint", root, new Vector3(2.8f, 2.1f, 9.7f), new Vector3(0.35f, 4.2f, 0.35f), new Color(0.86f, 0.74f, 0.48f));
        CreateStaticCube("HangangConvenienceStand", root, new Vector3(7.5f, 0.9f, -1.2f), new Vector3(2.4f, 1.8f, 1.6f), new Color(0.95f, 0.55f, 0.28f));
        CreateStaticCube("HangangPicnicMat", root, new Vector3(-6.5f, 0.02f, -1.2f), new Vector3(3.2f, 0.05f, 2.1f), new Color(0.95f, 0.84f, 0.35f));
    }

    private void GeneratePlazaShell(Transform root)
    {
        CreateStaticCube("PlazaFloor", root, new Vector3(0f, -0.05f, 0f), new Vector3(24f, 0.1f, 24f), new Color(0.28f, 0.48f, 0.34f));
        CreateStaticCube("NorthBoundary", root, new Vector3(0f, 1.2f, 12f), new Vector3(24f, 2.4f, 0.4f), new Color(0.38f, 0.45f, 0.38f));
        CreateStaticCube("SouthBoundary", root, new Vector3(0f, 1.2f, -12f), new Vector3(24f, 2.4f, 0.4f), new Color(0.38f, 0.45f, 0.38f));
        CreateStaticCube("EastBoundary", root, new Vector3(12f, 1.2f, 0f), new Vector3(0.4f, 2.4f, 24f), new Color(0.38f, 0.45f, 0.38f));
        CreateStaticCube("WestBoundary", root, new Vector3(-12f, 1.2f, 0f), new Vector3(0.4f, 2.4f, 24f), new Color(0.38f, 0.45f, 0.38f));
        CreateStaticCube("PlaygroundFrame", root, new Vector3(5f, 0.8f, 5f), new Vector3(3.5f, 1.5f, 0.25f), new Color(0.92f, 0.58f, 0.22f));
        CreateStaticCube("FlowerbedWall", root, new Vector3(-5f, 0.25f, 5f), new Vector3(4f, 0.5f, 1f), new Color(0.52f, 0.28f, 0.18f));
    }

    private void GenerateCheonggyecheonShell(Transform root)
    {
        CreateStaticCube("CheonggyecheonStream", root, new Vector3(0f, -0.12f, 0f), new Vector3(13f, 0.12f, 110f), new Color(0.10f, 0.42f, 0.60f));
        CreateStaticCube("CheonggyecheonLowerWalkLeft", root, new Vector3(-9f, 0f, 0f), new Vector3(6f, 0.12f, 112f), new Color(0.62f, 0.60f, 0.55f));
        CreateStaticCube("CheonggyecheonLowerWalkRight", root, new Vector3(9f, 0f, 0f), new Vector3(6f, 0.12f, 112f), new Color(0.62f, 0.60f, 0.55f));
        CreateStaticCube("CheonggyecheonUpperRoadLeft", root, new Vector3(-25f, 2.05f, 0f), new Vector3(24f, 0.18f, 116f), new Color(0.22f, 0.24f, 0.25f));
        CreateStaticCube("CheonggyecheonUpperRoadRight", root, new Vector3(25f, 2.05f, 0f), new Vector3(24f, 0.18f, 116f), new Color(0.22f, 0.24f, 0.25f));
        CreateStaticCube("CheonggyecheonWallLeft", root, new Vector3(-14f, 1.1f, 0f), new Vector3(2f, 2.2f, 112f), new Color(0.54f, 0.52f, 0.48f));
        CreateStaticCube("CheonggyecheonWallRight", root, new Vector3(14f, 1.1f, 0f), new Vector3(2f, 2.2f, 112f), new Color(0.54f, 0.52f, 0.48f));
        CreateStaticCube("CheonggyecheonSpringSculpture", root, new Vector3(0f, 4.4f, -58f), new Vector3(2f, 8.8f, 2f), new Color(0.82f, 0.25f, 0.72f));
        for (int i = 0; i < 4; i++)
        {
            float z = -38f + i * 25f;
            CreateStaticCube("CheonggyeBridge_" + i, root, new Vector3(0f, 2.35f, z), new Vector3(34f, 0.7f, 4.2f), new Color(0.58f, 0.55f, 0.50f));
        }
        for (int i = 0; i < 16; i++)
        {
            float z = -54f + i * 7.2f;
            CreateStaticCube("CheonggyeTreeLeft_" + i, root, new Vector3(-20f, 4.1f, z), new Vector3(1.2f, 4.2f, 1.2f), new Color(0.20f, 0.55f, 0.18f));
            CreateStaticCube("CheonggyeTreeRight_" + i, root, new Vector3(20f, 4.1f, z + 2f), new Vector3(1.2f, 4.2f, 1.2f), new Color(0.20f, 0.55f, 0.18f));
        }
        for (int i = 0; i < 14; i++)
        {
            float z = -54f + i * 8.4f;
            float height = 8f + (i % 5) * 2.2f;
            CreateStaticCube("CheonggyeOfficeTowerLeft_" + i, root, new Vector3(-36f, 2f + height * 0.5f, z), new Vector3(5f, height, 4f), new Color(0.34f, 0.42f, 0.48f));
            CreateStaticCube("CheonggyeOfficeTowerRight_" + i, root, new Vector3(36f, 2f + height * 0.5f, z + 2f), new Vector3(5f, height + 2f, 4f), new Color(0.40f, 0.48f, 0.54f));
        }
    }

    private void GenerateSeoulloShell(Transform root)
    {
        CreateStaticCube("SeoulloElevatedDeck", root, new Vector3(0f, 4.0f, 0f), new Vector3(12f, 0.7f, 118f), new Color(0.72f, 0.74f, 0.70f));
        CreateStaticCube("SeoulloCurvedDeckWest", root, new Vector3(-10f, 4.0f, -34f), new Vector3(18f, 0.7f, 16f), new Color(0.72f, 0.74f, 0.70f));
        CreateStaticCube("SeoulloCurvedDeckEast", root, new Vector3(10f, 4.0f, 34f), new Vector3(18f, 0.7f, 16f), new Color(0.72f, 0.74f, 0.70f));
        CreateStaticCube("SeoulloGlassRailLeft", root, new Vector3(-6.4f, 5.05f, 0f), new Vector3(0.35f, 2.1f, 118f), new Color(0.58f, 0.78f, 0.86f));
        CreateStaticCube("SeoulloGlassRailRight", root, new Vector3(6.4f, 5.05f, 0f), new Vector3(0.35f, 2.1f, 118f), new Color(0.58f, 0.78f, 0.86f));
        CreateStaticCube("SeoulloRoadBelow", root, new Vector3(0f, 0f, 0f), new Vector3(54f, 0.12f, 126f), new Color(0.18f, 0.19f, 0.20f));
        CreateStaticCube("SeoulloRailYardBelow", root, new Vector3(-36f, -0.02f, 4f), new Vector3(24f, 0.08f, 112f), new Color(0.42f, 0.36f, 0.27f));

        for (int i = 0; i < 20; i++)
        {
            float z = -55f + i * 5.8f;
            CreateStaticCube("SeoulloPlanter_" + i, root, new Vector3(i % 2 == 0 ? -3.2f : 3.2f, 5.0f, z), new Vector3(2.8f, 1.2f, 2.8f), new Color(0.58f, 0.56f, 0.50f));
            CreateStaticCube("SeoulloTree_" + i, root, new Vector3(i % 2 == 0 ? -3.2f : 3.2f, 7.0f, z), new Vector3(1.4f, 4.0f, 1.4f), new Color(0.20f, 0.55f, 0.18f));
            CreateStaticCube("SeoulloLamp_" + i, root, new Vector3(i % 2 == 0 ? 5.2f : -5.2f, 6.5f, z + 1.6f), new Vector3(0.35f, 5.0f, 0.35f), new Color(0.86f, 0.86f, 0.82f));
        }

        for (int i = 0; i < 12; i++)
        {
            float z = -52f + i * 9.5f;
            float height = 9f + (i % 5) * 2.4f;
            CreateStaticCube("SeoulloOfficeTowerLeft_" + i, root, new Vector3(-52f, height * 0.5f, z), new Vector3(8f, height, 6f), new Color(0.35f, 0.43f, 0.50f));
            CreateStaticCube("SeoulloOfficeTowerRight_" + i, root, new Vector3(52f, height * 0.5f, z + 3f), new Vector3(8f, height + 3f, 6f), new Color(0.40f, 0.48f, 0.54f));
        }
    }

    private void ClearGenerated()
    {
        DestroyByName("GeneratedStage");
        DestroyByName("GeneratedRoom");
        DestroyByName("GeneratedBreakables");
    }

    private void DestroyByName(string objectName)
    {
        GameObject existing = GameObject.Find(objectName);
        if (existing == null)
        {
            return;
        }

        DestroyImmediate(existing);
    }

    private bool TryGenerateAuthoredStage1()
    {
        GameObject template = FindInactiveObject("AuthoredStage1House");
        if (template == null)
        {
            return false;
        }

        GameObject instance = Instantiate(template);
        instance.name = "GeneratedStage";
        instance.SetActive(true);
        Transform spawn = instance.transform.Find("PlayerSpawnPoint");
        if (spawn != null)
        {
            playerSpawnPoint = spawn.position;
        }

        BreakableObject[] breakables = instance.GetComponentsInChildren<BreakableObject>(true);
        for (int i = 0; i < breakables.Length; i++)
        {
            if (breakables[i] == null)
            {
                continue;
            }

            breakables[i].health = breakables[i].maxHealth;
            breakables[i].currentHealth = breakables[i].health;
            breakables[i].EnsureCodexData();
        }

        return true;
    }

    private GameObject FindInactiveObject(string objectName)
    {
        GameObject active = GameObject.Find(objectName);
        if (active != null)
        {
            return active;
        }

        UnityEngine.SceneManagement.Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded)
        {
            return null;
        }

        GameObject[] roots = activeScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject found = FindInHierarchy(roots[i] != null ? roots[i].transform : null, objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private GameObject FindInHierarchy(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == objectName)
        {
            return root.gameObject;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            GameObject found = FindInHierarchy(root.GetChild(i), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private void GenerateHome(Transform root, Transform breakableRoot)
    {
        CreateExpandedHomeShell(root);
        SpawnRoles(breakableRoot, new[] { "Cup", "Book", "FlowerPot", "Chair", "TV", "Table" }, 42, 9.5f, 5.1f);
    }

    private void CreateExpandedHomeShell(Transform root)
    {
        CreateStaticCube("Floor", root, new Vector3(0f, -0.05f, 0f), new Vector3(22f, 0.1f, 12f), new Color(0.78f, 0.66f, 0.48f));
        CreateStaticCube("BackWall", root, new Vector3(0f, 1.5f, 6f), new Vector3(22.4f, 3f, 0.35f), new Color(0.96f, 0.86f, 0.68f));
        CreateStaticCube("LeftWall", root, new Vector3(-11f, 1.5f, 0f), new Vector3(0.35f, 3f, 12f), new Color(0.96f, 0.86f, 0.68f));
        CreateStaticCube("RightWall", root, new Vector3(11f, 1.5f, 0f), new Vector3(0.35f, 3f, 12f), new Color(0.96f, 0.86f, 0.68f));
        CreateStaticCube("FrontWallLeft", root, new Vector3(-6.4f, 1.5f, -6f), new Vector3(9.2f, 3f, 0.35f), new Color(0.96f, 0.86f, 0.68f));
        CreateStaticCube("FrontWallRight", root, new Vector3(6.4f, 1.5f, -6f), new Vector3(9.2f, 3f, 0.35f), new Color(0.96f, 0.86f, 0.68f));
        CreateStaticCube("KitchenArea", root, new Vector3(-8.2f, 0.02f, 2.9f), new Vector3(4.5f, 0.06f, 4.8f), new Color(0.92f, 0.86f, 0.72f));
        CreateStaticCube("BedroomArea", root, new Vector3(8.2f, 0.02f, 2.9f), new Vector3(4.5f, 0.06f, 4.8f), new Color(0.74f, 0.82f, 1f));
        CreateStaticCube("LivingRoomArea", root, new Vector3(0f, 0.025f, 2.2f), new Vector3(6.6f, 0.06f, 5.2f), new Color(0.88f, 0.72f, 0.58f));
        CreateStaticCube("BathroomArea", root, new Vector3(-8.2f, 0.025f, -3.2f), new Vector3(4.5f, 0.06f, 4.6f), new Color(0.66f, 0.9f, 0.96f));
        CreateStaticCube("MasterBedroomArea", root, new Vector3(8.2f, 0.025f, -3.2f), new Vector3(4.5f, 0.06f, 4.6f), new Color(0.92f, 0.72f, 0.88f));
        CreateStaticCube("LowGapBarrier", root, new Vector3(0f, 0.9f, 4f), new Vector3(4f, 0.28f, 0.35f), new Color(0.64f, 0.48f, 0.34f));
        GameObject houseExit = CreateStaticCube("CatHouseReturnZone", root, new Vector3(0f, 0.8f, -5.65f), new Vector3(2.2f, 1.6f, 0.28f), new Color(0.85f, 0.48f, 0.22f));
        HouseReturnZone zone = houseExit.AddComponent<HouseReturnZone>();
        zone.requirePlayerTag = false;
    }

    private void GenerateCorridor(Transform root, Transform breakableRoot)
    {
        CreateStaticCube("CorridorFloorA", root, new Vector3(0f, -0.05f, 5f), new Vector3(6f, 0.1f, 24f), new Color(0.34f, 0.34f, 0.38f));
        CreateStaticCube("CorridorFloorB", root, new Vector3(6f, -0.05f, 15f), new Vector3(12f, 0.1f, 6f), new Color(0.34f, 0.34f, 0.38f));
        CreateStaticCube("LeftLongWall", root, new Vector3(-3f, 1.5f, 5f), new Vector3(0.35f, 3f, 24f), new Color(0.62f, 0.62f, 0.66f));
        CreateStaticCube("RightLongWall", root, new Vector3(3f, 1.5f, 4f), new Vector3(0.35f, 3f, 20f), new Color(0.62f, 0.62f, 0.66f));
        CreateStaticCube("CornerWallOuter", root, new Vector3(6f, 1.5f, 18f), new Vector3(12f, 3f, 0.35f), new Color(0.62f, 0.62f, 0.66f));
        CreateStaticCube("CornerWallInner", root, new Vector3(3f, 1.5f, 12f), new Vector3(0.35f, 3f, 6f), new Color(0.62f, 0.62f, 0.66f));
        CreateStaticCube("CorridorSouthEndWall", root, new Vector3(0f, 1.5f, -7f), new Vector3(6f, 3f, 0.35f), new Color(0.62f, 0.62f, 0.66f));
        CreateStaticCube("CorridorNorthEndWall", root, new Vector3(0f, 1.5f, 17f), new Vector3(6f, 3f, 0.35f), new Color(0.62f, 0.62f, 0.66f));
        CreateStaticCube("BranchEastWall", root, new Vector3(12f, 1.5f, 15f), new Vector3(0.35f, 3f, 6f), new Color(0.62f, 0.62f, 0.66f));
        CreateStaticCube("BranchSouthWall", root, new Vector3(6f, 1.5f, 12f), new Vector3(6f, 3f, 0.35f), new Color(0.62f, 0.62f, 0.66f));
        for (int i = 0; i < 5; i++)
        {
            CreateStaticCube("Door_" + i, root, new Vector3(-2.8f, 1f, -2f + i * 4f), new Vector3(0.12f, 2f, 1.4f), new Color(0.45f, 0.32f, 0.22f));
        }
        SpawnRoles(breakableRoot, new[] { "Mailbox", "Bike", "FireExtinguisher", "PackageBox", "Cup", "Book" }, 28, 2.2f, 10.5f);
    }

    private void GenerateHangang(Transform root, Transform breakableRoot)
    {
        GenerateHangangShell(root);
        SpawnRoles(breakableRoot, new[] { "Bench", "Bike", "Cup", "Book", "PackageBox", "LampPost", "VendingMachine", "Flowerbed" }, 30, 9.8f, 3.2f);
    }

    private void GeneratePlaza(Transform root, Transform breakableRoot)
    {
        CreateStaticCube("PlazaFloor", root, new Vector3(0f, -0.05f, 0f), new Vector3(24f, 0.1f, 24f), new Color(0.28f, 0.48f, 0.34f));
        CreateStaticCube("NorthBoundary", root, new Vector3(0f, 1.2f, 12f), new Vector3(24f, 2.4f, 0.4f), new Color(0.38f, 0.45f, 0.38f));
        CreateStaticCube("SouthBoundary", root, new Vector3(0f, 1.2f, -12f), new Vector3(24f, 2.4f, 0.4f), new Color(0.38f, 0.45f, 0.38f));
        CreateStaticCube("EastBoundary", root, new Vector3(12f, 1.2f, 0f), new Vector3(0.4f, 2.4f, 24f), new Color(0.38f, 0.45f, 0.38f));
        CreateStaticCube("WestBoundary", root, new Vector3(-12f, 1.2f, 0f), new Vector3(0.4f, 2.4f, 24f), new Color(0.38f, 0.45f, 0.38f));
        CreateStaticCube("PlaygroundFrame", root, new Vector3(5f, 0.8f, 5f), new Vector3(3.5f, 1.5f, 0.25f), new Color(0.92f, 0.58f, 0.22f));
        CreateStaticCube("FlowerbedWall", root, new Vector3(-5f, 0.25f, 5f), new Vector3(4f, 0.5f, 1f), new Color(0.52f, 0.28f, 0.18f));
        SpawnRoles(breakableRoot, new[] { "Bench", "LampPost", "VendingMachine", "Flowerbed", "Playground", "PackageBox" }, 32, 10f, 10f);
    }

    private void GenerateCheonggyecheon(Transform root, Transform breakableRoot)
    {
        GenerateCheonggyecheonShell(root);
        SpawnRoles(breakableRoot, new[] { "Lantern", "Bench", "LampPost", "Bike", "VendingMachine", "Flowerbed", "PackageBox" }, 56, 26f, 50f);
    }

    private void GenerateSeoullo(Transform root, Transform breakableRoot)
    {
        GenerateSeoulloShell(root);
        SpawnRoles(breakableRoot, new[] { "Flowerbed", "Bench", "LampPost", "Bike", "VendingMachine", "PackageBox", "Lantern" }, 64, 5f, 52f);
    }

    private void SpawnRoles(Transform parent, string[] roles, int count, float halfX, float halfZ)
    {
        for (int i = 0; i < count; i++)
        {
            string role = roles[i % roles.Length];
            Vector3 position = FindSafePosition(halfX, halfZ);
            GameObject obj = CreateBreakable(role, i + 1, position);
            obj.transform.SetParent(parent);
        }
    }

    private Vector3 FindSafePosition(float halfX, float halfZ)
    {
        for (int attempt = 0; attempt < 120; attempt++)
        {
            Vector3 candidate = new Vector3(Random.Range(-halfX, halfX), 0.5f, Random.Range(-halfZ, halfZ));
            if (Mathf.Abs(candidate.x) < 1.5f && Mathf.Abs(candidate.z) < 1.5f)
            {
                continue;
            }

            bool clear = true;
            for (int i = 0; i < usedPositions.Count; i++)
            {
                if (Vector3.Distance(candidate, usedPositions[i]) < 1.35f)
                {
                    clear = false;
                    break;
                }
            }

            if (clear)
            {
                usedPositions.Add(candidate);
                return candidate;
            }
        }

        Vector3 fallback = new Vector3(Random.Range(-halfX, halfX), 0.5f, Random.Range(-halfZ, halfZ));
        usedPositions.Add(fallback);
        return fallback;
    }

    private static Vector3 ResolvePlayerSpawnPoint(int stage)
    {
        Transform sceneSpawn = FindScenePlayerSpawnPoint();
        if (sceneSpawn != null)
        {
            return sceneSpawn.position;
        }

        switch (stage)
        {
            case 5:
                return new Vector3(0f, 5.2f, -58f);
            case 4:
                return new Vector3(20f, 3.15f, -47.5f);
            default:
                return new Vector3(0f, 1.2f, 0f);
        }
    }

    private static Transform FindScenePlayerSpawnPoint()
    {
        UnityEngine.SceneManagement.Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || candidate.name != "PlayerSpawnPoint")
            {
                continue;
            }
            if (!candidate.gameObject.scene.IsValid() || candidate.gameObject.scene != activeScene)
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private GameObject CreateBreakable(string role, int index, Vector3 position)
    {
        BreakableObject.ObjectSize size = GetRoleSize(role);
        GameObject obj = GameObject.CreatePrimitive(GetRolePrimitive(role));
        obj.name = role + "_" + index.ToString("00");
        obj.transform.position = new Vector3(position.x, GetSpawnY(size), position.z);
        obj.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        obj.transform.localScale = GetRoleScale(role);

        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = obj.AddComponent<Rigidbody>();
        }
        rb.mass = size == BreakableObject.ObjectSize.Large ? 8f : size == BreakableObject.ObjectSize.Medium ? 4f : 1.5f;
        rb.linearDamping = 0.25f;
        rb.angularDamping = 0.7f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        BreakableObject breakable = obj.GetComponent<BreakableObject>();
        if (breakable == null)
        {
            breakable = obj.AddComponent<BreakableObject>();
        }
        breakable.objectRole = role;
        breakable.objectType = role;
        breakable.objectId = role;
        breakable.size = size;
        breakable.maxHealth = size == BreakableObject.ObjectSize.Large ? 35f : size == BreakableObject.ObjectSize.Medium ? 18f : 8f;
        breakable.health = breakable.maxHealth;
        breakable.currentHealth = breakable.maxHealth;
        breakable.requiredPower = size == BreakableObject.ObjectSize.Large ? 5f : size == BreakableObject.ObjectSize.Medium ? 2f : 1f;
        breakable.ConfigureForSize();
        ApplyStageDifficulty(breakable, CurrentStage);

        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = GetRoleColor(size);
        }

        return obj;
    }

    private static void ClearRigidbodyMotion(Rigidbody rb)
    {
        if (rb == null)
        {
            return;
        }

        if (rb.isKinematic)
        {
            rb.Sleep();
            return;
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    private void ApplyStageDifficulty(BreakableObject breakable, int stage)
    {
        if (breakable == null || stage <= 1)
        {
            return;
        }

        breakable.ApplyStageDifficultyScaling(stage);
    }

    private float GetSpawnY(BreakableObject.ObjectSize size)
    {
        return size == BreakableObject.ObjectSize.Large ? 0.75f : size == BreakableObject.ObjectSize.Medium ? 0.55f : 0.35f;
    }

    private PrimitiveType GetRolePrimitive(string role)
    {
        return role == "Cup" || role == "FlowerPot" || role == "FireExtinguisher" || role == "LampPost" || role == "Lantern" ? PrimitiveType.Cylinder : PrimitiveType.Cube;
    }

    private Vector3 GetRoleScale(string role)
    {
        switch (role)
        {
            case "Cup": return new Vector3(0.35f, 0.55f, 0.35f);
            case "Book": return new Vector3(0.8f, 0.18f, 0.55f);
            case "FlowerPot": return new Vector3(0.55f, 0.65f, 0.55f);
            case "Chair": return new Vector3(0.8f, 0.85f, 0.8f);
            case "TV": return new Vector3(1.3f, 0.85f, 0.18f);
            case "Table": return new Vector3(1.4f, 0.45f, 0.9f);
            case "Bike": return new Vector3(1.5f, 0.9f, 0.25f);
            case "VendingMachine": return new Vector3(1.1f, 1.8f, 0.8f);
            case "LampPost": return new Vector3(0.25f, 2.1f, 0.25f);
            case "Bench": return new Vector3(1.8f, 0.5f, 0.55f);
            case "Lantern": return new Vector3(0.65f, 0.75f, 0.65f);
            default: return new Vector3(0.9f, 0.7f, 0.7f);
        }
    }

    private BreakableObject.ObjectSize GetRoleSize(string role)
    {
        switch (role)
        {
            case "TV":
            case "Table":
            case "Bike":
            case "VendingMachine":
            case "Playground":
                return BreakableObject.ObjectSize.Large;
            case "FlowerPot":
            case "Chair":
            case "Mailbox":
            case "FireExtinguisher":
            case "PackageBox":
            case "Bench":
            case "LampPost":
            case "Flowerbed":
            case "Lantern":
                return BreakableObject.ObjectSize.Medium;
            default:
                return BreakableObject.ObjectSize.Small;
        }
    }

    private Color GetRoleColor(BreakableObject.ObjectSize size)
    {
        if (size == BreakableObject.ObjectSize.Large)
        {
            return new Color(0.55f, 0.45f, 0.82f);
        }

        return size == BreakableObject.ObjectSize.Medium ? new Color(0.85f, 0.55f, 0.32f) : new Color(0.9f, 0.86f, 0.58f);
    }

    private GameObject CreateStaticCube(string objectName, Transform parent, Vector3 position, Vector3 scale, Color color)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = objectName;
        obj.transform.SetParent(parent);
        obj.transform.position = position;
        obj.transform.localScale = scale;
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
        }
        return obj;
    }

    private void RegisterCodexObjects()
    {
        ObjectCodexManager codex = ObjectCodexManager.Instance;
        if (codex == null)
        {
            return;
        }

        BreakableObject[] breakables = FindObjectsOfType<BreakableObject>();
        for (int i = 0; i < breakables.Length; i++)
        {
            codex.RegisterObject(breakables[i]);
        }
        codex.RefreshCodexUI();
    }
}
