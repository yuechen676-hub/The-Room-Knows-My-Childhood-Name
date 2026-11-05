# 对话系统与推进

[返回首页](../README.md) · 本页包含线索与推进实现细节。

## 离线模式的选择

Windows 客户端读取可执行文件旁的 `room-service.json`。没有该文件，或文件中包含 `{"offline": true}` 时，载入预录对话。此次 ZIP 明确携带离线配置，不包含可选 LiveDialogue 工具，因此玩家无需启动服务器。

编辑器另支持 `Tools > Room > Play Offline Demo in Editor`。有效服务配置则走实时对话。损坏的配置会显示错误并停用客户端，不能把它描述为总会自动退回离线。

## 数据结构

`OfflineDialogue.json` 的主要字段：

| 字段 | 含义 |
|---|---|
| `opening.zh/en` | 双语开场 |
| `lines[]` | 可选台词，包含 id、stage、kind、前置条件与双语 ask |
| `replies[]` | 按 id + variant 索引的预录回复 |
| `reply.zh/en.text` | 对应语言的回复正文 |
| `reveal` | 与该回复一同显示的附加文字 |
| `closes` | 录制时服务端给出的阶段收束判定 |

`Deployment/record_offline_dialogue.py` 从真实服务录制回复；离线运行只回放现有数据，不调用模型。客户端等待约 1.4 秒呈现回应，这一等待不是网络延迟。

## 选项何时出现

- `care / doubt`：对应录音已经听过，并满足指定实物发现。
- `follow`：前置台词已经说过。
- `found`：玩家实际发现对应细节。
- `object`：当前所询问的普通物件匹配指定名称。
- `insight`：满足台词的具体 requires；依据探索是否完成选择 early 或 final 回复。
- `settle`：之前已经表达理解，且随后完成探索。
- `hint`：仍有未听过的当前阶段录音，按未听组合查找对应回复。

不是所有阶段都有 early 分支：第四阶段理解选项要求两段录音，第六阶段仅一件线索，因此直接使用 final。普通物件只显示实际存在的回复变体；如床的后期 tier5 未录制，运行时不会显示该选项。

第二阶段探索完成还要求杯底裂纹与纸背划字两项发现。某些阶段允许先表达理解，补齐探索后再收束。已用过的 id + variant 不重复出现。物件入口只显示该物件相关选项，Enter 打开的普通对话提供阶段层面的交流和引导。

## 实时接口

以下供完整工程开发者参考；本次离线 ZIP 不需要这些接口。

| 请求 | 主要内容 |
|---|---|
| `GET /health` | 服务健康检查 |
| `POST /start` | `name`、`language`，建立会话 |
| `POST /say` | `session_id`、`language`、`text`、`recovered_memories`、`discoveries` |

客户端回应结构包含 `reply`、`stage`、`reply_stage`、`stage_completed`、`transition_effect`、`door_opened`、`ended`、`reveal_reply` 等。具体公开接口由 `serve.py` 包装；不要把 `story.py` 原型接口当作部署入口。

在线服务需要用户自行配置模型供应商凭据。模型名称由代码中的 `MODEL` 决定，是否可用取决于账户，未在本次发行中验证。API 密钥保留在服务端环境中，客户端仅持有服务访问令牌。生产配置不应提交进仓库。

## 验证边界

JSON 结构、双语字段及收束标志检查不等于每一句内容均通过叙事审校，也不等于六阶段实际交互通关。详见[本次验证](VALIDATION.md)。

