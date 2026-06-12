using System.Collections.Generic;
using Gameplay.Agent.Talent;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay.TalentTree.UI
{
    /// <summary>
    /// 天赋树面板控制器。
    /// 负责把策划天赋树数据绑定到现有 UI 节点，并处理点击解锁和详情面板刷新。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TalentTreePanelController : MonoBehaviour
    {
        private const string RootSlotName = "Node_Start_Heal";
        private const string TopFirstSlotName = "Node_ArmorEntry";
        private const string TopSecondSlotName = "Node_HealMastery";
        private const string TopThirdSlotName = "Node_TopBranch_B";
        private const string BottomFirstSlotName = "Node_BladeEntry";
        private const string BottomSecondSlotName = "Node_BladeMastery";
        private const string BottomThirdSlotName = "Node_BottomBranch_A";
        private const string FinalSlotName = "Node_FinalCore";

        private static readonly string[] ConnectorNodeNames =
        {
            "Node_TopMerge",
            "Node_BottomMerge"
        };

        private static readonly string[] HiddenNodeNames =
        {
            "Node_TopBranch_A",
            "Node_TopBranch_C",
            "Node_BottomBranch_B"
        };

        [Header("Runtime")]
        [SerializeField] private AgentTalentRuntimeController _targetTalentRuntime;
        [SerializeField] private AgentTalentTreeKind _defaultTree = AgentTalentTreeKind.Earth;
        [SerializeField] private bool _autoFindTalentRuntime = true;

        [Header("UI")]
        [SerializeField] private bool _createTreeSwitchButtons = true;
        [SerializeField] private bool _hideUnusedNodeSlots = true;
        [SerializeField] private Text _treeTitleText;
        [SerializeField] private Text _gainTitleText;
        [SerializeField] private Text _requirementText;
        [SerializeField] private Text[] _descriptionTexts;

        [Header("Art")]
        [SerializeField] private Sprite _attackSprite;
        [SerializeField] private Sprite _defenseSprite;
        [SerializeField] private Sprite _healthSprite;
        [SerializeField] private Sprite _experienceSprite;
        [SerializeField] private Sprite _earthStoneSprite;
        [SerializeField] private Sprite _earthQuakeSprite;
        [SerializeField] private Sprite _iceFrostStrikeSprite;
        [SerializeField] private Sprite _iceWinterSprite;
        [SerializeField] private Sprite _specialNodeSprite;

        private readonly List<TalentTreeNodeView> _activeNodeViews = new List<TalentTreeNodeView>();
        private readonly Dictionary<string, Transform> _childByName = new Dictionary<string, Transform>();
        private AgentTalentTreeKind _currentTree;
        private bool _hasInitialized;

        /// <summary>
        /// 当前面板绑定的天赋运行时对象。
        /// </summary>
        public AgentTalentRuntimeController TargetTalentRuntime => _targetTalentRuntime;

        private void Awake()
        {
            InitializeIfNeeded();
        }

        private void OnEnable()
        {
            InitializeIfNeeded();
            ResolveTargetTalentRuntime();
            RefreshAll();
        }

        /// <summary>
        /// 设置面板绑定的 Agent 天赋运行时。
        /// </summary>
        /// <param name="talentRuntime">目标 Agent 上的天赋运行时组件。</param>
        public void SetTargetTalentRuntime(AgentTalentRuntimeController talentRuntime)
        {
            _targetTalentRuntime = talentRuntime;
            RefreshAll();
        }

        /// <summary>
        /// 切换到土系天赋树。
        /// </summary>
        public void ShowEarthTree()
        {
            ShowTree(AgentTalentTreeKind.Earth);
        }

        /// <summary>
        /// 切换到冰系天赋树。
        /// </summary>
        public void ShowIceTree()
        {
            ShowTree(AgentTalentTreeKind.Ice);
        }

        /// <summary>
        /// 切换到指定元素天赋树。
        /// </summary>
        /// <param name="treeKind">目标天赋树类型。</param>
        public void ShowTree(AgentTalentTreeKind treeKind)
        {
            InitializeIfNeeded();
            _currentTree = treeKind;
            BindVisibleTree();
            RefreshAll();
        }

        /// <summary>
        /// 尝试解锁指定 UI 节点绑定的天赋。
        /// </summary>
        /// <param name="nodeView">被点击的节点视图。</param>
        public void TryUnlockNode(TalentTreeNodeView nodeView)
        {
            if (nodeView == null)
                return;

            ResolveTargetTalentRuntime();
            if (_targetTalentRuntime == null)
            {
                ShowStatus("未绑定 Agent 天赋运行时");
                return;
            }

            if (_targetTalentRuntime.TryUnlockNode(nodeView.NodeId))
                ShowStatus("已解锁");
            else if (_targetTalentRuntime.IsUnlocked(nodeView.NodeId))
                ShowStatus("该节点已解锁");
            else
                ShowStatus(BuildPrerequisiteText(nodeView.NodeId));

            ShowNodeDetails(nodeView);
            RefreshAll();
        }

        /// <summary>
        /// 显示节点详情。
        /// </summary>
        /// <param name="nodeView">要展示的节点视图。</param>
        public void ShowNodeDetails(TalentTreeNodeView nodeView)
        {
            if (nodeView == null)
                return;

            SetDescriptionText(0, nodeView.Title);
            SetDescriptionText(1, nodeView.Description);
            SetDescriptionText(2, nodeView.Effect);

            if (_gainTitleText != null)
                _gainTitleText.text = "节点增益";

            ResolveTargetTalentRuntime();
            if (_targetTalentRuntime == null)
            {
                ShowStatus("未绑定 Agent 天赋运行时");
                return;
            }

            if (_targetTalentRuntime.IsUnlocked(nodeView.NodeId))
                ShowStatus("已解锁");
            else if (_targetTalentRuntime.CanUnlockNode(nodeView.NodeId))
                ShowStatus("点击解锁");
            else
                ShowStatus(BuildPrerequisiteText(nodeView.NodeId));
        }

        /// <summary>
        /// 刷新所有节点表现。
        /// </summary>
        public void RefreshAll()
        {
            InitializeIfNeeded();
            for (int i = 0; i < _activeNodeViews.Count; i++)
                _activeNodeViews[i]?.Refresh();
        }

        private void InitializeIfNeeded()
        {
            if (_hasInitialized)
                return;

            _hasInitialized = true;
            _currentTree = _defaultTree;
            CacheChildLookup();
            CacheTexts();
            ConfigureUnusedSlots();
            BindVisibleTree();

            if (_createTreeSwitchButtons)
                CreateTreeSwitchButtonsIfNeeded();
        }

        private void BindVisibleTree()
        {
            _activeNodeViews.Clear();
            SetTreeTitle(_currentTree == AgentTalentTreeKind.Earth ? "土系天赋" : "冰系天赋");

            if (_currentTree == AgentTalentTreeKind.Earth)
            {
                BindSlot(RootSlotName, AgentTalentNodeId.EarthAttack1, "攻击力 +1", "土系根节点。", "攻击属性提高 1 点。");
                BindSlot(TopFirstSlotName, AgentTalentNodeId.EarthHealth1, "生命值 +1", "上路线第一层。", "最大生命值提高 1 点。");
                BindSlot(TopSecondSlotName, AgentTalentNodeId.EarthStoneWallDuration, "磐石持续 +3s", "强化技能“磐石”。", "土墙持续时间增加 3 秒。");
                BindSlot(TopThirdSlotName, AgentTalentNodeId.EarthDefense1, "防御力 +1", "上路线第三层。", "防御属性提高 1 点。");
                BindSlot(BottomFirstSlotName, AgentTalentNodeId.EarthExperienceGain1, "经验获取 +5%", "下路线第一层。", "经验值获取效率提高 5%。");
                BindSlot(BottomSecondSlotName, AgentTalentNodeId.EarthQuakeDamagePerSecond, "撼动伤害 +20%", "强化技能“撼动”。", "每秒伤害倍率提高 20%。");
                BindSlot(BottomThirdSlotName, AgentTalentNodeId.EarthAttack2, "攻击力 +1", "下路线第三层。", "攻击属性提高 1 点。");
                BindSlot(FinalSlotName, AgentTalentNodeId.EarthReactiveShield, "受击护盾", "土系终点天赋。", "受伤后生成护盾，吸收防御力 * 200% 的伤害，持续 5 秒，冷却 120 秒。");
            }
            else
            {
                BindSlot(RootSlotName, AgentTalentNodeId.IceAttack1, "攻击力 +1", "冰系根节点。", "攻击属性提高 1 点。");
                BindSlot(TopFirstSlotName, AgentTalentNodeId.IceMaxHealth1, "最大生命值 +1", "上路线第一层。", "最大生命值提高 1 点。");
                BindSlot(TopSecondSlotName, AgentTalentNodeId.IceFrostAssaultSlowDuration, "霜袭减速 +3s", "强化技能“霜袭”。", "对敌人施加的减速效果持续时间增加 3 秒。");
                BindSlot(TopThirdSlotName, AgentTalentNodeId.IceDefense1, "防御力 +1", "上路线第三层。", "防御属性提高 1 点。");
                BindSlot(BottomFirstSlotName, AgentTalentNodeId.IceExperienceGain1, "经验获取 +5%", "下路线第一层。", "经验值获取效率提高 5%。");
                BindSlot(BottomSecondSlotName, AgentTalentNodeId.IceWinterfallDamage, "凛冬伤害 +50%", "强化技能“凛冬”。", "伤害倍率提高 50%。");
                BindSlot(BottomThirdSlotName, AgentTalentNodeId.IceAttack2, "攻击力 +1", "下路线第三层。", "攻击属性提高 1 点。");
                BindSlot(FinalSlotName, AgentTalentNodeId.IceKillAttackStack, "击杀叠攻", "冰系终点天赋。", "每消灭一个敌人，攻击力增加 10%，最多增加 50%。");
            }

            for (int i = 0; i < ConnectorNodeNames.Length; i++)
                ConfigureConnectorNode(ConnectorNodeNames[i]);

            if (_activeNodeViews.Count > 0)
                ShowNodeDetails(_activeNodeViews[0]);
        }

        private void BindSlot(
            string slotName,
            AgentTalentNodeId nodeId,
            string title,
            string description,
            string effect)
        {
            Transform slotTransform = FindCachedChild(slotName);
            if (slotTransform == null)
                return;

            slotTransform.gameObject.SetActive(true);
            TalentTreeNodeView nodeView = slotTransform.GetComponent<TalentTreeNodeView>();
            if (nodeView == null)
                nodeView = slotTransform.gameObject.AddComponent<TalentTreeNodeView>();

            nodeView.Bind(this, nodeId, title, description, effect);
            nodeView.SetIconSprite(GetIconSprite(nodeId));
            _activeNodeViews.Add(nodeView);
        }

        private void ConfigureUnusedSlots()
        {
            for (int i = 0; i < HiddenNodeNames.Length; i++)
            {
                Transform hiddenTransform = FindCachedChild(HiddenNodeNames[i]);
                if (hiddenTransform != null)
                    hiddenTransform.gameObject.SetActive(!_hideUnusedNodeSlots);
            }
        }

        private void ConfigureConnectorNode(string nodeName)
        {
            Transform connectorTransform = FindCachedChild(nodeName);
            if (connectorTransform == null)
                return;

            connectorTransform.gameObject.SetActive(true);
            TalentTreeNodeView nodeView = connectorTransform.GetComponent<TalentTreeNodeView>();
            if (nodeView == null)
                nodeView = connectorTransform.gameObject.AddComponent<TalentTreeNodeView>();

            nodeView.BindAsConnector();
            nodeView.SetIconSprite(null);

            global::TalentTreeNodeHoverTarget hoverTarget =
                connectorTransform.GetComponent<global::TalentTreeNodeHoverTarget>();
            if (hoverTarget != null)
                hoverTarget.enabled = false;
        }

        private Sprite GetIconSprite(AgentTalentNodeId nodeId)
        {
            switch (nodeId)
            {
                case AgentTalentNodeId.EarthAttack1:
                case AgentTalentNodeId.EarthAttack2:
                case AgentTalentNodeId.IceAttack1:
                case AgentTalentNodeId.IceAttack2:
                    return _attackSprite;
                case AgentTalentNodeId.EarthDefense1:
                case AgentTalentNodeId.IceDefense1:
                    return _defenseSprite;
                case AgentTalentNodeId.EarthHealth1:
                case AgentTalentNodeId.IceMaxHealth1:
                    return _healthSprite;
                case AgentTalentNodeId.EarthExperienceGain1:
                case AgentTalentNodeId.IceExperienceGain1:
                    return _experienceSprite;
                case AgentTalentNodeId.EarthStoneWallDuration:
                    return _earthStoneSprite;
                case AgentTalentNodeId.EarthQuakeDamagePerSecond:
                    return _earthQuakeSprite;
                case AgentTalentNodeId.IceFrostAssaultSlowDuration:
                    return _iceFrostStrikeSprite;
                case AgentTalentNodeId.IceWinterfallDamage:
                    return _iceWinterSprite;
                case AgentTalentNodeId.EarthReactiveShield:
                case AgentTalentNodeId.IceKillAttackStack:
                    return _specialNodeSprite;
                default:
                    return null;
            }
        }

        private void ResolveTargetTalentRuntime()
        {
            if (_targetTalentRuntime != null || !_autoFindTalentRuntime)
                return;

            _targetTalentRuntime = FindObjectOfType<AgentTalentRuntimeController>();
            if (_targetTalentRuntime != null)
                _targetTalentRuntime.EnsureInitialUnlocksApplied();
        }

        private void CacheChildLookup()
        {
            _childByName.Clear();
            Transform[] children = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child != null && !_childByName.ContainsKey(child.name))
                    _childByName.Add(child.name, child);
            }
        }

        private void CacheTexts()
        {
            if (_treeTitleText == null)
                _treeTitleText = FindText("TreeTitle");

            if (_gainTitleText == null)
                _gainTitleText = FindText("GainTitle");

            if (_requirementText == null)
                _requirementText = FindText("RequirementHint");

            if (_descriptionTexts == null || _descriptionTexts.Length == 0)
            {
                _descriptionTexts = new[]
                {
                    FindText("Description_01"),
                    FindText("Description_02"),
                    FindText("Description_03")
                };
            }
        }

        private Text FindText(string objectName)
        {
            Transform target = FindCachedChild(objectName);
            return target != null ? target.GetComponent<Text>() : null;
        }

        private Transform FindCachedChild(string objectName)
        {
            Transform target;
            return _childByName.TryGetValue(objectName, out target) ? target : null;
        }

        private void SetTreeTitle(string title)
        {
            if (_treeTitleText != null)
                _treeTitleText.text = title;
        }

        private void ShowStatus(string status)
        {
            if (_requirementText != null)
                _requirementText.text = status;
        }

        private void SetDescriptionText(int index, string value)
        {
            if (_descriptionTexts == null || index < 0 || index >= _descriptionTexts.Length)
                return;

            if (_descriptionTexts[index] != null)
                _descriptionTexts[index].text = value;
        }

        private string BuildPrerequisiteText(AgentTalentNodeId nodeId)
        {
            if (_targetTalentRuntime == null)
                return "未绑定 Agent 天赋运行时";

            IReadOnlyList<AgentTalentNodeId> prerequisites = _targetTalentRuntime.GetPrerequisites(nodeId);
            if (prerequisites == null || prerequisites.Count <= 0)
                return "点击解锁";

            for (int i = 0; i < prerequisites.Count; i++)
            {
                if (!_targetTalentRuntime.IsUnlocked(prerequisites[i]))
                    return "需要前置节点：" + GetDisplayName(prerequisites[i]);
            }

            return "点击解锁";
        }

        private string GetDisplayName(AgentTalentNodeId nodeId)
        {
            switch (nodeId)
            {
                case AgentTalentNodeId.EarthAttack1:
                case AgentTalentNodeId.IceAttack1:
                    return "攻击力 +1";
                case AgentTalentNodeId.EarthHealth1:
                    return "生命值 +1";
                case AgentTalentNodeId.IceMaxHealth1:
                    return "最大生命值 +1";
                case AgentTalentNodeId.EarthStoneWallDuration:
                    return "磐石持续 +3s";
                case AgentTalentNodeId.IceFrostAssaultSlowDuration:
                    return "霜袭减速 +3s";
                case AgentTalentNodeId.EarthDefense1:
                case AgentTalentNodeId.IceDefense1:
                    return "防御力 +1";
                case AgentTalentNodeId.EarthExperienceGain1:
                case AgentTalentNodeId.IceExperienceGain1:
                    return "经验获取 +5%";
                case AgentTalentNodeId.EarthQuakeDamagePerSecond:
                    return "撼动伤害 +20%";
                case AgentTalentNodeId.IceWinterfallDamage:
                    return "凛冬伤害 +50%";
                case AgentTalentNodeId.EarthAttack2:
                case AgentTalentNodeId.IceAttack2:
                    return "攻击力 +1";
                case AgentTalentNodeId.EarthReactiveShield:
                    return "受击护盾";
                case AgentTalentNodeId.IceKillAttackStack:
                    return "击杀叠攻";
                default:
                    return nodeId.ToString();
            }
        }

        private void CreateTreeSwitchButtonsIfNeeded()
        {
            if (FindCachedChild("TalentTreeSwitch_Earth") != null ||
                FindCachedChild("TalentTreeSwitch_Ice") != null)
            {
                return;
            }

            Transform parent = FindCachedChild("TalentTreeArea") ?? transform;
            CreateSwitchButton(parent, "TalentTreeSwitch_Earth", "土", new Vector2(-410f, 300f), ShowEarthTree);
            CreateSwitchButton(parent, "TalentTreeSwitch_Ice", "冰", new Vector2(-350f, 300f), ShowIceTree);
        }

        private void CreateSwitchButton(
            Transform parent,
            string objectName,
            string label,
            Vector2 anchoredPosition,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            RectTransform rectTransform = buttonObject.transform as RectTransform;
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = new Vector2(48f, 36f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.9f, 0.94f, 1f, 0.92f);

            Button button = buttonObject.GetComponent<Button>();
            button.onClick.AddListener(onClick);

            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(buttonObject.transform, false);

            RectTransform labelRect = labelObject.transform as RectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            Text text = labelObject.GetComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 20;
            text.color = new Color(0.07f, 0.08f, 0.1f, 1f);
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}
