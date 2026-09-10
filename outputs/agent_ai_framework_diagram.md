# Agent AI Framework Diagram

这张图按当前真实代码结构整理，不是理想化设计稿。核心链路是：

`AgentPawnRoot -> AgentBrainController -> HierarchicalStateMachine -> AgentBrainState -> BehaviorTreeRunner -> Agent Action Nodes -> Gameplay Systems`

## Overall Architecture

```mermaid
flowchart LR
    subgraph Scene["Scene / External Inputs"]
        PlayerInput["PlayerInputManager<br/>玩家点击目标群"]
        Discovery["AgentTargetDiscoveryController<br/>保底自动目标发现"]
        Decision["AgentTargetDecisionController<br/>评分目标决策<br/>(当前 Agent prefab 中关闭)"]
        TargetRegistry["GameplayTargetRegistry<br/>目标群注册表"]
        RuntimeRegistry["AgentRuntimeRegistry<br/>多 Agent 注册 / 聚焦"]
    end

    subgraph AgentEntry["Agent Runtime Entry"]
        Pawn["AgentPawnRoot<br/>IAgentReadOnly<br/>IAgentCommandReceiver"]
        Intervention["AgentInterventionController<br/>写入指令 / 清除指令"]
        Blackboard["BehaviorBlackboard<br/>HasVisibleEnemy<br/>HasResourceTarget<br/>ShouldExtract<br/>PendingDirectiveRequest"]
    end

    subgraph Brain["AI Brain"]
        BrainController["AgentBrainController"]
        HSM["HierarchicalStateMachine"]
        StateFactory["AgentBrainStateFactory"]
        TransitionRules["AgentBrainTransitionRules"]
    end

    subgraph States["HSM Leaf States"]
        Explore["Explore"]
        Combat["Combat"]
        EnemySource["InvestigateEnemySource"]
        Search["SearchResource"]
        Loot["InteractLoot"]
        Extract["Extraction"]
    end

    subgraph BT["Behavior Trees inside states"]
        ExploreBT["ExploreTree<br/>MaintainMacroStateActionNode"]
        CombatBT["CombatTree<br/>Sequence:<br/>MoveToTargetActionNode<br/>EngageEnemyActionNode"]
        SourceBT["InvestigateEnemySourceTree<br/>MoveToTargetActionNode"]
        SearchBT["SearchResourceTree<br/>Sequence:<br/>MoveToTargetActionNode<br/>SearchResourceActionNode"]
        LootBT["InteractLootTree<br/>reuse SearchResourceTree"]
        ExtractBT["ExtractionTree<br/>Sequence:<br/>MoveToTargetActionNode<br/>ExtractActionNode"]
    end

    subgraph Gameplay["Gameplay Systems"]
        NavMesh["NavMeshAgent<br/>移动寻路"]
        CombatSystem["AgentCombatController / AgentCombatShooter<br/>EnemyHealthController"]
        LootSystem["LootBoxEntity / WorldLootItem<br/>InventoryScreenController"]
        RaidSystem["ExtractionPointController<br/>RaidFlowController"]
        TargetSystem["ResourceCluster / ActiveEnemyCluster<br/>EnemySourceCluster / ExtractionCluster"]
    end

    PlayerInput --> Pawn
    Discovery --> Pawn
    Decision --> Pawn
    TargetRegistry --> Discovery
    TargetRegistry --> Decision
    RuntimeRegistry --> Pawn

    Pawn --> Intervention
    Intervention --> Blackboard
    Pawn --> BrainController
    BrainController --> Blackboard
    BrainController --> HSM
    StateFactory --> HSM
    TransitionRules --> HSM
    Blackboard --> TransitionRules

    HSM --> Explore
    HSM --> Combat
    HSM --> EnemySource
    HSM --> Search
    HSM --> Loot
    HSM --> Extract

    Explore --> ExploreBT
    Combat --> CombatBT
    EnemySource --> SourceBT
    Search --> SearchBT
    Loot --> LootBT
    Extract --> ExtractBT

    CombatBT --> NavMesh
    CombatBT --> CombatSystem
    SearchBT --> NavMesh
    SearchBT --> LootSystem
    ExtractBT --> NavMesh
    ExtractBT --> RaidSystem
    SourceBT --> TargetSystem
    SearchBT --> TargetSystem
    CombatBT --> TargetSystem
    ExtractBT --> TargetSystem
```

