using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player ranged combat + magic abilities + unlock progression.
/// </summary>
public class PlayerShootingController : MonoBehaviour
{
    private enum LearnedPrimarySkill
    {
        FireShot,
        IceFreeze,
        IceCone
    }

    public static PlayerShootingController Instance { get; private set; }

    public Transform FirePoint;

    [Header("Bullet")]
    public GameObject BulletPrefab;
    public float WeaponDamage = 25f;
    public float WeaponRange = 100f;
    public float FrozenFireBonusMultiplier = 2.2f;

    [Header("Status Effect")]
    public float SilenceTintStrength = 0.55f;
    public Color SilenceTintColor = new Color(0.32f, 0.82f, 1f, 1f);

    [Header("Magic Unlock (Runtime)")]
    public bool StartWithIceFreezeUnlocked = true;
    public bool StartWithIceConeUnlocked = true;
    public bool StartWithEarthWallUnlocked = true;
    public bool StartWithTravelerBootsUnlocked = true;
    public bool StartWithTimeHourglassUnlocked = true;
    public bool StartWithSpaceHourglassUnlocked = true;
    public int StartWithRunePatternLevel = 1;

    [Header("Rune Growth")]
    public float AttackBonusPerRuneLevel = 0.12f;
    public float DefenseBonusPerRuneLevel = 0.08f;
    public float MaxHealthBonusPerRuneLevel = 0.1f;

    [Header("Ice Freeze Spell")]
    public KeyCode IceFreezeKey = KeyCode.Alpha2;
    public float IceFreezeCooldownSeconds = 4f;
    public float IceFreezeRange = 24f;
    public float IceFreezeDamage = 22f;
    public float IceFreezeDuration = 2.2f;

    [Header("Ice Freeze Visual")]
    public bool EnableIceFreezeVisual = true;
    public Color IceFreezeIndicatorColor = new Color(0.52f, 0.92f, 1f, 0.45f);
    public float IceFreezeIndicatorDuration = 0.32f;
    public float IceFreezeIndicatorHeightOffset = 0.05f;
    public float IceFreezeIndicatorRadius = 1.15f;
    public float IceFreezeIndicatorInnerRadius = 0.75f;
    [Range(6, 64)]
    public int IceFreezeIndicatorSegments = 24;
    public int IceFreezePillarCount = 8;
    public float IceFreezePillarLifetime = 0.48f;
    public float IceFreezePillarSpreadRadius = 1.35f;
    public Vector2 IceFreezePillarWidthRange = new Vector2(0.1f, 0.2f);
    public Vector2 IceFreezePillarHeightRange = new Vector2(1f, 1.9f);
    public Color IceFreezePillarColor = new Color(0.74f, 0.96f, 1f, 1f);

    [Header("Ice Cone Spell")]
    public KeyCode IceConeKey = KeyCode.Alpha3;
    public float IceConeCooldownSeconds = 5f;
    public float IceConeRange = 9f;
    public float IceConeAngle = 65f;
    public float IceConeDamage = 34f;
    public float IceConeSlowMultiplier = 0.45f;
    public float IceConeSlowDuration = 2.5f;

    [Header("Ice Cone Visual")]
    public bool EnableIceConeVisual = true;
    public Color IceConeIndicatorColor = new Color(0.38f, 0.86f, 1f, 0.45f);
    public float IceConeIndicatorDuration = 0.35f;
    public float IceConeIndicatorHeightOffset = 0.06f;
    [Range(4, 64)]
    public int IceConeIndicatorSegments = 20;
    public int IceSpikeCount = 9;
    public float IceSpikeLifetime = 0.5f;
    public Vector2 IceSpikeWidthRange = new Vector2(0.14f, 0.24f);
    public Vector2 IceSpikeHeightRange = new Vector2(0.8f, 1.6f);
    public float IceSpikeSpawnInnerRadius = 1.5f;
    public Color IceSpikeColor = new Color(0.68f, 0.92f, 1f, 1f);

    [Header("Earth Wall")]
    public KeyCode EarthWallKey = KeyCode.Q;
    public float EarthWallCooldownSeconds = 7f;
    public float EarthWallLifetime = 6f;
    public float EarthWallForwardDistance = 1.8f;
    public Vector3 EarthWallSize = new Vector3(2.8f, 2.3f, 0.45f);

    [Header("Traveler Boots Dash")]
    public KeyCode TravelerBootsKey = KeyCode.LeftShift;
    public float DashCooldownSeconds = 3.5f;
    public float DashImpulseStrength = 16f;

    [Header("Time Hourglass")]
    public KeyCode TimeHourglassKey = KeyCode.Z;
    public float TimeHourglassCooldownSeconds = 14f;
    public float TimeHourglassDuration = 3f;
    public float TimeSlowScale = 0.35f;

    [Header("Space Hourglass")]
    public KeyCode SpaceHourglassKey = KeyCode.X;
    public float SpaceHourglassCooldownSeconds = 12f;
    public float SpaceHourglassDuration = 4f;
    public float SpaceSealRadius = 7f;
    public float SpaceSealPulseInterval = 0.2f;
    public float SpaceSealDuration = 0.45f;

    [Header("Skill Panel")]
    public KeyCode SkillPanelToggleKey = KeyCode.K;
    public bool AutoSelectNewUnlockedSkill = true;
    public bool CloseSkillPanelAfterSelection = true;
    public bool CloseSkillPanelAfterPrimaryCast;
    public bool EnableLegacySkillHotkeys;
    public bool ShowSelectedSkillHud = true;
    public bool ShowUtilitySkillHud = true;
    public Vector2 SkillPanelPosition = new Vector2(20f, 92f);
    public Vector2 SkillPanelSize = new Vector2(320f, 288f);

    private float _silenceDurationRemaining;
    private Renderer[] _cachedRenderers;
    private Color[] _originalColors;
    private Camera _mainCamera;
    private PlayerMovementController _playerMovementController;
    private PlayerHealthController _playerHealthController;
    private float _defaultFixedDeltaTime;

    private bool _iceFreezeUnlocked;
    private bool _iceConeUnlocked;
    private bool _earthWallUnlocked;
    private bool _travelerBootsUnlocked;
    private bool _timeHourglassUnlocked;
    private bool _spaceHourglassUnlocked;
    private int _runePatternLevel;

