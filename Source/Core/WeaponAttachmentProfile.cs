using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum WeaponAttachmentObjectType
{
    TinyObject,
    SmallObject,
    ShortObject,
    LongWeapon,
    FlatObject,
    HeavyObject,
    TwoHandWeapon
}

public enum WeaponAttachmentHandType
{
    OneHand,
    TwoHand
}

public enum WeaponAttachmentHandSide
{
    Left,
    Right
}

public enum WeaponAttachmentValidationStatus
{
    NotValidated = 0,
    Untested = NotValidated,
    Generated = NotValidated,
    Passed,
    Failed,
    NeedsReview,
    NeedsManualReview = NeedsReview
}

[Serializable]
public sealed class WeaponAttachmentProfile
{
    public string geometrySignature;
    public string runtimeGeometryKey;
    public string weaponId;
    public string canonicalType;
    public string prefabName;
    public string displayName;
    public int stageIndex;
    public WeaponAttachmentObjectType weaponType;
    public WeaponAttachmentHandType handType;
    public WeaponAttachmentHandSide handSide;
    public Vector3 gripLocalPosition;
    public Vector3 gripLocalEuler;
    public Vector3 gripLocalRotation;
    public Vector3 equipLocalPosition;
    public Vector3 equipLocalEuler;
    public Vector3 equipLocalRotation;
    public Vector3 equipLocalScale = Vector3.one;
    public Vector3 catScalePositionOffset;
    public Vector3 catScaleRotationOffset;
    public float catScaleScaleMultiplier = 1f;
    public Vector3 bodyClearanceOffset;
    public bool longWeaponMode;
    public Vector3 visualLocalCenter;
    public Vector3 visualWorldSize;
    public float targetBoundsSize;
    public float damage;
    public float range;
    public float lastGripError;
    public float lastBoundsGripError;
    public int autoCorrectionAttemptCount;
    public bool hasValidatedPose;
    public WeaponAttachmentValidationStatus validationStatus = WeaponAttachmentValidationStatus.NotValidated;
    public string note;
    public List<string> lastValidationScreenshotPaths = new List<string>();
    public string lastValidationError;
    public string lastValidatedAt;

    public string LegacyKey => MakeKey(weaponId, canonicalType, displayName, stageIndex, handType, handSide);
    public string Key => string.IsNullOrEmpty(geometrySignature) ? LegacyKey : LegacyKey + "|g" + geometrySignature;

    public static string MakeKey(string weaponId, string canonicalType, string displayName, WeaponAttachmentHandType handType, WeaponAttachmentHandSide handSide)
    {
        return MakeKey(weaponId, canonicalType, displayName, 0, handType, handSide);
    }

    public static string MakeKey(string weaponId, string canonicalType, string displayName, int stageIndex, WeaponAttachmentHandType handType, WeaponAttachmentHandSide handSide)
    {
        string id = !ObjectCodexManager.IsGenericObjectName(weaponId) ? weaponId :
            !ObjectCodexManager.IsGenericObjectName(canonicalType) ? canonicalType : displayName;
        string normalized = ObjectCodexManager.NormalizeObjectName(id);
        if (string.IsNullOrEmpty(normalized))
        {
            normalized = "unknown_weapon";
        }
        return normalized.ToLowerInvariant() + "|S" + Mathf.Max(0, stageIndex) + "|" + handType + "|" + handSide;
    }
}

public static class WeaponGeometrySignature
{
    // Three decimals still merged small child-transform variations that become
    // visible offsets on scene props whose imported root scale is in the
    // hundreds or thousands.  Four decimals remains below the 400-profile cap
    // measured by the V3 audit (271 profiles for 1,309 placements).
    public const int Precision = 4;

    public static string BuildGeometrySignature(Transform root)
    {
        return Hash(BuildDescriptor(root, true));
    }

    public static string BuildRuntimeKey(Transform root)
    {
        return Hash(BuildDescriptor(root, false));
    }

