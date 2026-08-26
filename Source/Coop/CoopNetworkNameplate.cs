using Photon.Pun;
using TMPro;
using UnityEngine;

public sealed class CoopNetworkNameplate : MonoBehaviourPun
{
    private const float MaxDistance = 40f;
    private TextMeshPro label;
    private Renderer labelRenderer;
    private Camera activeCamera;
    private string appliedNickname = string.Empty;
    private float nextLayoutAt;

    public bool LabelVisible => label != null && label.gameObject.activeInHierarchy;
    public float CameraDistance { get; private set; }
    public float DistanceScaleMultiplier { get; private set; }
    public bool IsOccluded { get; private set; }

    private void Start()
    {
        GameObject root = new GameObject("CoopNicknameLabel", typeof(TextMeshPro));
        root.transform.SetParent(transform, false);
        label = root.GetComponent<TextMeshPro>();
        RefreshNickname();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 3.2f;
        label.color = Color.white;
        label.raycastTarget = false;
        UIRebuildPrefabCatalog catalog = UIRebuildPrefabCatalog.Load();
        if (catalog != null && catalog.worldTextNormalMaterial != null) label.fontSharedMaterial = catalog.worldTextNormalMaterial;
        labelRenderer = root.GetComponent<Renderer>();
        PositionAboveModel();
    }

    private void LateUpdate()
    {
        if (label == null) return;
        RefreshNickname();
        if (Time.unscaledTime >= nextLayoutAt)
        {
            nextLayoutAt = Time.unscaledTime + 0.5f;
            PositionAboveModel();
        }
        bool remoteStoryPlayer = photonView != null && !photonView.IsMine && CoopNetService.Instance.IsInRoom;
        activeCamera = activeCamera != null ? activeCamera : Camera.main;
        if (!remoteStoryPlayer || activeCamera == null)
        {
            CameraDistance = 0f;
            DistanceScaleMultiplier = 0f;
            IsOccluded = false;
            label.gameObject.SetActive(false);
            return;
        }
        float distance = Vector3.Distance(activeCamera.transform.position, label.transform.position);
        CameraDistance = distance;
        if (distance > MaxDistance)
        {
            DistanceScaleMultiplier = 0f;
            IsOccluded = false;
            label.gameObject.SetActive(false);
            return;
        }
        Vector3 direction = label.transform.position - activeCamera.transform.position;
        bool occluded = Physics.Raycast(activeCamera.transform.position, direction.normalized, out RaycastHit hit,
            direction.magnitude, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(transform);
        IsOccluded = occluded;
        label.gameObject.SetActive(!occluded);
        if (occluded) return;
        label.transform.rotation = Quaternion.LookRotation(label.transform.position - activeCamera.transform.position);
        float scale = Mathf.Clamp(distance / 12f, 0.7f, 1.5f) * 0.1f;
        DistanceScaleMultiplier = scale / 0.1f;
        label.transform.localScale = Vector3.one * scale;
    }

    private void PositionAboveModel()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        float top = 1.8f;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || renderers[i] == labelRenderer || !renderers[i].enabled) continue;
            if (renderers[i].GetComponentInParent<CoopBreakableState>() != null) continue;
            top = Mathf.Max(top, transform.InverseTransformPoint(renderers[i].bounds.max).y);
        }
        label.transform.localPosition = new Vector3(0f, top + 0.4f, 0f);
    }

    private void RefreshNickname()
    {
        if (label == null || photonView == null || photonView.Owner == null) return;
        string nickname = photonView.Owner.NickName;
        if (string.IsNullOrWhiteSpace(nickname)) nickname = "플레이어 " + photonView.Owner.ActorNumber;
        if (nickname == appliedNickname) return;
        appliedNickname = nickname;
        label.text = nickname;
    }
}
