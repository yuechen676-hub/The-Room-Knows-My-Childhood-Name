# The Room Knows My Childhood Name

A first-person narrative game set in a childhood bedroom. Exploration, recorded memories and conversations with the room unfold across six emotional stages and eleven clue objects.

## Downloads / 下载

- **Complete Unity project / 完整 Unity 工程:** [Download the source project — 2026-09-12](https://github.com/yuechen676-hub/The-Room-Knows-My-Childhood-Name/releases/download/v2026.09.09/TheRoomKnowsMyChildhoodName-Windows.zip)
- **Release page / 发布页面:** [Open the original release page](https://github.com/yuechen676-hub/The-Room-Knows-My-Childhood-Name/releases/tag/v2026.09.09)

**This download contains the complete Unity source project and requires Unity to open.** It replaces the previously published Windows executable package. The release page and download URL are unchanged: the archive deliberately retains the historical filename `TheRoomKnowsMyChildhoodName-Windows.zip` and release tag `v2026.09.09`, while its contents are the **September 12, 2026 source snapshot**. No standalone executable is included.

中文说明：这是一个通过探索童年卧室、聆听录音、观察物件和与房间对话展开的第一人称叙事游戏。原 Windows 游戏附件现已替换为完整工程；为保持原下载直链可用，ZIP 沿用旧文件名，里面是工程文件夹，不含可直接运行的游戏程序。解压后在 Unity Hub 中添加包含 `Assets`、`Packages` 和 `ProjectSettings` 的文件夹。工程使用 **Unity 6000.3.7f1**，本地对话服务需要自行配置 API 密钥，具体步骤见下文。工程包保留全部当前项目资源及其 `.meta` 文件、C# 脚本、Python 后端、工程配置和文档；缓存、旧构建、日志、备份及本机凭据不包含在内。

Paths and document names below refer to files inside the downloaded project ZIP.

## Open the project

1. Download and extract the complete project using the named ZIP link above. The repository homepage documents this release; the full project is distributed as that release attachment. GitHub's automatic "Source code" archives follow the historical release tag and do not contain this project snapshot.
2. Install Unity **6000.3.7f1** through Unity Hub and add the extracted project directory. Allow Unity to restore packages and import assets; the first import takes longer because generated caches are not distributed.
3. Open `Assets/Scenes/TheRoomKnowsMyChildhoodName.unity`, the enabled build scene. Its GUID is preserved and the startup and validation tools use this path.
4. Start the local dialogue service below, then enter Play Mode.

Dialogue requires the Python service and access to its language-model provider. Unity Editor uses `room-service.editor.json` in the project root when present, otherwise `room-service.json`. Both are local configuration files. Service setup and entry points are described in `Deployment/README.md`.

### Local dialogue setup

Use Python 3.11 or later. From the project root, run these commands in Windows PowerShell:

```powershell
python -m venv Deployment/.venv
Deployment/.venv/Scripts/python.exe -m pip install -r Deployment/server/requirements.txt
$env:ANTHROPIC_API_KEY = Read-Host 'Anthropic API key'
$roomLocalToken = [guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N')
$roomLocalConfig = @{ serviceUrl = 'http://127.0.0.1:5002'; accessToken = $roomLocalToken }
[IO.File]::WriteAllText((Join-Path (Get-Location) 'room-service.editor.json'), ($roomLocalConfig | ConvertTo-Json))
Deployment/.venv/Scripts/python.exe Deployment/server/editor_service.py
```

Keep that terminal open while playing in the Editor. The backend uses the Anthropic SDK; the requested model is configured by `MODEL` in `Deployment/server/story.py` and must be available to your provider account. Live dialogue uses that account's API service. On macOS/Linux, use the virtual environment's `bin/python` executable and set `ANTHROPIC_API_KEY` in that shell; create the same JSON configuration shown in the `Deployment/README.md`.

The service listens on `127.0.0.1:5002`. The Unity client and backend must use the same local access token. Published source contains no working service credentials: configure your own locally and keep them out of commits. Some asset-generation tools reference external original production files; those originals are not needed to open the retained Unity scene.

## Controls

| Input | Action |
|---|---|
| WASD / mouse | Move / look |
| Left Ctrl or C | Crouch |
| Space | Jump while standing |
| Left click | Interact with the highlighted object or displayed control |
| Hold right mouse button during close inspection | Rotate the object |
| Enter | Open text input or send a message |
| Escape | Close inspection, leave text input or release the cursor, depending on context |

Object-specific actions appear in the inspection panel. Stage transitions wait for the dialogue panel to close.

## Project structure

| Location | Contents |
|---|---|
| `Assets/Scripts/` | Player movement, interaction, inspection, dialogue UI and stage presentation |
| `Assets/Editor/` | Scene setup, build tools and Unity validation utilities |
| `Assets/Resources/` | Runtime clue data, recordings and interaction assets |
| Other asset folders | Prop models, materials, shaders and associated setup scripts |
| `Packages/`, `ProjectSettings/` | Unity dependencies and project configuration |
| `LocalPackages/` | Local copies of package dependencies required when moving the project |
| `Deployment/server/` | Dialogue service, progression rules and backend tests |
| `Deployment/render-upload/` | Deployment staging copy; the maintained backend is in `server/` |
| `ColliderReview/` | Inspection and collision tools, resource generators and validation records |
| `SubmissionPlanning/` | Written submission, figures and source documentation |
| `Builds/` | Generated standalone packages |

`ColliderReview/` retains its existing name and paths because several editor tools and resource generators depend on them. Dated reports describe the checks performed at that time. Current narrative constraints are maintained in `EMOTIONAL_NARRATIVE_GUIDELINES.md`.

The `PROJECT_REVIEW.md` records directory sizes, scene and metadata checks, portability fixes and remaining validation work. `ColliderReview/README.md` and `SubmissionPlanning/README.md` have separate indexes.

The `ColliderReview/AssetPruning/README.md` documents the scene migration and removal of unused third-party resources. Project-created models and required third-party dependencies are retained.

## Source and release contents

A Unity source handoff includes `Assets/` with all `.meta` files, `Packages/`, `LocalPackages/`, `ProjectSettings/`, the backend source and the relevant documentation and tools. Preserve original resource names used by object lookup and audio loading. The Tripo Bridge dependency resolves to the local package copy rather than a machine-specific Downloads directory.

Unity caches, local editor settings, logs, backups, Python environments and build outputs are excluded from new version-control additions by `.gitignore`. They remain available locally. Ignoring a file does not remove it from an existing repository or from a manually created ZIP.

Standalone releases are assembled from their platform build folders. The Windows build entry point is `BuildTeacherRelease.BuildWindows`; macOS uses `BuildMacRelease.Build`. Release reports in `Deployment/` record build-specific verification and limitations. Later source changes require a new build before they appear in a distributed package.

In the Editor, use **Tools → Room → Build Windows for Teacher** for a Windows build. Output is written to `Builds/Windows/`. Configure a separate `room-service.json` alongside the Windows executable when preparing a playable distribution. The macOS build helper currently copies its service configuration from `Builds/Windows/room-service.json`, so prepare that local file and install macOS build support before using **Tools → Room → Build macOS Universal Release**.

## Validation and current limits

Backend regression tests can be run from the project root after installing the service dependencies:

```powershell
Deployment/.venv/Scripts/python.exe -m unittest discover -s Deployment/server -p "test_*.py"
```

The September 12 cleanup record reports successful Unity compilation, main-scene loading and resolution of all eleven clue bindings. It also records pre-existing missing-script components; see the `ColliderReview/AssetPruning/README.md` for the current count and scope. These checks do not establish a complete six-stage manual playthrough or a fresh import on another computer. Detailed narrative and backend documents contain story spoilers.

## Credits and development

The project combines Blender assets, recorded and synthesised audio, Unity/C# interaction code and a Python service using Claude for live text dialogue. Code development included AI assistance; the child's recorded voice uses ElevenLabs. Third-party scene resources include Will's Room, AllSky and PBR 200 Premium Materials.

Detailed asset provenance, image adaptation and development methods are recorded in `SubmissionPlanning/sources-and-code-framework.md` and the `SubmissionPlanning/Submission_Draft/English_Statement_with_References.md`. Asset licences and source credits remain with the project; this repository does not grant redistribution rights to third-party resources.
