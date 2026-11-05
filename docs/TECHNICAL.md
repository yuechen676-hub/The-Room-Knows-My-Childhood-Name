# 技术总览

[返回首页](../README.md)

## 技术栈

| 层 | 当前实现 |
|---|---|
| 引擎 | Unity 6000.3.7f1 |
| 渲染 | Universal Render Pipeline 17.3.0 |
| 输入 / UI | Input System 1.18.0 / uGUI 2.0.0 |
| 客户端 | C#，Windows x64 standalone |
| 离线对话 | Resources 中的 JSON，固定选项与预录文字回复 |
| 可选实时服务 | Python 3.11+，Flask 3.1.3，Waitress 3.0.2，Anthropic SDK 0.118.0 |

依赖版本取自本地 `Packages/manifest.json` 与 `Deployment/server/requirements.txt`，不是对第三方最新版本的声明。

## 运行数据流

玩家观察与交互 → 已听录音 / 已见细节 → 对话选项过滤 → 预录回复 → 等待玩家关闭对话 → 阶段表现变化。

实时模式将“选项过滤与预录回复”替换为“文本输入 → Python 服务 → 结构化回应”；观察、录音、UI 和阶段表现共用客户端系统。

## 模块职责

以下是完整本地工程中的路径，不是此文档仓库内的源码链接。

| 路径 / 模块 | 职责 |
|---|---|
| `Assets/Scripts/FirstPersonController.cs` | 移动、视角及姿态 |
| `ClueInteractionController.cs` / `RoomClueCatalog.cs` | 线索绑定、录音记录与阶段探索状态 |
| `RoomObjectInteraction.cs` / `RoomInspectionPanel.cs` | 物件交互与近距离查看 |
| `StageTwoDiscovery.cs` | 第二阶段实物细节发现 |
| `RoomObjectRecords.cs` | 已观察物件及记录界面 |
| `RoomConversationUI.cs` | 配置读取、服务会话、响应与 UI 协调 |
| `RoomOfflineDemo.cs` | 同一 partial 类中的离线选项、回复回放与阶段收束 |
| `RoomLocalization.cs` / `RoomReadingPreferences.cs` | 中英文与阅读字号 |
| `RoomTitleMenu.cs` / `RoomGameFlow.cs` | 标题、开始与游戏流程 |
| `RoomStageOneTransition.cs` / `RoomPlayerExit.cs` | 阶段表现及最终离开动作 |
| `ClueSoundLure.cs` | 尚未听过的当前阶段物件的声音引导 |
| `Deployment/server/story.py` | 实时会话与叙事协调 |
| `stage_one.py` / `stage_two.py` / `later_stages.py` | 阶段理解与推进规则 |

表中省略目录的 C# 文件均位于 `Assets/Scripts/`；Python 文件位于 `Deployment/server/`。

## 资源与状态边界

主场景是 `Assets/Scenes/TheRoomKnowsMyChildhoodName.unity`，Build Settings 当前仅启用这一场景。11 件线索按六阶段组织；同阶段允许自由探索顺序。听过录音、看到细节、表达理解是不同状态，不能把播放录音直接视为完成理解。

录音声音与台词、物件事实、对话解释分别处理。界面语言切换不应重置探索状态，已发生的对话保留当时原文。预录对话由 `Assets/Resources/Demo/OfflineDialogue.json` 随构建打包；原始制作与录制脚本不参与玩家运行。

## 设计约束

叙事遵循逐步变化的六阶段情绪，早期不能提前完整揭示声音身份。阶段转换等待玩家读完并关闭对话；最终叙事完成只解锁出口，玩家仍需主动使用钥匙并跨过门槛。底层 `door_opened` 字段不能直接解释为立即移除门。

声音引导只提示当前物件，不播放未发现的台词。普通陈设查看不新增通关条件。完整创作约束由本地 `EMOTIONAL_NARRATIVE_GUIDELINES.md` 维护。

