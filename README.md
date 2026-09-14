The Room Knows My Childhood Name

A first-person narrative game set in a childhood bedroom. Through exploration, recorded memories and conversations with the room, the player moves through six emotional stages and discovers eleven clue objects.

Downloads / 下载

* Complete Unity project / 完整 Unity 工程: Download the source project — 2026-09-12
* Release page / 发布页面: Open the release page

This download contains the Unity source project. No standalone executable is included.

The archive contains the September 12, 2026 source snapshot. It retains the filename TheRoomKnowsMyChildhoodName-Windows.zip and release tag v2026.09.09 to preserve the original download link.

中文说明：这是一个通过探索童年卧室、聆听录音、观察物件和与房间对话展开的第一人称叙事游戏。下载内容为完整 Unity 工程，不含可直接运行的游戏程序。ZIP 沿用原文件名，实际内容为 2026 年 9 月 12 日的工程版本。

解压后，在 Unity Hub 中添加包含 Assets、Packages 和 ProjectSettings 的文件夹。工程使用 Unity 6000.3.7f1；对话功能需要启动本地 Python 服务并配置 API 密钥，具体步骤见下文。

Paths below refer to files inside the downloaded project ZIP.

Open the project

1. Download and extract the project using the ZIP link above. GitHub’s automatic “Source code” archives do not contain this project snapshot.
2. Install Unity 6000.3.7f1 through Unity Hub and add the extracted project directory.
3. Allow Unity to restore packages and import assets. The first import may take some time.
4. Open Assets/Scenes/TheRoomKnowsMyChildhoodName.unity.
5. Start the local dialogue service, then enter Play Mode.

Local dialogue setup

The dialogue service requires Python 3.11 or later and an Anthropic API key.

From the project root, run these commands in Windows PowerShell:

python -m venv Deployment/.venv
Deployment/.venv/Scripts/python.exe -m pip install -r Deployment/server/requirements.txt
$env:ANTHROPIC_API_KEY = Read-Host 'Anthropic API key'
$roomLocalToken = [guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N')
$roomLocalConfig = @{ serviceUrl = 'http://127.0.0.1:5002'; accessToken = $roomLocalToken }
[IO.File]::WriteAllText((Join-Path (Get-Location) 'room-service.editor.json'), ($roomLocalConfig | ConvertTo-Json))
Deployment/.venv/Scripts/python.exe Deployment/server/editor_service.py

Keep the terminal open while playing in the Editor.

The service runs at http://127.0.0.1:5002. Unity and the service must use the same local access token. The Editor reads room-service.editor.json from the project root when present, otherwise room-service.json.

The model is configured by MODEL in Deployment/server/story.py and must be available to your API account. API credentials are not included in the project.

On macOS/Linux, use the virtual environment’s bin/python executable and set ANTHROPIC_API_KEY in your shell. See Deployment/README.md for configuration details.

Controls

Input	Action
WASD / mouse	Move / look
Left Ctrl or C	Crouch
Space	Jump while standing
Left click	Interact with the highlighted object or displayed control
Hold right mouse button during close inspection	Rotate the object
Enter	Open text input or send a message
Escape	Close inspection, leave text input or release the cursor, depending on context

Object-specific actions appear in the inspection panel. Stage transitions wait for the dialogue panel to close.

Project structure

Location	Contents
Assets/Scenes/	Main game scene
Assets/Scripts/	Player movement, object interaction, inspection, dialogue UI and stage presentation
Assets/Resources/	Clue data, recordings and interaction assets
Packages/, LocalPackages/, ProjectSettings/	Package dependencies and project configuration
Deployment/server/	Dialogue service and progression rules
SubmissionPlanning/	Project statement, figures and supporting documentation

Preserve the .meta files when moving or copying the project, as Unity uses them to maintain resource references.

Notes

The project has been checked for Unity compilation, main-scene loading and all eleven clue bindings. A complete six-stage playthrough and a fresh import on another computer have not been verified. Some pre-existing missing-script components are recorded in the project validation documents.

Narrative and backend documents contain story spoilers.