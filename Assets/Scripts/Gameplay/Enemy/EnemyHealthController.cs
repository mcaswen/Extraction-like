using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 敌人生命控制器。
/// 负责受伤、血条刷新、死亡和死亡掉落容器生成。
/// </summary>
public class EnemyHealthController : MonoBehaviour
{
    public float MaxHealth = 100f;

    [Header("Health UI")]
    public Image HealthFillImage;

    [Header("Death Loot")]
    public bool SpawnLootContainerOnDeath = true;
    public GameObject DeathLootContainerPrefab;
    public Transform DeathLootSpawnPoint;
    public Vector3 DeathLootSpawnOffset = new Vector3(0f, 0.1f, 0f);

    private float _currentHealth;
    private bool _hasDied;

    private void Start()
    {
        _currentHealth = MaxHealth;
        UpdateHealthBar();
    }

    /// <summary>
    /// 对敌人造成伤害。
    /// </summary>
    public void TakeDamage(float damageAmount)
    {
        if (_hasDied)
        {
            return;
        }

        _currentHealth -= damageAmount;
        _currentHealth = Mathf.Clamp(_currentHealth, 0f, MaxHealth);

        UpdateHealthBar();
        if (_currentHealth <= 0f)
        {
            Die();
        }
    }

    private void UpdateHealthBar()
    {
        if (HealthFillImage != null)
        {
            HealthFillImage.fillAmount = MaxHealth <= 0f ? 0f : _currentHealth / MaxHealth;
        }
    }

    /// <summary>
    /// 处理敌人死亡。
    /// </summary>
    private void Die()
    {
        if (_hasDied)
        {
            return;
        }

        _hasDied = true;
        Debug.Log("敌人死亡。");
        SpawnDeathLootContainer();
        Destroy(gameObject);
    }

    /// <summary>
    /// 在敌人死亡位置生成一个真实可交互的尸体盒子/宝箱。
    /// </summary>
    private void SpawnDeathLootContainer()
    {
        if (!SpawnLootContainerOnDeath || DeathLootContainerPrefab == null)
        {
            return;
        }

        Vector3 spawnPosition = DeathLootSpawnPoint != null
            ? DeathLootSpawnPoint.position
            : transform.position + DeathLootSpawnOffset;
        Quaternion spawnRotation = DeathLootSpawnPoint != null
            ? DeathLootSpawnPoint.rotation
            : Quaternion.identity;

        GameObject lootContainerObject = Instantiate(DeathLootContainerPrefab, spawnPosition, spawnRotation);
        LootBoxEntity lootBox = lootContainerObject.GetComponent<LootBoxEntity>();
        if (lootBox == null)
        {
            lootBox = lootContainerObject.GetComponentInChildren<LootBoxEntity>();
        }

        if (lootBox != null)
        {
            lootBox.PrecalculateLootIfNeeded();
        }
        else
        {
            Debug.LogWarning($"[{name}] 死亡掉落预制体 {DeathLootContainerPrefab.name} 上没有找到 LootBoxEntity。");
        }
    }
}
