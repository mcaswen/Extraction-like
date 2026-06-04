# BoardGame 原型说明

## 项目概述

这个目录是一套基于固定点线图的 Unity 原型

当前原型主要验证这些内容

- 四个 AI 共享同一张地图并持续自走
- 玩家不直接控制移动和战斗
- 玩家通过切换焦点和实时改目标来打断 AI
- 搜索 战斗 撤离都是持续过程
- 地图由 ScriptableObject 驱动

这是一版用于玩法验证的原型，不是最终产品内容

## 当前玩法模型

- 默认有四个 AI 共享同一局运行时状态
- 玩家一次只操作当前焦点 AI
- `Tab` 切下一个焦点，`Shift + Tab` 切上一个焦点
- AI 默认只会在相邻节点里自主选目标
- 当前默认 AI 刻意做得比较笨和保守
- 玩家可以直接点击节点改写当前焦点 AI 的目标
- Boss 战不能被打断
- 多个 AI 可以同时处于同一个节点
- 同节点战斗共享一条敌人血条，敌人会同时攻击节点内所有存活 AI
- 节点 loot 共享一份，只有当前焦点 AI 可以打开背包继续搜索
- 打开 loot 背包或升级面板时，游戏会暂停
- 所有存活 AI 都撤离后，这一局才算成功
- 资源搜索进度 敌人剩余血量 撤离进度都会保留在运行时状态里

## 目录结构

`BoardGame/Config`

- ScriptableObject 定义
- 策划预设代码
- 地图 规则 掉落配置

`BoardGame/Runtime`

- 运行时枚举
- 运行时状态
- 服务层
- 控制器
- 场景节点标记组件

`BoardGame/Presentation`

- HUD
- 道具栏
- 鼠标输入

`BoardGame/Views`

- 节点 边 AI 表现组件
- 道具槽位组件
- 高亮外圈组件

`BoardGame/Editor`

- 场景节点导入工具

## 主要资产

当前原型依赖这些 ScriptableObject

- `SO_BoardGame_MapDefinition`
- `SO_BoardGame_RuleSet`
- `SO_BoardGame_LootTableSet`
- `SO_BoardGame_AgentRoster`

场景里的运行时入口是

- `BoardGamePrototypeInstaller`

## 场景搭建概览

这套原型目前仍然需要手动接线

场景中至少需要这些对象

- 一个安装器对象
- 一个地图根节点
- 一个地图视图控制器
- 一个输入控制器
- 一个 HUD 控制器
- 一个道具栏控制器
- 一个主相机
- 一个 EventSystem

至少需要这些预制体

- 节点预制体
- 边预制体
- AI 预制体
- 道具栏槽位对象

节点 边 AI 的世界物体会根据地图定义在运行时生成

## 场景节点导入工具

当前有一个编辑器工具可以把场景里摆好的节点位置写回地图 ScriptableObject

基本流程是

- 在场景里摆节点标记物体
- 给每个物体填写 `NodeId`
- 若需要地图默认起点，可标记 `IsStartNode`
- 若要回写四个 AI 的出生点，再额外挂 `BoardGameSceneAgentSpawnMarker`
- 给出生位标记填写 `AgentId`
- 打开 `Tools/BoardGame/Scene Node Import Tool`
- 把位置写回地图资产
- 若已指定 roster 资产，会同时把 `AgentId -> StartNodeId` 写回 roster

这个工具目前只负责

- 节点位置导入
- 起点导入
- Agent 出生位导入到 roster

它暂时还不负责

- 边数据导入
- 完整节点类型和等级导入

## 当前范围

这一版已经包含

- 四 AI 共享地图的自主移动
- 焦点切换和定向改目标
- 持续搜索 战斗 撤离
- 同节点共享战斗与共享 loot
- 掉落 背包 消耗品使用
- 地图 HUD 和道具栏展示
- 单局运行时节点状态保留

这一版还没有包含

- 正式存档读档流程
- 完整的表驱动地图导入链路
- 自动地图排布
- 最终美术 动画 特效和打磨

## 备注

- 核心逻辑本身不依赖具体场景，但仍然依赖安装器里的显式引用绑定
- 部分 UI 会在运行时自动补建，例如升级面板和 loot overlay
- 如果世界文本使用 World Space UI，要注意不要挡住鼠标点击