    private float _iceFreezeCooldownRemaining;
    private float _iceConeCooldownRemaining;
    private float _earthWallCooldownRemaining;
    private float _dashCooldownRemaining;
    private float _timeHourglassCooldownRemaining;
    private float _spaceHourglassCooldownRemaining;

    private bool _timeHourglassActive;
    private float _timeHourglassRemaining;
    private bool _spaceHourglassActive;
    private float _spaceHourglassRemaining;
    private float _spaceSealPulseRemaining;

    private Material _iceFreezeIndicatorMaterial;
    private Material _iceFreezePillarMaterial;
    private Material _iceConeIndicatorMaterial;
    private Material _iceSpikeMaterial;

    private string _latestUnlockMessage = string.Empty;
    private float _unlockMessageRemaining;
    private LearnedPrimarySkill _selectedPrimarySkill = LearnedPrimarySkill.FireShot;
    private bool _isSkillPanelVisible;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        _defaultFixedDeltaTime = Time.fixedDeltaTime;
    }

    private void Start()
    {
        CacheRendererColors();
        _mainCamera = Camera.main;
        _playerMovementController = GetComponent<PlayerMovementController>();
        _playerHealthController = GetComponent<PlayerHealthController>();

        _iceFreezeUnlocked = StartWithIceFreezeUnlocked;
        _iceConeUnlocked = StartWithIceConeUnlocked;
        _earthWallUnlocked = StartWithEarthWallUnlocked;
        _travelerBootsUnlocked = StartWithTravelerBootsUnlocked;
        _timeHourglassUnlocked = StartWithTimeHourglassUnlocked;
        _spaceHourglassUnlocked = StartWithSpaceHourglassUnlocked;
        _runePatternLevel = Mathf.Max(0, StartWithRunePatternLevel);
        ApplyRuneCombatEnhancement();
        EnsureSelectedSkillAvailable();
    }

    private void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (_timeHourglassActive)
        {
            DeactivateTimeHourglass();
        }

        RestoreRendererColors();
        DestroyRuntimeMaterial(ref _iceFreezeIndicatorMaterial);
        DestroyRuntimeMaterial(ref _iceFreezePillarMaterial);
        DestroyRuntimeMaterial(ref _iceConeIndicatorMaterial);
        DestroyRuntimeMaterial(ref _iceSpikeMaterial);
    }

    private void Update()
    {
        TickSilence();
        TickCooldowns();
        TickUnlockMessage();
        TickTimeHourglass();
        TickSpaceHourglass();

        if (Input.GetKeyDown(SkillPanelToggleKey))
        {
            ToggleSkillPanel();
        }

        if (RaidFlowController.Instance != null && RaidFlowController.Instance.IsInputLocked)
        {
            return;
        }

        if (InventoryScreenController.Instance != null && InventoryScreenController.Instance.IsInventoryOpen)
        {
            return;
        }

        if (_silenceDurationRemaining > 0f)
        {
            return;
        }

        if (_isSkillPanelVisible)
        {
            return;
        }

        HandleUtilitySkillInput();

        if (EnableLegacySkillHotkeys)
        {
            HandleAttackSkillHotkeys();
        }

        if (Input.GetMouseButtonDown(0))
        {
            TryCastSelectedPrimarySkill();
        }
    }

    private void OnGUI()
    {
        if (ShowSelectedSkillHud)
        {
            DrawSelectedSkillHud();
        }

        if (ShowUtilitySkillHud)
        {
            DrawUtilitySkillHud();
        }

        if (_isSkillPanelVisible)
        {
            DrawSkillPanel();
        }

        if (_unlockMessageRemaining <= 0f || string.IsNullOrEmpty(_latestUnlockMessage))
        {
            return;
        }

        Rect hintRect = new Rect(Screen.width - 560f, 16f, 540f, 28f);
        GUI.Box(hintRect, string.Empty);
        GUI.Label(new Rect(hintRect.x + 10f, hintRect.y + 6f, hintRect.width - 20f, 20f), _latestUnlockMessage);
    }

    public void TryUnlockFromItem(InventoryItemData itemData)
    {
        if (itemData == null)
        {
            return;
        }

        MagicUnlockType unlockType = itemData.MagicUnlock;
        if (unlockType == MagicUnlockType.None)
        {
            unlockType = GuessUnlockTypeFromItemName(itemData.ItemName);
        }

        if (unlockType == MagicUnlockType.None)
        {
            return;
        }

        TryUnlockMagic(unlockType, Mathf.Max(1, itemData.RunePatternPoints), itemData.ItemName);
    }

    public bool TryUnlockMagic(MagicUnlockType unlockType, int runePatternPoints = 1, string sourceName = null)
    {
        if (unlockType == MagicUnlockType.None)
        {
            return false;
        }

        bool didUnlock = false;
        int resolvedRunePoints = Mathf.Max(1, runePatternPoints);

        switch (unlockType)
        {
            case MagicUnlockType.IceFreeze:
                didUnlock = SetUnlock(ref _iceFreezeUnlocked);
                break;
            case MagicUnlockType.IceCone:
                didUnlock = SetUnlock(ref _iceConeUnlocked);
                break;
            case MagicUnlockType.EarthWall:
                didUnlock = SetUnlock(ref _earthWallUnlocked);
                break;
            case MagicUnlockType.TravelerBoots:
                didUnlock = SetUnlock(ref _travelerBootsUnlocked);
                break;
            case MagicUnlockType.TimeHourglass:
                didUnlock = SetUnlock(ref _timeHourglassUnlocked);
                break;
            case MagicUnlockType.SpaceHourglass:
                didUnlock = SetUnlock(ref _spaceHourglassUnlocked);
                break;
            case MagicUnlockType.RunePattern:
                _runePatternLevel += resolvedRunePoints;
                ApplyRuneCombatEnhancement();
                didUnlock = true;
                break;
        }

        if (!didUnlock)
        {
            return false;
        }

        if (AutoSelectNewUnlockedSkill && TryMapUnlockToPrimarySkill(unlockType, out LearnedPrimarySkill unlockedSkill))
        {
            _selectedPrimarySkill = unlockedSkill;
        }

        string unlockName = GetUnlockDisplayName(unlockType, resolvedRunePoints);
        string usageSuffix = GetUnlockUsageHint(unlockType);
        string sourceSuffix = string.IsNullOrWhiteSpace(sourceName) ? string.Empty : $"（来源：{sourceName}）";
        _latestUnlockMessage = $"已解锁：{unlockName}{usageSuffix}{sourceSuffix}";
        _unlockMessageRemaining = 3f;
        Debug.Log($"[Magic Unlock] {_latestUnlockMessage}");
        return true;
    }

    /// <summary>
    /// Applies silence effect during which shooting and magic skills are unavailable.
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

    private void ToggleSkillPanel()
    {
        if (RaidFlowController.Instance != null && RaidFlowController.Instance.IsInputLocked)
        {
            return;
        }

        if (InventoryScreenController.Instance != null && InventoryScreenController.Instance.IsInventoryOpen)
        {
            return;
        }

        _isSkillPanelVisible = !_isSkillPanelVisible;
        EnsureSelectedSkillAvailable();
    }

    private bool TryCastSelectedPrimarySkill()
    {
        EnsureSelectedSkillAvailable();

        bool didCast = _selectedPrimarySkill switch
        {
            LearnedPrimarySkill.FireShot => TryShoot(),
            LearnedPrimarySkill.IceFreeze => TryCastIceFreeze(),
            LearnedPrimarySkill.IceCone => TryCastIceCone(),
            _ => false
        };

        if (didCast && CloseSkillPanelAfterPrimaryCast)
        {
            _isSkillPanelVisible = false;
        }

        return didCast;
    }

    private void DrawSelectedSkillHud()
    {
        string currentSkillName = GetPrimarySkillDisplayName(_selectedPrimarySkill);
        float cooldown = GetSkillCooldownRemaining(_selectedPrimarySkill);
        string cooldownText = cooldown > 0f ? $"  冷却: {cooldown:0.0}s" : string.Empty;

        Rect hudRect = new Rect(16f, Screen.height - 52f, 440f, 34f);
        GUI.Box(hudRect, string.Empty);
        GUI.Label(
            new Rect(hudRect.x + 10f, hudRect.y + 8f, hudRect.width - 20f, 20f),
            $"当前左键攻击: {currentSkillName}{cooldownText}    [{SkillPanelToggleKey}] 攻击技能面板");
    }

    private void DrawUtilitySkillHud()
    {
        string utilityHint = GetUnlockedUtilitySkillHintText(includeCooldown: true);
        if (string.IsNullOrEmpty(utilityHint))
        {
            return;
        }

        float hudWidth = Mathf.Min(Screen.width - 32f, 760f);
        Rect hudRect = new Rect(16f, Screen.height - 86f, hudWidth, 30f);
        GUI.Box(hudRect, string.Empty);
        GUI.Label(
            new Rect(hudRect.x + 10f, hudRect.y + 6f, hudRect.width - 20f, 20f),
            utilityHint);
    }

    private void DrawSkillPanel()
    {
        List<LearnedPrimarySkill> learnedSkills = GetLearnedPrimarySkills();
        Rect panelRect = new Rect(
            Mathf.Max(12f, SkillPanelPosition.x),
            Mathf.Max(12f, SkillPanelPosition.y),
            Mathf.Max(260f, SkillPanelSize.x),
            Mathf.Max(180f, SkillPanelSize.y));
        GUI.Box(panelRect, "已学习攻击技能（左键释放）");

        if (learnedSkills.Count <= 0)
        {
            GUI.Label(new Rect(panelRect.x + 12f, panelRect.y + 36f, panelRect.width - 24f, 20f), "暂无可用技能");
            return;
        }

        float buttonX = panelRect.x + 12f;
        float buttonY = panelRect.y + 36f;
        float buttonWidth = panelRect.width - 24f;
        const float buttonHeight = 30f;
        const float buttonGap = 6f;

        for (int i = 0; i < learnedSkills.Count; i++)
        {
            LearnedPrimarySkill skill = learnedSkills[i];
            bool isSelected = skill == _selectedPrimarySkill;
            float cooldown = GetSkillCooldownRemaining(skill);
            string cooldownText = cooldown > 0f ? $"（冷却 {cooldown:0.0}s）" : string.Empty;
            string caption = $"{(isSelected ? "● " : string.Empty)}{GetPrimarySkillDisplayName(skill)} {cooldownText}";

            Rect buttonRect = new Rect(buttonX, buttonY, buttonWidth, buttonHeight);
            if (GUI.Button(buttonRect, caption))
            {
                _selectedPrimarySkill = skill;
                if (CloseSkillPanelAfterSelection)
                {
                    _isSkillPanelVisible = false;
                    break;
                }
            }

            buttonY += buttonHeight + buttonGap;
            if (buttonY + buttonHeight > panelRect.y + panelRect.height)
            {
                break;
            }
        }

        Rect footerRect = new Rect(panelRect.x + 12f, panelRect.yMax - 24f, panelRect.width - 24f, 20f);
        GUI.Label(footerRect, GetUtilitySkillHintText());
    }

    private List<LearnedPrimarySkill> GetLearnedPrimarySkills()
    {
        List<LearnedPrimarySkill> learnedSkills = new List<LearnedPrimarySkill> { LearnedPrimarySkill.FireShot };

        if (_iceFreezeUnlocked)
        {
            learnedSkills.Add(LearnedPrimarySkill.IceFreeze);
        }

        if (_iceConeUnlocked)
        {
            learnedSkills.Add(LearnedPrimarySkill.IceCone);
        }

        return learnedSkills;
    }

    private void EnsureSelectedSkillAvailable()
    {
        if (IsSkillLearned(_selectedPrimarySkill))
        {
            return;
        }

        _selectedPrimarySkill = LearnedPrimarySkill.FireShot;
    }

    private bool IsSkillLearned(LearnedPrimarySkill skill)
    {
        return skill switch
        {
            LearnedPrimarySkill.FireShot => true,
            LearnedPrimarySkill.IceFreeze => _iceFreezeUnlocked,
            LearnedPrimarySkill.IceCone => _iceConeUnlocked,
            _ => false
        };
    }

    private static bool TryMapUnlockToPrimarySkill(MagicUnlockType unlockType, out LearnedPrimarySkill skill)
    {
        switch (unlockType)
        {
            case MagicUnlockType.IceFreeze:
                skill = LearnedPrimarySkill.IceFreeze;
                return true;
            case MagicUnlockType.IceCone:
                skill = LearnedPrimarySkill.IceCone;
                return true;
            default:
                skill = LearnedPrimarySkill.FireShot;
                return false;
        }
    }

    private static string GetPrimarySkillDisplayName(LearnedPrimarySkill skill)
    {
        switch (skill)
        {
            case LearnedPrimarySkill.FireShot:
                return "火焰射击";
            case LearnedPrimarySkill.IceFreeze:
                return "冰冻术";
            case LearnedPrimarySkill.IceCone:
                return "冰锥术";
            default:
                return skill.ToString();
        }
    }

    private float GetSkillCooldownRemaining(LearnedPrimarySkill skill)
    {
        return skill switch
        {
            LearnedPrimarySkill.IceFreeze => _iceFreezeCooldownRemaining,
            LearnedPrimarySkill.IceCone => _iceConeCooldownRemaining,
            _ => 0f
        };
    }

    private void HandleAttackSkillHotkeys()
    {
        if (_iceFreezeUnlocked && Input.GetKeyDown(IceFreezeKey))
        {
            TryCastIceFreeze();
        }

        if (_iceConeUnlocked && Input.GetKeyDown(IceConeKey))
        {
            TryCastIceCone();
        }
    }

    private void HandleUtilitySkillInput()
    {
        if (_earthWallUnlocked && Input.GetKeyDown(EarthWallKey))
        {
            TryCastEarthWall();
        }

        if (_travelerBootsUnlocked && Input.GetKeyDown(TravelerBootsKey))
        {
            TryCastTravelerDash();
        }

        if (_timeHourglassUnlocked && Input.GetKeyDown(TimeHourglassKey))
        {
            TryCastTimeHourglass();
        }

        if (_spaceHourglassUnlocked && Input.GetKeyDown(SpaceHourglassKey))
        {
            TryCastSpaceHourglass();
        }
    }

    private string GetUtilitySkillHintText()
    {
        string utilityHint = GetUnlockedUtilitySkillHintText(includeCooldown: false);
        if (string.IsNullOrEmpty(utilityHint))
        {
            return "提示: 选中后使用鼠标左键释放攻击技能";
        }

        return utilityHint;
    }

    private string GetUnlockedUtilitySkillHintText(bool includeCooldown)
    {
        List<string> keyHints = new List<string>();
        if (_earthWallUnlocked)
        {
            keyHints.Add($"土墙[{EarthWallKey}]{FormatCooldownSuffix(_earthWallCooldownRemaining, includeCooldown)}");
        }

        if (_travelerBootsUnlocked)
        {
            keyHints.Add($"位移[{TravelerBootsKey}]{FormatCooldownSuffix(_dashCooldownRemaining, includeCooldown)}");
        }

        if (_timeHourglassUnlocked)
        {
            keyHints.Add($"时间沙漏[{TimeHourglassKey}]{FormatCooldownSuffix(_timeHourglassCooldownRemaining, includeCooldown)}");
        }

        if (_spaceHourglassUnlocked)
        {
            keyHints.Add($"空间沙漏[{SpaceHourglassKey}]{FormatCooldownSuffix(_spaceHourglassCooldownRemaining, includeCooldown)}");
        }

        if (keyHints.Count <= 0)
        {
            return string.Empty;
        }

        return $"功能技能按键: {string.Join("  ", keyHints)}";
    }

    private static string FormatCooldownSuffix(float cooldown, bool includeCooldown)
    {
        if (!includeCooldown || cooldown <= 0f)
        {
            return string.Empty;
        }

        return $"({cooldown:0.0}s)";
    }

    private bool TryShoot()
    {
        if (FirePoint == null || BulletPrefab == null)
        {
            Debug.LogWarning("Please assign FirePoint and BulletPrefab on PlayerShootingController.");
            return false;
        }

        GameObject bulletObject = Instantiate(BulletPrefab, FirePoint.position, FirePoint.rotation);
        BulletController bullet = bulletObject.GetComponent<BulletController>();
        if (bullet != null)
        {
            bullet.Damage = WeaponDamage * GetAttackMultiplier();
            bullet.AttackElement = BulletController.AttackElementType.Fire;
            bullet.FrozenFireBonusMultiplier = FrozenFireBonusMultiplier;
        }

        return true;
    }

    private bool TryCastIceFreeze()
    {
        if (_iceFreezeCooldownRemaining > 0f)
        {
            return false;
        }

        if (!TryFindEnemyInAimDirection(IceFreezeRange, out EnemyHealthController enemyHealthController))
        {
            return false;
        }

        EnemyStatusEffectController statusController = GetOrCreateEnemyStatus(enemyHealthController);
        statusController.ApplyFreeze(IceFreezeDuration);
        enemyHealthController.TakeDamage(IceFreezeDamage * GetAttackMultiplier());
        SpawnIceFreezeVisual(enemyHealthController.transform.position);
        _iceFreezeCooldownRemaining = IceFreezeCooldownSeconds;
        return true;
    }

    private bool TryCastIceCone()
    {
        if (_iceConeCooldownRemaining > 0f)
        {
            return false;
        }

        Vector3 origin = FirePoint != null ? FirePoint.position : transform.position + Vector3.up * 1f;
        Vector3 forward = transform.forward;
        Collider[] hits = Physics.OverlapSphere(origin, IceConeRange);
        HashSet<EnemyHealthController> uniqueTargets = new HashSet<EnemyHealthController>();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            EnemyHealthController enemyHealthController = hit.GetComponentInParent<EnemyHealthController>();
            if (enemyHealthController == null || uniqueTargets.Contains(enemyHealthController))
            {
                continue;
            }

            Vector3 directionToEnemy = enemyHealthController.transform.position - origin;
            directionToEnemy.y = 0f;
            if (directionToEnemy.sqrMagnitude <= 0.001f)
            {
                continue;
            }

            float angleToEnemy = Vector3.Angle(forward, directionToEnemy.normalized);
            if (angleToEnemy > IceConeAngle * 0.5f)
            {
                continue;
            }

            uniqueTargets.Add(enemyHealthController);
            EnemyStatusEffectController statusController = GetOrCreateEnemyStatus(enemyHealthController);
            statusController.ApplySlow(IceConeSlowMultiplier, IceConeSlowDuration);
            enemyHealthController.TakeDamage(IceConeDamage * GetAttackMultiplier());
        }

        SpawnIceConeVisual(origin, forward);
        _iceConeCooldownRemaining = IceConeCooldownSeconds;
        return true;
    }

    private bool TryCastEarthWall()
    {
        if (_earthWallCooldownRemaining > 0f)
        {
            return false;
        }

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.001f)
        {
            forward = Vector3.forward;
        }

        Vector3 spawnPosition = transform.position + forward.normalized * EarthWallForwardDistance;
        spawnPosition.y += EarthWallSize.y * 0.5f;

        GameObject wallObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallObject.name = "EarthWall";
        wallObject.transform.position = spawnPosition;
        wallObject.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        wallObject.transform.localScale = EarthWallSize;

        Rigidbody wallRigidbody = wallObject.AddComponent<Rigidbody>();
        wallRigidbody.isKinematic = true;
        wallRigidbody.useGravity = false;

        Renderer wallRenderer = wallObject.GetComponent<Renderer>();
        if (wallRenderer != null && wallRenderer.material != null)
        {
            wallRenderer.material.color = new Color(0.46f, 0.36f, 0.28f, 1f);
        }

        Destroy(wallObject, EarthWallLifetime);
        _earthWallCooldownRemaining = EarthWallCooldownSeconds;
        return true;
    }

    private bool TryCastTravelerDash()
    {
        if (_dashCooldownRemaining > 0f || _playerMovementController == null)
        {
            return false;
        }

        _playerMovementController.ApplyExternalImpulse(transform.forward, DashImpulseStrength);
        _dashCooldownRemaining = DashCooldownSeconds;
        return true;
    }

    private bool TryCastTimeHourglass()
    {
        if (_timeHourglassCooldownRemaining > 0f || _timeHourglassActive)
        {
            return false;
        }

        float clampedScale = Mathf.Clamp(TimeSlowScale, 0.05f, 1f);
        Time.timeScale = clampedScale;
        Time.fixedDeltaTime = _defaultFixedDeltaTime * clampedScale;

        if (_playerMovementController != null)
        {
            _playerMovementController.ApplyMoveSpeedMultiplier(1f / clampedScale, TimeHourglassDuration);
        }

        _timeHourglassActive = true;
        _timeHourglassRemaining = TimeHourglassDuration;
        _timeHourglassCooldownRemaining = TimeHourglassCooldownSeconds;
        return true;
    }

    private bool TryCastSpaceHourglass()
    {
        if (_spaceHourglassCooldownRemaining > 0f || _spaceHourglassActive)
        {
            return false;
        }

        _spaceHourglassActive = true;
        _spaceHourglassRemaining = SpaceHourglassDuration;
        _spaceSealPulseRemaining = 0f;
        _spaceHourglassCooldownRemaining = SpaceHourglassCooldownSeconds;
        return true;
    }

    private void SpawnIceFreezeVisual(Vector3 targetPosition)
    {
        if (!EnableIceFreezeVisual)
        {
            return;
        }

        float groundY = ResolveGroundY(targetPosition, targetPosition.y);
        Vector3 visualOrigin = new Vector3(
            targetPosition.x,
            groundY + IceFreezeIndicatorHeightOffset,
            targetPosition.z);

        SpawnIceFreezeGroundIndicator(visualOrigin);
        SpawnIceFreezePillars(visualOrigin);
    }

    private void SpawnIceFreezeGroundIndicator(Vector3 origin)
    {
        float duration = Mathf.Max(0.05f, IceFreezeIndicatorDuration);
        GameObject indicatorObject = new GameObject("IceFreezeGroundIndicator");
        indicatorObject.transform.position = origin;
        indicatorObject.transform.rotation = Quaternion.identity;

        MeshFilter meshFilter = indicatorObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = indicatorObject.AddComponent<MeshRenderer>();

        float outerRadius = Mathf.Max(0.2f, IceFreezeIndicatorRadius);
        float innerRadius = Mathf.Clamp(IceFreezeIndicatorInnerRadius, 0f, outerRadius - 0.03f);
        Mesh ringMesh = BuildRingMesh(outerRadius, innerRadius, Mathf.Clamp(IceFreezeIndicatorSegments, 6, 64));
        meshFilter.sharedMesh = ringMesh;
        meshRenderer.sharedMaterial = GetOrCreateIceFreezeIndicatorMaterial();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        Destroy(ringMesh, duration + 0.1f);
        Destroy(indicatorObject, duration);
    }

    private void SpawnIceFreezePillars(Vector3 origin)
    {
        int pillarCount = Mathf.Max(1, IceFreezePillarCount);
        float lifetime = Mathf.Max(0.05f, IceFreezePillarLifetime);
        float spreadRadius = Mathf.Max(0.1f, IceFreezePillarSpreadRadius);

        GameObject pillarsRoot = new GameObject("IceFreezePillars");
        pillarsRoot.transform.position = origin;
        pillarsRoot.transform.rotation = Quaternion.identity;

        for (int i = 0; i < pillarCount; i++)
        {
            float normalized = i / (float)pillarCount;
            float angle = normalized * 360f + Random.Range(-12f, 12f);
            float distance = Random.Range(0.15f, spreadRadius);

            Vector3 radialDirection = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 localPosition = radialDirection * distance;

            float width = Mathf.Max(0.04f, Random.Range(IceFreezePillarWidthRange.x, IceFreezePillarWidthRange.y));
            float height = Mathf.Max(0.1f, Random.Range(IceFreezePillarHeightRange.x, IceFreezePillarHeightRange.y));

            GameObject pillarObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillarObject.name = $"IceFreezePillar_{i + 1}";
            pillarObject.transform.SetParent(pillarsRoot.transform, false);
            pillarObject.transform.localPosition = localPosition + Vector3.up * (height * 0.5f);
            pillarObject.transform.localRotation = Quaternion.Euler(
                Random.Range(-4f, 4f),
                Random.Range(0f, 360f),
                Random.Range(-4f, 4f));
            pillarObject.transform.localScale = new Vector3(width, height * 0.5f, width);

            Collider colliderComponent = pillarObject.GetComponent<Collider>();
            if (colliderComponent != null)
            {
                Destroy(colliderComponent);
            }

            Renderer rendererComponent = pillarObject.GetComponent<Renderer>();
            if (rendererComponent != null)
            {
                rendererComponent.sharedMaterial = GetOrCreateIceFreezePillarMaterial();
                rendererComponent.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rendererComponent.receiveShadows = false;
            }
        }

        Destroy(pillarsRoot, lifetime);
    }

    private void SpawnIceConeVisual(Vector3 origin, Vector3 forward)
    {
        if (!EnableIceConeVisual)
        {
            return;
        }

        Vector3 planarForward = forward;
        planarForward.y = 0f;
        if (planarForward.sqrMagnitude <= 0.0001f)
        {
            planarForward = transform.forward;
            planarForward.y = 0f;
        }

        if (planarForward.sqrMagnitude <= 0.0001f)
        {
            planarForward = Vector3.forward;
        }

        float groundY = ResolveGroundY(origin, transform.position.y);
        Vector3 visualOrigin = new Vector3(origin.x, groundY + IceConeIndicatorHeightOffset, origin.z);

        SpawnIceConeGroundIndicator(visualOrigin, planarForward.normalized);
        SpawnIceConeSpikes(visualOrigin, planarForward.normalized);
    }

    private void SpawnIceConeGroundIndicator(Vector3 origin, Vector3 forward)
    {
        float duration = Mathf.Max(0.05f, IceConeIndicatorDuration);

        GameObject indicatorObject = new GameObject("IceConeGroundIndicator");
        indicatorObject.transform.position = origin;
        indicatorObject.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

        MeshFilter meshFilter = indicatorObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = indicatorObject.AddComponent<MeshRenderer>();

        Mesh sectorMesh = BuildSectorMesh(
            Mathf.Max(0.1f, IceConeRange),
            Mathf.Clamp(IceConeAngle, 5f, 179f),
            Mathf.Clamp(IceConeIndicatorSegments, 4, 64));
        meshFilter.sharedMesh = sectorMesh;
        meshRenderer.sharedMaterial = GetOrCreateIceConeIndicatorMaterial();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        Destroy(sectorMesh, duration + 0.1f);
        Destroy(indicatorObject, duration);
    }

    private void SpawnIceConeSpikes(Vector3 origin, Vector3 forward)
    {
        int spikeCount = Mathf.Max(1, IceSpikeCount);
        float duration = Mathf.Max(0.05f, IceSpikeLifetime);

        GameObject spikesRoot = new GameObject("IceConeSpikes");
        spikesRoot.transform.position = origin;
        spikesRoot.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

        float halfAngle = Mathf.Clamp(IceConeAngle, 5f, 179f) * 0.5f;
        float innerRadius = Mathf.Clamp(IceSpikeSpawnInnerRadius, 0.2f, IceConeRange);

        for (int i = 0; i < spikeCount; i++)
        {
            float t = spikeCount <= 1 ? 0.5f : i / (float)(spikeCount - 1);
            float angle = Mathf.Lerp(-halfAngle, halfAngle, t) + Random.Range(-3f, 3f);
            float distance = Mathf.Lerp(innerRadius, IceConeRange, t) + Random.Range(-0.35f, 0.35f);
            distance = Mathf.Clamp(distance, innerRadius, IceConeRange);

            Vector3 localDirection = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 localPosition = localDirection * distance;

            float width = Mathf.Max(0.05f, Random.Range(IceSpikeWidthRange.x, IceSpikeWidthRange.y));
            float height = Mathf.Max(0.1f, Random.Range(IceSpikeHeightRange.x, IceSpikeHeightRange.y));

            GameObject spikeObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            spikeObject.name = $"IceSpike_{i + 1}";
            spikeObject.transform.SetParent(spikesRoot.transform, false);
            spikeObject.transform.localPosition = localPosition + Vector3.up * (height * 0.5f);
            spikeObject.transform.localRotation = Quaternion.LookRotation(localDirection, Vector3.up);
            spikeObject.transform.localScale = new Vector3(width, height * 0.5f, width);

            Collider colliderComponent = spikeObject.GetComponent<Collider>();
            if (colliderComponent != null)
            {
                Destroy(colliderComponent);
            }

            Renderer rendererComponent = spikeObject.GetComponent<Renderer>();
            if (rendererComponent != null)
            {
                rendererComponent.sharedMaterial = GetOrCreateIceSpikeMaterial();
                rendererComponent.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rendererComponent.receiveShadows = false;
            }
        }

        Destroy(spikesRoot, duration);
    }

    private float ResolveGroundY(Vector3 samplePosition, float fallbackY)
    {
        RaycastHit[] hits = Physics.RaycastAll(
            samplePosition + Vector3.up * 3f,
            Vector3.down,
            16f,
            ~0,
            QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length <= 0)
        {
            return fallbackY;
        }

        float closestDistance = float.MaxValue;
        float resolvedY = fallbackY;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hitInfo = hits[i];
            if (hitInfo.collider == null)
            {
                continue;
            }

            if (hitInfo.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hitInfo.distance < closestDistance)
            {
                closestDistance = hitInfo.distance;
                resolvedY = hitInfo.point.y;
            }
        }

        return resolvedY;
    }

    private Mesh BuildSectorMesh(float radius, float angle, int segments)
    {
        Mesh mesh = new Mesh
        {
            name = "IceConeSectorMesh"
        };

        int vertexCount = segments + 2;
        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uv = new Vector2[vertexCount];
        int[] triangles = new int[segments * 6];

        vertices[0] = Vector3.zero;
        uv[0] = new Vector2(0.5f, 0.5f);

        float halfAngle = angle * 0.5f;
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float currentAngle = Mathf.Lerp(-halfAngle, halfAngle, t) * Mathf.Deg2Rad;
            float x = Mathf.Sin(currentAngle) * radius;
            float z = Mathf.Cos(currentAngle) * radius;

            int vertexIndex = i + 1;
            vertices[vertexIndex] = new Vector3(x, 0f, z);
            uv[vertexIndex] = new Vector2(0.5f + x / (radius * 2f), 0.5f + z / (radius * 2f));

            if (i < segments)
            {
                int triangleIndex = i * 6;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = vertexIndex;
                triangles[triangleIndex + 2] = vertexIndex + 1;
                triangles[triangleIndex + 3] = 0;
                triangles[triangleIndex + 4] = vertexIndex + 1;
                triangles[triangleIndex + 5] = vertexIndex;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

    private Mesh BuildRingMesh(float outerRadius, float innerRadius, int segments)
    {
        Mesh mesh = new Mesh
        {
            name = "IceFreezeRingMesh"
        };

        int clampedSegments = Mathf.Clamp(segments, 6, 64);
        float safeOuter = Mathf.Max(0.1f, outerRadius);
        float safeInner = Mathf.Clamp(innerRadius, 0f, safeOuter - 0.01f);

        int vertexCount = (clampedSegments + 1) * 2;
        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uv = new Vector2[vertexCount];
        int[] triangles = new int[clampedSegments * 12];

        for (int i = 0; i <= clampedSegments; i++)
        {
            float t = i / (float)clampedSegments;
            float angle = t * Mathf.PI * 2f;
            float sin = Mathf.Sin(angle);
            float cos = Mathf.Cos(angle);

            int vertexIndex = i * 2;
            vertices[vertexIndex] = new Vector3(sin * safeOuter, 0f, cos * safeOuter);
            vertices[vertexIndex + 1] = new Vector3(sin * safeInner, 0f, cos * safeInner);
            uv[vertexIndex] = new Vector2(0.5f + sin * 0.5f, 0.5f + cos * 0.5f);
            uv[vertexIndex + 1] = new Vector2(0.5f + sin * 0.25f, 0.5f + cos * 0.25f);

            if (i >= clampedSegments)
            {
                continue;
            }

            int nextVertexIndex = vertexIndex + 2;
            int triangleIndex = i * 12;

            triangles[triangleIndex] = vertexIndex;
            triangles[triangleIndex + 1] = nextVertexIndex;
            triangles[triangleIndex + 2] = vertexIndex + 1;
            triangles[triangleIndex + 3] = nextVertexIndex;
            triangles[triangleIndex + 4] = nextVertexIndex + 1;
            triangles[triangleIndex + 5] = vertexIndex + 1;

            triangles[triangleIndex + 6] = vertexIndex;
            triangles[triangleIndex + 7] = vertexIndex + 1;
            triangles[triangleIndex + 8] = nextVertexIndex;
            triangles[triangleIndex + 9] = nextVertexIndex;
            triangles[triangleIndex + 10] = vertexIndex + 1;
            triangles[triangleIndex + 11] = nextVertexIndex + 1;
        }

        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

    private Material GetOrCreateIceFreezeIndicatorMaterial()
    {
        if (_iceFreezeIndicatorMaterial == null)
        {
            Shader shader = Shader.Find("Legacy Shaders/Transparent/Diffuse");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            _iceFreezeIndicatorMaterial = new Material(shader)
            {
                name = "Runtime_IceFreezeIndicator"
            };
            _iceFreezeIndicatorMaterial.renderQueue = 3000;
        }

        ApplyColorToMaterial(_iceFreezeIndicatorMaterial, IceFreezeIndicatorColor);
        return _iceFreezeIndicatorMaterial;
    }

    private Material GetOrCreateIceFreezePillarMaterial()
    {
        if (_iceFreezePillarMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            _iceFreezePillarMaterial = new Material(shader)
            {
                name = "Runtime_IceFreezePillar"
            };
        }

        ApplyColorToMaterial(_iceFreezePillarMaterial, IceFreezePillarColor);
        return _iceFreezePillarMaterial;
    }

    private Material GetOrCreateIceConeIndicatorMaterial()
    {
        if (_iceConeIndicatorMaterial == null)
        {
            Shader shader = Shader.Find("Legacy Shaders/Transparent/Diffuse");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            _iceConeIndicatorMaterial = new Material(shader)
            {
                name = "Runtime_IceConeIndicator"
            };
            _iceConeIndicatorMaterial.renderQueue = 3000;
        }

        ApplyColorToMaterial(_iceConeIndicatorMaterial, IceConeIndicatorColor);
        return _iceConeIndicatorMaterial;
    }

    private Material GetOrCreateIceSpikeMaterial()
    {
        if (_iceSpikeMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            _iceSpikeMaterial = new Material(shader)
            {
                name = "Runtime_IceSpike"
            };
        }

        ApplyColorToMaterial(_iceSpikeMaterial, IceSpikeColor);
        return _iceSpikeMaterial;
    }

    private static void ApplyColorToMaterial(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.color = color;
        }
    }

    private static void DestroyRuntimeMaterial(ref Material material)
    {
        if (material == null)
        {
            return;
        }

        Destroy(material);
        material = null;
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

    private void TickCooldowns()
    {
        float deltaTime = Time.unscaledDeltaTime;
        _iceFreezeCooldownRemaining = Mathf.Max(0f, _iceFreezeCooldownRemaining - deltaTime);
        _iceConeCooldownRemaining = Mathf.Max(0f, _iceConeCooldownRemaining - deltaTime);
        _earthWallCooldownRemaining = Mathf.Max(0f, _earthWallCooldownRemaining - deltaTime);
        _dashCooldownRemaining = Mathf.Max(0f, _dashCooldownRemaining - deltaTime);
        _timeHourglassCooldownRemaining = Mathf.Max(0f, _timeHourglassCooldownRemaining - deltaTime);
        _spaceHourglassCooldownRemaining = Mathf.Max(0f, _spaceHourglassCooldownRemaining - deltaTime);
    }

    private void TickUnlockMessage()
    {
        if (_unlockMessageRemaining > 0f)
        {
            _unlockMessageRemaining -= Time.unscaledDeltaTime;
            if (_unlockMessageRemaining <= 0f)
            {
                _latestUnlockMessage = string.Empty;
            }
        }
    }

    private void TickTimeHourglass()
    {
        if (!_timeHourglassActive)
        {
            return;
        }

        _timeHourglassRemaining -= Time.unscaledDeltaTime;
        if (_timeHourglassRemaining <= 0f)
        {
            DeactivateTimeHourglass();
        }
    }

    private void TickSpaceHourglass()
    {
        if (!_spaceHourglassActive)
        {
            return;
        }

        _spaceHourglassRemaining -= Time.unscaledDeltaTime;
        _spaceSealPulseRemaining -= Time.unscaledDeltaTime;

        if (_spaceSealPulseRemaining <= 0f)
        {
            _spaceSealPulseRemaining = Mathf.Max(0.05f, SpaceSealPulseInterval);
            ApplySpaceSealPulse();
        }

        if (_spaceHourglassRemaining <= 0f)
        {
            _spaceHourglassActive = false;
        }
    }

    private void ApplySpaceSealPulse()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, SpaceSealRadius);
        HashSet<EnemyHealthController> uniqueTargets = new HashSet<EnemyHealthController>();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            EnemyHealthController enemyHealthController = hit.GetComponentInParent<EnemyHealthController>();
            if (enemyHealthController == null || uniqueTargets.Contains(enemyHealthController))
            {
                continue;
            }

            uniqueTargets.Add(enemyHealthController);
            EnemyStatusEffectController statusController = GetOrCreateEnemyStatus(enemyHealthController);
            statusController.ApplyMagicSeal(SpaceSealDuration);
        }
    }

    private void DeactivateTimeHourglass()
    {
        _timeHourglassActive = false;
        _timeHourglassRemaining = 0f;

        if (RaidFlowController.Instance == null || !RaidFlowController.Instance.IsInputLocked)
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = _defaultFixedDeltaTime;
        }
    }

    private bool TryFindEnemyInAimDirection(float maxDistance, out EnemyHealthController enemyHealthController)
    {
        enemyHealthController = null;
        Camera cameraToUse = _mainCamera != null ? _mainCamera : Camera.main;
        if (cameraToUse == null)
        {
            return false;
        }

        Ray ray = cameraToUse.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
        {
            enemyHealthController = hit.collider.GetComponentInParent<EnemyHealthController>();
            if (enemyHealthController != null)
            {
                return true;
            }
        }

        return false;
    }

    private EnemyStatusEffectController GetOrCreateEnemyStatus(EnemyHealthController enemyHealthController)
    {
        EnemyStatusEffectController statusController = enemyHealthController.GetComponent<EnemyStatusEffectController>();
        if (statusController == null)
        {
            statusController = enemyHealthController.gameObject.AddComponent<EnemyStatusEffectController>();
        }

        return statusController;
    }

    private float GetAttackMultiplier()
    {
        return 1f + Mathf.Max(0, _runePatternLevel) * AttackBonusPerRuneLevel;
    }

    private void ApplyRuneCombatEnhancement()
    {
        float defenseMultiplier = 1f + Mathf.Max(0, _runePatternLevel) * DefenseBonusPerRuneLevel;
        float healthMultiplier = 1f + Mathf.Max(0, _runePatternLevel) * MaxHealthBonusPerRuneLevel;
        if (_playerHealthController != null)
        {
            _playerHealthController.ApplyCombatEnhancement(defenseMultiplier, healthMultiplier);
        }
    }

    private static bool SetUnlock(ref bool targetFlag)
    {
        if (targetFlag)
        {
            return false;
        }

        targetFlag = true;
        return true;
    }

    private string GetUnlockUsageHint(MagicUnlockType unlockType)
    {
        switch (unlockType)
        {
            case MagicUnlockType.IceFreeze:
            case MagicUnlockType.IceCone:
                return "（已加入左键攻击技能面板）";
            case MagicUnlockType.EarthWall:
                return $"（按 [{EarthWallKey}] 使用）";
            case MagicUnlockType.TravelerBoots:
                return $"（按 [{TravelerBootsKey}] 使用）";
            case MagicUnlockType.TimeHourglass:
                return $"（按 [{TimeHourglassKey}] 使用）";
            case MagicUnlockType.SpaceHourglass:
                return $"（按 [{SpaceHourglassKey}] 使用）";
            case MagicUnlockType.RunePattern:
                return "（被动强化已生效）";
            default:
                return string.Empty;
        }
    }

    private static string GetUnlockDisplayName(MagicUnlockType unlockType, int runePatternPoints)
    {
        switch (unlockType)
        {
            case MagicUnlockType.IceFreeze:
                return "冰冻术";
            case MagicUnlockType.IceCone:
                return "冰锥术";
            case MagicUnlockType.EarthWall:
                return "土墙";
            case MagicUnlockType.RunePattern:
                return $"魔能纹路 +{Mathf.Max(1, runePatternPoints)}";
            case MagicUnlockType.TravelerBoots:
                return "远行者位移";
            case MagicUnlockType.TimeHourglass:
                return "时间沙漏";
            case MagicUnlockType.SpaceHourglass:
                return "空间沙漏";
            default:
                return unlockType.ToString();
        }
    }

    private static MagicUnlockType GuessUnlockTypeFromItemName(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName))
        {
            return MagicUnlockType.None;
        }

        string lowerName = itemName.ToLowerInvariant();
        string compactName = lowerName.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);

        if (ContainsAny(lowerName, compactName, "冰锥", "ice cone", "icecone", "frost cone", "frostcone"))
        {
            return MagicUnlockType.IceCone;
        }

        if (ContainsAny(lowerName, compactName, "冰冻", "冻结", "freeze", "frost", "icefreeze", "ice freeze"))
        {
            return MagicUnlockType.IceFreeze;
        }

        if (ContainsAny(lowerName, compactName, "土墙", "earth wall", "earthwall", "stone wall", "stonewall"))
        {
            return MagicUnlockType.EarthWall;
        }

        if (ContainsAny(lowerName, compactName, "远行者之靴", "traveler boots", "travelerboots", "dash boots", "dashboots"))
        {
            return MagicUnlockType.TravelerBoots;
        }

        if (ContainsAny(lowerName, compactName, "时间之沙漏", "time hourglass", "timehourglass", "sandevistan", "slow time", "slowtime"))
        {
            return MagicUnlockType.TimeHourglass;
        }

        if (ContainsAny(lowerName, compactName, "空间之沙漏", "space hourglass", "spacehourglass", "magic seal", "magicseal", "silence"))
        {
            return MagicUnlockType.SpaceHourglass;
        }

        if (ContainsAny(lowerName, compactName, "纹路", "rune", "pattern", "glyph"))
        {
            return MagicUnlockType.RunePattern;
        }

        return MagicUnlockType.None;
    }

    private static bool ContainsAny(string rawName, string compactName, params string[] tokens)
    {
        if (string.IsNullOrEmpty(rawName) || tokens == null || tokens.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            string lowerToken = token.ToLowerInvariant();
            string compactToken = lowerToken.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
            if (rawName.Contains(lowerToken) || compactName.Contains(compactToken))
            {
                return true;
            }
        }

        return false;
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


