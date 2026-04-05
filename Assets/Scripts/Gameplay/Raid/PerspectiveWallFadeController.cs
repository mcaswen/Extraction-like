using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2.5D 透视墙体淡出控制器。
/// 摄像机到玩家之间的墙体会自动半透明。
/// </summary>
public class PerspectiveWallFadeController : MonoBehaviour
{
    public Transform PlayerTransform;
    public Camera TargetCamera;
    public LayerMask OccluderMask = ~0;

    private readonly HashSet<PerspectiveFadeWall> _activeOccluders = new HashSet<PerspectiveFadeWall>();

    private void LateUpdate()
    {
        if (PlayerTransform == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                PlayerTransform = playerObject.transform;
            }
        }

        if (TargetCamera == null)
        {
            TargetCamera = Camera.main;
        }

        if (PlayerTransform == null || TargetCamera == null)
        {
            return;
        }

        UpdateOccluders();
    }

    private void UpdateOccluders()
    {
        Vector3 origin = TargetCamera.transform.position;
        Vector3 target = PlayerTransform.position + Vector3.up * 1f;
        Vector3 direction = target - origin;
        float distance = direction.magnitude;
        if (distance <= 0.01f)
        {
            return;
        }

        HashSet<PerspectiveFadeWall> nextOccluders = new HashSet<PerspectiveFadeWall>();
        RaycastHit[] hits = Physics.RaycastAll(origin, direction.normalized, distance, OccluderMask, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            PerspectiveFadeWall fadeWall = hit.collider.GetComponentInParent<PerspectiveFadeWall>();
            if (fadeWall != null)
            {
                nextOccluders.Add(fadeWall);
            }
        }

        foreach (PerspectiveFadeWall occluder in _activeOccluders)
        {
            if (!nextOccluders.Contains(occluder))
            {
                occluder.SetOccluded(false);
            }
        }

        foreach (PerspectiveFadeWall occluder in nextOccluders)
        {
            occluder.SetOccluded(true);
        }

        _activeOccluders.Clear();
        foreach (PerspectiveFadeWall occluder in nextOccluders)
        {
            _activeOccluders.Add(occluder);
        }
    }
}
