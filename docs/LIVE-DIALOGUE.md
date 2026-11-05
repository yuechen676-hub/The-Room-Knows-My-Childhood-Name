> 历史记录：此页所述源码包已停止公开分发，安装步骤不再作为当前下载指南。当前试玩范围请查看默认分支。

# 可选联网模式 · 使用自己的 API Key

[技术文档目录](README.md)

同一个 Windows 程序支持离线预录对话和联网实时对话。默认试玩无需配置；联网属于可选功能。

| | 离线试玩 | 实时 AI 对话 |
|---|---|---|
| 输入与回复 | 固定选项、预录文字 | 自由输入、API 实时生成文字 |
| 网络 / API Key | 不需要 | 自己的 Anthropic API Key |
| Python | 不需要 | Python 3.11+ |
| API 费用 | 无运行时 API 调用 | 计入玩家自己的 API 账户 |

## 1. 下载与放置

先完整解压 [Windows 试玩包](https://github.com/yuechen676-hub/The-Room-Knows-My-Childhood-Name/releases/download/offline-demo-2026.11.05/TheRoomKnowsMyChildhoodName-OfflineDemo-Windows-x64.zip)，再下载 （源码下载已撤下）。

将整个 `LiveDialogue` 文件夹放到 exe 旁边：

```text
TheRoomKnowsMyChildhoodName-OfflineDemo/
  TheRoomKnowsMyChildhoodName.exe
  TheRoomKnowsMyChildhoodName_Data/
  room-service.json
  LiveDialogue/
    启动实时对话.cmd
    start-live-dialogue.ps1
    server/
      story.py
      local_service.py
      requirements.txt
      ...
```

保留游戏原有 DLL 和其他目录。补充包不是独立游戏，无需重新构建 Unity 程序。

## 2. 准备 Python 与自己的 API 账户

安装 [Python 3.11 或更新版本](https://www.python.org/downloads/)，确认终端中 `py -3` 或 `python` 可用。联网工具首次运行会建立独立环境并安装依赖，需要网络。

在 [Claude Console 的 API Keys 页面](https://platform.claude.com/settings/keys)创建密钥，步骤参见[官方说明](https://platform.claude.com/docs/en/get-api-key)。当前接入为 Anthropic API，其他厂商的 Key 不能直接替换。

请确认账户有可用额度和所需模型权限。API 与 Claude 聊天订阅分别计费，赠送额度以账户实际显示为准。本项目不提供共享 Key，调用费用由玩家承担。[官方计费说明](https://support.claude.com/en/articles/9876003-why-is-claude-api-usage-billed-separately-from-my-paid-claude-plan)

## 3. API Key 输入在哪里

**先退出游戏，再双击 `LiveDialogue/启动实时对话.cmd`。Key 输入在弹出的终端中，不在游戏设置里。**

依赖准备完成后，终端会提示：

```text
Anthropic API 密钥（输入时不显示）/ Anthropic API key (hidden):
工作区 ID，没有就直接回车 / Workspace ID, or press Enter to skip:
```

第一项粘贴 Key 后按回车，输入不会明文回显。第二项为可选工作区 ID，不是再次输入 Key；没有额外工作区要求时直接回车。

工具自动写入 exe 旁的连接配置，其中只有本机服务地址与随机访问令牌。**不要把 API Key 填入 room-service.json 的 accessToken**，二者用途不同。

待本地服务保持运行、没有错误退出，再启动游戏。按 Enter 打开对话后可以自由输入。游玩时保持服务窗口打开。

启动器会先打印“服务已启动”，再启动 Python，因此这句话本身不是成功证明。需要排查时，在另一个 PowerShell 窗口运行：

```powershell
Invoke-RestMethod http://127.0.0.1:5002/health
```

返回 `status: ok` 只证明本地服务可访问；第一次真实对话才会检查供应商连接和实际模型权限。

## 4. 模型配置

现有模型 ID 位于 `LiveDialogue/server/story.py` 的 `MODEL` 常量中。如果账户不能访问该模型，停止服务，用文本编辑器将它改为账户可访问的官方模型 ID，再启动服务和游戏。不要将聊天界面的显示名称直接当成 API 模型 ID。

更换模型可能影响结构化回复兼容性与叙事表现，需要重新验证。本次补充包复用了现有服务代码，完成 ZIP 完整性、Python 语法与启动脚本语法检查，**未使用真实密钥进行付费 API 验收**。

## 5. 切回离线

先退出游戏，再在服务窗口按 Ctrl+C。正常清理会移除连接配置，游戏下次启动使用预录回复。切换模式需要重启，不承诺迁移本次对话与探索进度。

如果强制关闭窗口或系统中断导致配置未清理，停止服务后，用文本编辑器将 exe 旁的 `room-service.json` 改为：

```json
{"offline": true}
```

保存并重新启动游戏即可。

## 数据与密钥

启动器通过本机进程环境把 API Key 传给服务，不写入游戏连接配置，也无需将 Key 发给作者。本地服务监听 `127.0.0.1:5002`，无需端口转发。

联网时，玩家文字、必要的近期对话与已发现线索上下文会发送给 Anthropic。服务也会在 `LiveDialogue/server/logs/` 产生本地会话记录，可能包含对话内容。服务在本机运行不意味着模型请求不出网。不要将密钥或包含私人内容的日志贴到公开 issue。

## 常见问题

| 现象 | 检查 |
|---|---|
| 仍显示固定选项 | 确认 LiveDialogue 与 exe 同级；服务启动后重启游戏；检查 offline 配置 |
| 找不到 Python | 确认 `py -3` 或 `python` 可用 |
| 依赖安装失败 | 先解决终端错误和软件包网络访问，再启动游戏 |
| 5002 端口占用 | 关闭自己之前开启的同一服务，不要重复启动多个窗口 |
| 401 / 额度不足 / 模型不可用 | 检查自己的 API Key、额度和模型权限 |
| health 正常但对话失败 | 本地服务和供应商连接是两步，继续检查终端及模型配置 |
| 服务结束后仍无法离线 | 退出游戏，恢复上面的 offline 配置后重启 |

## English quick start

Place the optional add-on's `LiveDialogue` folder beside the game executable. Install Python 3.11+. Close the game, double-click `启动实时对话.cmd`, and enter your own Anthropic API key at the hidden terminal prompt. The workspace ID is optional. Keep the service running, then launch the game and press Enter for free-text dialogue.

Usage is billed to your own API account. The key prompt is in the terminal, not the game settings. Exit the game and press Ctrl+C to stop the service; restart for offline mode. If needed, restore `room-service.json` to `{"offline": true}`. Live API access and model compatibility have not been verified for your account.

