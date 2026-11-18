# 联网模式与试玩版本

[技术文档目录](README.md)

项目开发版本支持自由输入，经 Python 服务调用语言模型，并结合阶段规则检查推进条件。实现思路见[AI 叙事系统](NARRATIVE.md)与[对话接口](DIALOGUE.md)。

游戏有两个版本：

- **离线试玩版：** Windows 64 位，使用固定选项与预录回复，保留场景探索、录音、六阶段变化和结尾，无需 Python、网络或 API Key。
- **API 联网版：** 支持自由输入，由语言模型实时回应。

两个版本目前都不公开下载，仓库也不提供联网服务的安装包或完整实现。**有兴趣尝试离线试玩版本和 API 联网版本，请联系作者**，可通过 [GitHub 主页](https://github.com/yuechen676-hub)或[仓库 Issue](https://github.com/yuechen676-hub/The-Room-Knows-My-Childhood-Name/issues)留言。

已经持有联网版本、希望切回离线的玩家，可以先退出游戏并停止服务，再将 exe 旁的 `room-service.json` 设置为：

```json
{"offline": true}
```

保存后重新启动游戏。

The game has an offline demo (prerecorded dialogue, no API key) and a live API version (free-form dialogue). Neither is publicly downloadable at the moment. If you are interested in trying either version, please contact the author via GitHub or an Issue. Existing users of the live version can stop the local service, restore the offline configuration above, and restart the game.
