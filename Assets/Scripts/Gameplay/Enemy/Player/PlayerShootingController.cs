using UnityEngine;

/// <summary>
/// 玩家射击控制器。
/// </summary>
public class PlayerShootingController : MonoBehaviour
{
    public Transform FirePoint;

    [Header("Bullet")]
    public GameObject BulletPrefab;
    public float WeaponDamage = 25f;
    public float WeaponRange = 100f;

    [Header("Visual")]
    public LineRenderer BulletTrail;
    public float TrailDuration = 0.05f;

    [Header("Status Effect")]
    public float SilenceTintStrength = 0.55f;
    public Color SilenceTintColor = new Color(0.32f, 0.82f, 1f, 1f);

    private float _silenceDurationRemaining;
    private Renderer[] _cachedRenderers;
    private Color[] _originalColors;

    private void Start()
    {
        CacheRendererColors();
    }

    private void Update()
    {
        if (RaidFlowController.Instance != null && RaidFlowController.Instance.IsInputLocked)
        {
            return;
        }

        TickSilence();

        if (InventoryScreenController.Instance != null && InventoryScreenController.Instance.IsInventoryOpen)
        {
            return;
        }

        if (_silenceDurationRemaining > 0f)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            Shoot();
        }
    }

    private void Shoot()
    {
        if (FirePoint == null || BulletPrefab == null)
        {
            Debug.LogWarning("请在 Inspector 中指定 FirePoint 和 BulletPrefab。");
            return;
        }

        GameObject bulletObject = Instantiate(BulletPrefab, FirePoint.position, FirePoint.rotation);
        BulletController bullet = bulletObject.GetComponent<BulletController>();
        if (bullet != null)
        {
            bullet.Damage = WeaponDamage;
        }
    }

    /// <summary>
    /// 施加禁魔/沉默效果，期间无法射击。
    /// </summary>
    public void ApplySilence(float duration)
    {
        if (duration <= 0f)
        {
            return;
        }

        _silenceDurationRemaining = Mathf.Max(_silenceDurationRemaining, duration);
        UpdateSilenceVisual();
    }

    public bool IsSilenced()
    {
        return _silenceDurationRemaining > 0f;
    }

    private void TickSilence()
    {
        if (_silenceDurationRemaining <= 0f)
        {
            RestoreRendererColors();
            return;
        }

        _silenceDurationRemaining -= Time.deltaTime;
        if (_silenceDurationRemaining <= 0f)
        {
            _silenceDurationRemaining = 0f;
            RestoreRendererColors();
            return;
        }

        UpdateSilenceVisual();
    }

    private void CacheRendererColors()
    {
        _cachedRenderers = GetComponentsInChildren<Renderer>(true);
        _originalColors = new Color[_cachedRenderers.Length];

        for (int i = 0; i < _cachedRenderers.Length; i++)
        {
            Renderer rendererComponent = _cachedRenderers[i];
            _originalColors[i] = rendererComponent != null && rendererComponent.material.HasProperty("_Color")
                ? rendererComponent.material.color
                : Color.white;
        }
    }

    private void UpdateSilenceVisual()
    {
        if (_cachedRenderers == null || _originalColors == null)
        {
            return;
        }

        float pulse = 0.5f + Mathf.Sin(Time.time * 8f) * 0.5f;
        float tintStrength = SilenceTintStrength * (0.55f + pulse * 0.45f);

        for (int i = 0; i < _cachedRenderers.Length; i++)
        {
            Renderer rendererComponent = _cachedRenderers[i];
            if (rendererComponent == null || !rendererComponent.material.HasProperty("_Color"))
            {
                continue;
            }

            rendererComponent.material.color = Color.Lerp(_originalColors[i], SilenceTintColor, tintStrength);
        }
    }

    private void RestoreRendererColors()
    {
        if (_cachedRenderers == null || _originalColors == null)
        {
            return;
        }

        for (int i = 0; i < _cachedRenderers.Length; i++)
        {
            Renderer rendererComponent = _cachedRenderers[i];
            if (rendererComponent == null || !rendererComponent.material.HasProperty("_Color"))
            {
                continue;
            }

            rendererComponent.material.color = _originalColors[i];
        }
    }
}
