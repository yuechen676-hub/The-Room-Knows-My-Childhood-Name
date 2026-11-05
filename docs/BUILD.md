# 构建与发布

[返回首页](../README.md)

## 玩家运行

下载首页明确标注的 OfflineDemo Windows x64 ZIP，完整解压，双击 exe。首次启动可以在设置中选择中文 / English 与字号。不要移动单独的 exe，也不要从压缩包内部运行。

当前发行只提供 Windows 64 位。macOS / Linux 尚无构建与测试记录，最低硬件配置也尚未完成系统性测试。

## 从完整工程构建

这些步骤要求另有完整 Unity 工程；克隆此文档仓库不足以构建游戏。

1. 安装 Unity 6000.3.7f1 及 Windows Build Support。
2. 打开包含 Assets、Packages、ProjectSettings 的工程目录。
3. 确认 Build Settings 仅启用 `Assets/Scenes/TheRoomKnowsMyChildhoodName.unity`。
4. 确认 `Assets/Resources/Demo/OfflineDialogue.json` 存在且完整。
5. 使用 `Tools > Room > Build Windows Offline Demo`；代码入口为 `RoomOfflineDemoTools.BuildWindowsDemo`。
6. 输出到 `Builds/WindowsDemo/`，检查 Unity 构建日志是否成功。
7. 打包 exe、Data、MonoBleedingEdge、引擎 DLL 及运行所需目录，并附离线配置与操作说明。

现有构建工具还复制可选 LiveDialogue 工具到输出目录；默认离线发行在打包阶段排除此目录。离线对白位于构建资源中，修改工程 JSON 后需要重新构建并发布。

## 玩家开启实时对话

现有 Windows 程序可配合单独下载的 LiveDialogue 补充包使用。玩家自行提供 Anthropic API Key；输入入口在 `启动实时对话.cmd` 打开的终端，不在游戏内。详见[联网安装与操作指南](LIVE-DIALOGUE.md)，包含依赖、目录位置、费用、模型配置与切回离线的方法。

## 可选实时开发

完整工程后端依赖：

```powershell
python -m venv Deployment/.venv
Deployment/.venv/Scripts/python.exe -m pip install -r Deployment/server/requirements.txt
Deployment/.venv/Scripts/python.exe -m unittest discover -s Deployment/server -p "test_*.py"
```

实时配置为本地 `room-service.editor.json` 或构建旁的 `room-service.json`，包括 `serviceUrl` 与 `accessToken`。本地服务可监听 127.0.0.1:5002；客户端与服务端令牌必须一致。供应商密钥使用环境变量，不随试玩分发。真实 API 的验收状态见[测试报告](VALIDATION.md)。

## 发布流程

当前发行标签：`offline-demo-2026.11.05`。使用 2026-10-14 已有构建，发布日期与编译日期明确分开。

准备 ZIP → ZIP CRC 完整性检查 → SHA-256 → 上传为草稿 Release → 核对远程大小与 SHA-256 → 发布。

附件中的 `.sha256` 可与 PowerShell `Get-FileHash -Algorithm SHA256 <ZIP路径>` 对照。GitHub 自动生成的 Source code 是文档仓库快照，不是可运行试玩或完整 Unity 工程。

