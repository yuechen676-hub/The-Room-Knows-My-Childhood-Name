# 构建与发布

[返回首页](../README.md)

## 玩家运行

试玩版目前不公开下载，有兴趣尝试离线试玩版本和 API 联网版本，请联系作者。收到 Windows x64 ZIP 后完整解压，双击 exe。首次启动可以在设置中选择中文 / English 与字号。不要移动单独的 exe，也不要从压缩包内部运行。

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

## 联网实现的分发范围

离线试玩版与 API 联网版都不公开分发，联网服务的完整源码也不公开。如需试玩请联系作者，详见[联网模式说明](LIVE-DIALOGUE.md)。

## 可选实时开发

完整工程后端依赖：

```powershell
python -m venv Deployment/.venv
Deployment/.venv/Scripts/python.exe -m pip install -r Deployment/server/requirements.txt
Deployment/.venv/Scripts/python.exe -m unittest discover -s Deployment/server -p "test_*.py"
```

实时配置为本地 `room-service.editor.json` 或构建旁的 `room-service.json`，包括 `serviceUrl` 与 `accessToken`。本地服务可监听 127.0.0.1:5002；客户端与服务端令牌必须一致。供应商密钥使用环境变量，不随试玩分发。真实 API 的验收状态见[测试报告](VALIDATION.md)。

## 发布流程

当前试玩包使用 2026-10-14 的构建，于 2026-11-05 打包，由作者单独提供，不在本仓库公开发布。

打包流程：准备 ZIP → ZIP CRC 完整性检查 → 生成 SHA-256 → 交付前核对大小与 SHA-256。

收到试玩包后，可用 PowerShell `Get-FileHash -Algorithm SHA256 <ZIP路径>` 与[测试报告](VALIDATION.md)中的校验值对照。本仓库只包含文档，不是可运行试玩或完整 Unity 工程。

