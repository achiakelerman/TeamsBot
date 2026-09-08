# Teams Meeting Companion

Local-first Teams meeting companion in C#/.NET 9.

## Current status

- Demo transcript and deterministic fallback are available.
- Ollama integration is enabled by default (`qwen2.5:7b`).
- Graph transcript adapter is available when Entra credentials and permissions are configured.
- Teams browser presence and captions worker are the next implementation slice.

## Run locally

```powershell
dotnet run --project src/TeamsBot.Web
```

The local Ollama service must be running with the configured model:

```powershell
ollama list
```

Set `DemoMode` to `false` only after configuring Graph credentials through User Secrets. Never commit secrets or meeting content.

## API

- `GET /health`
- `POST /api/meetings/{meetingId}/join`
- `POST /api/meetings/{meetingId}/process`
- `POST /api/meetings/{meetingId}/actions/{actionId}/approve`

See [docs/ADR-001.md](docs/ADR-001.md) for the architecture decision.
