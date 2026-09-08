# Local system audio capture

Captures the Windows playback device (Teams output) to a local WAV file. It does not capture the microphone. Use only with participant disclosure and consent.

```powershell
dotnet run --project .\tools\TeamsBot.AudioCapture
```

Stop with `Ctrl+C`. The WAV file is written to `output/audio/` and is intentionally ignored by Git.
