# The Room Knows My Childhood Name
# 房间记得我童年的名字

这是我围绕童年记忆与自我安慰创作的一款第一人称叙事游戏。玩家在一间卧室中观察物件、聆听记忆并回应房间，经历六个阶段的情绪变化。

A first-person narrative game about exploring a childhood bedroom, listening to memories and responding to the room.

## 下载试玩 / Play

**[下载 Windows 离线试玩 ZIP](https://github.com/yuechen676-hub/The-Room-Knows-My-Childhood-Name/releases/download/offline-demo-2026.11.05/TheRoomKnowsMyChildhoodName-OfflineDemo-Windows-x64.zip)** · [发布页与校验文件](https://github.com/yuechen676-hub/The-Room-Knows-My-Childhood-Name/releases/tag/offline-demo-2026.11.05)

完整解压后运行 `TheRoomKnowsMyChildhoodName.exe`。需要 Windows 64 位；无需 Unity、Python、网络或 API 密钥。请保留解压后的全部文件。

Extract the entire ZIP and launch `TheRoomKnowsMyChildhoodName.exe`. Windows 64-bit; no Unity, Python, internet connection or API key required.

试玩采用**可选台词与预录模型文字回复**，保留场景探索、线索录音、六阶段流程及结尾；不支持自由输入，不代表实时模型的全部回应。中文 / English、阅读字号可在设置中切换。物件录音保留中文原声。

This demo uses selectable lines and prerecorded model text. It includes the six-stage progression and ending. It does not generate live responses or accept free-form dialogue.

## 可选联网对话 / Optional live dialogue

也可以使用自己的 **Anthropic API Key** 体验自由输入与实时模型回复。下载可选 LiveDialogue 工具，放到游戏旁边；双击 `启动实时对话.cmd`，在终端中输入 Key，再启动游戏。需要 Python 3.11+ 和网络，API 用量由自己的账户承担。

**[联网配置、Key 输入位置与常见问题](docs/LIVE-DIALOGUE.md)**

Optional live dialogue supports free-text input using your own Anthropic API key. See the setup guide above. The default demo remains offline.

## 操作 / Controls

| 输入 / Input | 功能 / Action |
|---|---|
| WASD / 鼠标 | 移动 / 观察 · Move / look |
| 左键 / Left click | 交互 / 选择 · Interact / select |
| 查看时按住右键 / Hold right mouse in inspection | 旋转物件 · Rotate object |
| Ctrl / C | 蹲下 · Crouch |
| Space | 站立时跳跃 · Jump while standing |
| Enter | 打开房间对话 · Open room dialogue |
| Esc | 关闭当前界面或暂停 · Close / pause |

先探索并聆听当前阶段的录音，再与房间交谈。读完回复后关闭对话，阶段变化才会继续。找不到方向时可选择房间对话中的引导选项。

## 技术文档 / Documentation

**[游戏开发文档：玩法、系统实现与技术迭代](docs/README.md)**

- [六阶段程序化天空](docs/SKY.md) · [四种交互着色器](docs/SHADERS.md)
- [资产预算与程序化外壳](docs/TOOLS.md) · [性能分析与优化](docs/PERFORMANCE.md)
- [交互、声音与界面](docs/INTERACTION.md) · [AI 叙事迭代](docs/NARRATIVE.md)
- [制作与 AIGC 辅助流程](docs/PRODUCTION.md) · [开发迭代与后续计划](docs/REVIEW.md)


- [技术总览与模块架构](docs/TECHNICAL.md)
- [离线数据、推进与实时接口](docs/DIALOGUE.md)（含实现细节及轻度剧透）
- [构建、运行与发布](docs/BUILD.md)
- [测试结果与已知限制](docs/VALIDATION.md)

本仓库用于试玩分发、技术说明与选定代码快照展示。GitHub 自动生成的 **Source code** 附件只包含此文档仓库，不是完整 Unity 工程。

当前试玩基于 **2026-10-14 Windows 构建**，于 2026-11-05 打包发布。文档介绍开发工程中的实现；仓库提供试玩、技术说明和选定代码摘录，完整 Unity 工程及原始制作素材未公开。

## 制作与工具 / Credits

Unity / C# 实现探索、物件查看、声音、界面和阶段表现；Blender 与制作脚本用于资产整合，房间外壳和天空采用程序化实现。Python / Flask / Anthropic SDK 支持可选联网对话。孩子声音使用 ElevenLabs 预先制作，开发包含 AI 编程辅助和少量 AIGC 辅助贴图产出。详见[资产制作与场景整合](docs/PRODUCTION.md)。