    private static string BuildDescriptor(Transform root, bool useEditorGuid)
    {
        if (root == null)
        {
            return "missing-geometry";
        }

        List<string> meshes = new List<string>();
        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            MeshFilter filter = filters[i];
            if (filter == null || filter.sharedMesh == null) continue;
            meshes.Add(GetPath(filter.transform, root) + "|" + GetMeshIdentity(filter.sharedMesh, useEditorGuid));
        }
        SkinnedMeshRenderer[] skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < skinned.Length; i++)
        {
            SkinnedMeshRenderer renderer = skinned[i];
            if (renderer == null || renderer.sharedMesh == null) continue;
            meshes.Add(GetPath(renderer.transform, root) + "|" + GetMeshIdentity(renderer.sharedMesh, useEditorGuid));
        }
        meshes.Sort(StringComparer.Ordinal);

        StringBuilder descriptor = new StringBuilder();
        descriptor.Append(string.Join(";", meshes));
        descriptor.Append("|B:").Append(Vector(CalculateRootLocalBounds(root), Precision));
        descriptor.Append("|S:").Append(Vector(Abs(root.lossyScale), Precision));
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true)
            .Where(item => item != root).OrderBy(item => GetPath(item, root), StringComparer.Ordinal).ToArray();
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform item = transforms[i];
            descriptor.Append("|T:").Append(GetPath(item, root))
                .Append(':').Append(Vector(item.localPosition, Precision))
                .Append(':').Append(Vector(item.localEulerAngles, Precision))
                .Append(':').Append(Vector(item.localScale, Precision));
        }
        return descriptor.ToString();
    }

    private static string GetMeshIdentity(Mesh mesh, bool useEditorGuid)
    {
#if UNITY_EDITOR
        if (useEditorGuid)
        {
            string assetPath = AssetDatabase.GetAssetPath(mesh);
            if (!string.IsNullOrEmpty(assetPath))
            {
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (!string.IsNullOrEmpty(guid)) return guid;
            }
        }
#endif
        Bounds bounds = mesh.bounds;
        return mesh.name + "|v" + mesh.vertexCount + "|s" + mesh.subMeshCount + "|b" + Vector(bounds.size, Precision);
    }

    private static Bounds CalculateRootLocalBounds(Transform root)
    {
        Bounds result = new Bounds(Vector3.zero, Vector3.zero);
        bool found = false;
        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            MeshFilter filter = filters[i];
            if (filter == null || filter.sharedMesh == null) continue;
            Encapsulate(filter.sharedMesh.bounds, filter.transform, root, ref result, ref found);
        }
        SkinnedMeshRenderer[] skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < skinned.Length; i++)
        {
            SkinnedMeshRenderer renderer = skinned[i];
            if (renderer == null || renderer.sharedMesh == null) continue;
            Encapsulate(renderer.sharedMesh.bounds, renderer.transform, root, ref result, ref found);
        }
        return result;
    }

    private static void Encapsulate(Bounds bounds, Transform source, Transform root, ref Bounds result, ref bool found)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 point = root.InverseTransformPoint(source.TransformPoint(new Vector3(
                (corner & 1) == 0 ? min.x : max.x,
                (corner & 2) == 0 ? min.y : max.y,
                (corner & 4) == 0 ? min.z : max.z)));
            if (!found) { result = new Bounds(point, Vector3.zero); found = true; }
            else result.Encapsulate(point);
        }
    }

    private static string GetPath(Transform item, Transform root)
    {
        if (item == root) return string.Empty;
        List<string> parts = new List<string>();
        Transform current = item;
        while (current != null && current != root)
        {
            parts.Add(current.name);
            current = current.parent;
        }
        parts.Reverse();
        return string.Join("/", parts);
    }

    private static Vector3 Abs(Vector3 value) { return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z)); }
    private static string Vector(Bounds bounds, int decimals) { return Vector(bounds.size, decimals); }
    private static string Vector(Vector3 value, int decimals)
    {
        return Round(value.x, decimals) + "," + Round(value.y, decimals) + "," + Round(value.z, decimals);
    }
    private static string Round(float value, int decimals)
    {
        return Math.Round(value, decimals, MidpointRounding.AwayFromZero).ToString("F" + decimals, CultureInfo.InvariantCulture);
    }
    private static string Hash(string value)
    {
        using (SHA256 sha = SHA256.Create())
        {
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty)))
                .Replace("-", string.Empty).Substring(0, 16).ToLowerInvariant();
        }
    }
}

[Serializable]
public sealed class WeaponAttachmentProfileList
{
    public List<WeaponAttachmentProfile> profiles = new List<WeaponAttachmentProfile>();
}

public sealed class WeaponGripPointMarker : MonoBehaviour
{
    public string profileKey;
    public WeaponAttachmentHandType handType;
    public WeaponAttachmentHandSide handSide;
}

