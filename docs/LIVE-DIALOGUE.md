# 联网模式与公开试玩范围

[技术文档目录](README.md)

项目开发版本支持自由输入，经 Python 服务调用语言模型，并结合阶段规则检查推进条件。实现思路见[AI 叙事系统](NARRATIVE.md)与[对话接口](DIALOGUE.md)。

联网源码补充包已停止公开下载，当前仓库不提供该服务的安装包或完整实现。公开 Windows 试玩使用固定选项与预录回复，保留场景探索、录音、六阶段变化和结尾，无需 Python、网络或 API Key。

已经持有旧联网工具、希望切回离线的玩家，可以先退出游戏并停止服务，再将 exe 旁的 `room-service.json` 设置为：

```json
{"offline": true}
```

保存后重新启动游戏。

The live-dialogue source add-on is no longer publicly distributed. The public Windows demo uses offline prerecorded dialogue and requires no API key. Existing users can stop the local service, restore the offline configuration above, and restart the game.