## Class Relationship

```mermaid
classDiagram
    class AgentPawnRoot {
        +AgentId AgentId
        +BehaviorBlackboard Blackboard
        +SubmitDirective()
        +SetVisibleEnemy()
        +SetHasResourceTarget()
        +SetShouldExtract()
        +ApplyDamage()
    }

    class IAgentReadOnly
    class IAgentCommandReceiver
    class AgentBrainController {
        +BehaviorBlackboard Blackboard
        +Start()
        +Tick()
        +SetFact()
    }

    class HierarchicalStateMachine {
        +CurrentLeafState
        +Start()
        +Update()
        +ForceTransition()
    }

    class StateMachineState {
        +Children
        +Transitions
        +AddChild()
        +AddTransition()
    }

    class AgentBrainState {
        +AgentMacroStateId MacroStateId
    }

    class BehaviorTree {
        +Name
        +RootNode
    }

    class BehaviorTreeRunner {
        +Tick()
        +Abort()
    }

    class BehaviorNode {
        +Enter()
        +Execute()
        +Exit()
        +Abort()
    }

    class SequenceNode
    class SelectorNode
    class ActionNode
    class DecoratorNode
    class MoveToTargetActionNode
    class EngageEnemyActionNode
    class SearchResourceActionNode
    class ExtractActionNode

    AgentPawnRoot ..|> IAgentReadOnly
    AgentPawnRoot ..|> IAgentCommandReceiver
    AgentPawnRoot --> AgentBrainController
    AgentBrainController --> HierarchicalStateMachine
    AgentBrainController --> BehaviorBlackboard
    HierarchicalStateMachine --> StateMachineState
    StateMachineState <|-- AgentBrainState
    StateMachineState --> BehaviorTreeRunner
    BehaviorTreeRunner --> BehaviorTree
    BehaviorTree --> BehaviorNode
    BehaviorNode <|-- SequenceNode
    BehaviorNode <|-- SelectorNode
    BehaviorNode <|-- ActionNode
    BehaviorNode <|-- DecoratorNode
    ActionNode <|-- MoveToTargetActionNode
    ActionNode <|-- EngageEnemyActionNode
    ActionNode <|-- SearchResourceActionNode
    ActionNode <|-- ExtractActionNode
```

## HSM + Behavior Tree Mapping

```mermaid
flowchart TB
    Root["AgentRoot"]
    Raid["Raid"]
    Root --> Raid

    Raid --> Explore["Explore<br/>维持默认探索状态"]
    Raid --> Combat["Combat<br/>处理敌人目标"]
    Raid --> Source["InvestigateEnemySource<br/>侦查敌人来源点"]
    Raid --> Search["SearchResource<br/>搜索资源点"]
    Raid --> Loot["InteractLoot<br/>战利品交互"]
    Raid --> Extract["Extraction<br/>撤离"]

    Combat --> CombatSeq["Sequence"]
    CombatSeq --> CombatMove["MoveToTargetActionNode<br/>移动到敌人射程"]
    CombatSeq --> Attack["EngageEnemyActionNode<br/>技能 / 射击 / 直接伤害兜底"]

    Search --> SearchSeq["Sequence"]
    SearchSeq --> SearchMove["MoveToTargetActionNode<br/>移动到可达资源"]
    SearchSeq --> OpenLoot["SearchResourceActionNode<br/>箱子 / 地面物 / 等背包关闭"]

    Extract --> ExtractSeq["Sequence"]
    ExtractSeq --> ExtractMove["MoveToTargetActionNode<br/>移动到撤离点"]
    ExtractSeq --> ExtractAction["ExtractActionNode<br/>持续通知 RaidFlowController 读条"]

    Source --> SourceMove["MoveToTargetActionNode<br/>靠近敌人来源点"]
    Explore --> Maintain["MaintainMacroStateActionNode"]
    Loot --> SearchSeq
```