public static class WeaponAttachmentProfileDatabase
{
    private const string ResourcePath = "WeaponAttachmentProfiles/generated_v3";
    private const string LegacyResourcePath = "WeaponAttachmentProfiles/generated";
    private static readonly Dictionary<string, WeaponAttachmentProfile> ProfilesByKey = new Dictionary<string, WeaponAttachmentProfile>();
    private static readonly Dictionary<string, WeaponAttachmentProfile> ProfilesByRuntimeGeometry = new Dictionary<string, WeaponAttachmentProfile>();
    private static readonly Dictionary<string, WeaponAttachmentProfile> LegacyAliases = new Dictionary<string, WeaponAttachmentProfile>();
    private static bool loaded;

    public static IReadOnlyCollection<WeaponAttachmentProfile> Profiles
    {
        get
        {
            EnsureLoaded();
            return ProfilesByKey.Values;
        }
    }

    public static void ClearForTests()
    {
        ProfilesByKey.Clear();
        ProfilesByRuntimeGeometry.Clear();
        LegacyAliases.Clear();
        loaded = true;
    }

    public static WeaponAttachmentProfile GetOrCreate(CodexWeaponDefinition definition, BreakableObject weapon, bool twoHanded, bool leftHand, float targetBoundsSize)
    {
        EnsureLoaded();
        WeaponAttachmentHandType handType = twoHanded ? WeaponAttachmentHandType.TwoHand : WeaponAttachmentHandType.OneHand;
        WeaponAttachmentHandSide handSide = leftHand ? WeaponAttachmentHandSide.Left : WeaponAttachmentHandSide.Right;
        string key = WeaponAttachmentProfile.MakeKey(definition != null ? definition.objectId : weapon != null ? weapon.objectId : string.Empty,
            definition != null ? definition.canonicalType : weapon != null ? weapon.objectType : string.Empty,
            definition != null ? definition.displayName : weapon != null ? weapon.displayName : string.Empty,
            definition != null ? definition.stageIndex : 0,
            handType,
            handSide);

        if (string.IsNullOrEmpty(key))
        {
            key = "unknown_weapon|S" + Mathf.Max(0, definition != null ? definition.stageIndex : 0) + "|" + handType + "|" + handSide;
        }

        if (weapon != null)
        {
            string runtimeGeometryKey = WeaponGeometrySignature.BuildRuntimeKey(weapon.transform);
            if (ProfilesByRuntimeGeometry.TryGetValue(key + "|r" + runtimeGeometryKey, out WeaponAttachmentProfile geometryProfile))
            {
                return geometryProfile;
            }
        }

        if (ProfilesByKey.TryGetValue(key, out WeaponAttachmentProfile existing))
        {
            return existing;
        }

        if (LegacyAliases.TryGetValue(key, out existing))
        {
            return existing;
        }

        Bounds bounds = CalculateVisibleBounds(weapon);
        WeaponAttachmentProfile profile = new WeaponAttachmentProfile
        {
            weaponId = definition != null ? definition.objectId : weapon != null ? weapon.objectId : key,
            canonicalType = definition != null ? definition.canonicalType : weapon != null ? weapon.objectType : key,
            prefabName = weapon != null ? weapon.gameObject.name : key,
            displayName = definition != null ? definition.displayName : weapon != null ? weapon.displayName : key,
            stageIndex = definition != null ? definition.stageIndex : 0,
            handType = handType,
            handSide = handSide,
            weaponType = Classify(bounds, definition, twoHanded),
            targetBoundsSize = targetBoundsSize,
            damage = definition != null ? definition.damage : 0f,
            range = definition != null ? definition.range : 0f,
            validationStatus = WeaponAttachmentValidationStatus.NotValidated,
            note = "Auto generated from mesh bounds. Awaiting exhaustive attachment validation."
        };
        if (weapon != null)
        {
            profile.geometrySignature = WeaponGeometrySignature.BuildGeometrySignature(weapon.transform);
            profile.runtimeGeometryKey = WeaponGeometrySignature.BuildRuntimeKey(weapon.transform);
        }
        IndexProfile(profile);
        return profile;
    }

    public static void CaptureValidatedPose(WeaponAttachmentProfile profile, BreakableObject weapon, Transform socket, float boundsGripError)
    {
        if (profile == null || weapon == null || socket == null)
        {
            return;
        }

        Bounds bounds = CalculateVisibleBounds(weapon);
        profile.gripLocalPosition = weapon.transform.InverseTransformPoint(socket.position);
        profile.gripLocalEuler = (Quaternion.Inverse(weapon.transform.rotation) * socket.rotation).eulerAngles;
        profile.gripLocalRotation = profile.gripLocalEuler;
        profile.equipLocalPosition = weapon.transform.localPosition;
        profile.equipLocalEuler = weapon.transform.localEulerAngles;
        profile.equipLocalRotation = profile.equipLocalEuler;
        profile.equipLocalScale = weapon.transform.localScale;
        profile.visualLocalCenter = weapon.transform.InverseTransformPoint(bounds.center);
        profile.visualWorldSize = bounds.size;
        profile.lastGripError = Vector3.Distance(weapon.transform.TransformPoint(profile.gripLocalPosition), socket.position);
        profile.lastBoundsGripError = boundsGripError;
        profile.longWeaponMode = profile.weaponType == WeaponAttachmentObjectType.LongWeapon || profile.weaponType == WeaponAttachmentObjectType.TwoHandWeapon;
        profile.catScaleScaleMultiplier = Mathf.Max(0.001f, weapon.transform.lossyScale.magnitude);
        profile.bodyClearanceOffset = bounds.center - socket.position;
        profile.hasValidatedPose = true;
        profile.validationStatus = WeaponAttachmentValidationStatus.NotValidated;
        profile.note = "GripPoint pose captured. Numeric and screenshot validation still required.";
        profile.lastValidationError = string.Empty;
        profile.lastValidatedAt = DateTime.UtcNow.ToString("o");
        string profileKey = profile.Key;
        if (!string.IsNullOrEmpty(profileKey))
        {
            ProfilesByKey[profileKey] = profile;
        }
    }

    public static void UpdateValidationResult(WeaponAttachmentProfile profile, WeaponAttachmentValidationStatus status, string note, string error, int autoCorrectionAttempts, IEnumerable<string> screenshotPaths)
    {
        if (profile == null)
        {
            return;
        }

        profile.validationStatus = status;
        profile.note = note ?? string.Empty;
        profile.lastValidationError = error ?? string.Empty;
        profile.autoCorrectionAttemptCount = Mathf.Max(0, autoCorrectionAttempts);
        profile.lastValidatedAt = DateTime.UtcNow.ToString("o");
        if (profile.lastValidationScreenshotPaths == null)
        {
            profile.lastValidationScreenshotPaths = new List<string>();
        }
        profile.lastValidationScreenshotPaths.Clear();
        if (screenshotPaths != null)
        {
            profile.lastValidationScreenshotPaths.AddRange(screenshotPaths);
        }

        string profileKey = profile.Key;
        if (!string.IsNullOrEmpty(profileKey))
        {
            ProfilesByKey[profileKey] = profile;
        }
    }

    public static bool TryGet(string key, out WeaponAttachmentProfile profile)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(key))
        {
            profile = null;
            return false;
        }

        return ProfilesByKey.TryGetValue(key, out profile) || LegacyAliases.TryGetValue(key, out profile);
    }

    public static void SaveAllToJson(string absolutePath)
    {
        EnsureLoaded();
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
        WeaponAttachmentProfileList list = new WeaponAttachmentProfileList();
        list.profiles.AddRange(ProfilesByKey.Values);
        list.profiles.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
        File.WriteAllText(absolutePath, JsonUtility.ToJson(list, true), System.Text.Encoding.UTF8);
    }

    public static void SaveAllToCsv(string absolutePath)
    {
        EnsureLoaded();
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
        using (StreamWriter writer = new StreamWriter(absolutePath, false, System.Text.Encoding.UTF8))
        {
            writer.WriteLine("weaponId,prefabName,displayName,stage,weaponType,handType,handSide,gripLocalPosition,gripLocalRotation,equipLocalPosition,equipLocalRotation,equipLocalScale,catScalePositionOffset,catScaleRotationOffset,catScaleScaleMultiplier,bodyClearanceOffset,longWeaponMode,targetBoundsSize,lastGripError,lastBoundsGripError,autoCorrectionAttemptCount,status,lastValidatedAt,lastValidationError,lastValidationScreenshotPaths,note");
            List<WeaponAttachmentProfile> sorted = new List<WeaponAttachmentProfile>(ProfilesByKey.Values);
            sorted.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            for (int i = 0; i < sorted.Count; i++)
            {
                WeaponAttachmentProfile p = sorted[i];
                writer.WriteLine(string.Join(",",
                    Escape(p.weaponId),
                    Escape(p.prefabName),
                    Escape(p.displayName),
                    p.stageIndex.ToString(),
                    p.weaponType.ToString(),
                    p.handType.ToString(),
                    p.handSide.ToString(),
                    Escape(FormatVector(p.gripLocalPosition)),
                    Escape(FormatVector(p.gripLocalRotation)),
                    Escape(FormatVector(p.equipLocalPosition)),
                    Escape(FormatVector(p.equipLocalRotation)),
                    Escape(FormatVector(p.equipLocalScale)),
                    Escape(FormatVector(p.catScalePositionOffset)),
                    Escape(FormatVector(p.catScaleRotationOffset)),
                    p.catScaleScaleMultiplier.ToString("F5"),
                    Escape(FormatVector(p.bodyClearanceOffset)),
                    p.longWeaponMode.ToString(),
                    p.targetBoundsSize.ToString("F4"),
                    p.lastGripError.ToString("F5"),
                    p.lastBoundsGripError.ToString("F5"),
                    p.autoCorrectionAttemptCount.ToString(),
                    p.validationStatus.ToString(),
                    Escape(p.lastValidatedAt),
                    Escape(p.lastValidationError),
                    Escape(p.lastValidationScreenshotPaths != null ? string.Join(";", p.lastValidationScreenshotPaths) : string.Empty),
                    Escape(p.note)));
            }
        }
    }

    private static void EnsureLoaded()
    {
        if (loaded)
        {
            return;
        }

        loaded = true;
        TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null || string.IsNullOrWhiteSpace(asset.text))
        {
            asset = Resources.Load<TextAsset>(LegacyResourcePath);
        }
        if (asset == null || string.IsNullOrWhiteSpace(asset.text))
        {
            return;
        }

        WeaponAttachmentProfileList list = JsonUtility.FromJson<WeaponAttachmentProfileList>(asset.text);
        if (list == null || list.profiles == null)
        {
            return;
        }

        for (int i = 0; i < list.profiles.Count; i++)
        {
            WeaponAttachmentProfile profile = list.profiles[i];
            if (profile != null && !string.IsNullOrEmpty(profile.Key))
            {
                IndexProfile(profile);
            }
        }
    }

    public static void RegisterOrReplace(WeaponAttachmentProfile profile)
    {
        EnsureLoaded();
        if (profile == null || string.IsNullOrEmpty(profile.Key)) return;
        IndexProfile(profile);
    }

    private static void IndexProfile(WeaponAttachmentProfile profile)
    {
        ProfilesByKey[profile.Key] = profile;
        if (!string.IsNullOrEmpty(profile.runtimeGeometryKey))
        {
            ProfilesByRuntimeGeometry[profile.LegacyKey + "|r" + profile.runtimeGeometryKey] = profile;
        }
        if (!LegacyAliases.ContainsKey(profile.LegacyKey))
        {
            LegacyAliases[profile.LegacyKey] = profile;
        }
    }

    private static WeaponAttachmentObjectType Classify(Bounds bounds, CodexWeaponDefinition definition, bool twoHanded)
    {
        if (twoHanded)
        {
            return WeaponAttachmentObjectType.TwoHandWeapon;
        }

        Vector3 size = bounds.size;
        float largest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        float smallest = Mathf.Max(0.001f, Mathf.Min(size.x, Mathf.Min(size.y, size.z)));
        float horizontal = Mathf.Max(size.x, size.z);
        float volume = Mathf.Max(0f, size.x * size.y * size.z);

        if (largest < 0.42f)
        {
            return WeaponAttachmentObjectType.TinyObject;
        }
        if (largest / smallest > 2.35f)
        {
            return WeaponAttachmentObjectType.LongWeapon;
        }
        if (horizontal > 0.001f && size.y < horizontal * 0.48f)
        {
            return WeaponAttachmentObjectType.FlatObject;
        }
        if ((definition != null && definition.size == BreakableObject.ObjectSize.Large) || volume > 1.0f)
        {
            return WeaponAttachmentObjectType.HeavyObject;
        }
        return largest < 0.86f ? WeaponAttachmentObjectType.SmallObject : WeaponAttachmentObjectType.ShortObject;
    }

    private static Bounds CalculateVisibleBounds(BreakableObject weapon)
    {
        if (weapon == null)
        {
            return new Bounds(Vector3.zero, Vector3.zero);
        }

        Renderer[] renderers = weapon.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds(weapon.transform.position, Vector3.zero);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled || renderer is ParticleSystemRenderer)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return bounds;
    }

    private static string FormatVector(Vector3 value)
    {
        return value.x.ToString("F5") + " " + value.y.ToString("F5") + " " + value.z.ToString("F5");
    }

    private static string Escape(string value)
    {
        value = value ?? string.Empty;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
