# Teams Web worker (local POC)

This worker opens Teams Web in a visible browser, keeps a local profile for a manual sign-in, joins with a disclosed display name, and writes observed captions to `output/playwright/*.jsonl`. It never automates passwords and is intended for a consenting test meeting.

```powershell
npm install
npm run join -- "https://teams.microsoft.com/l/meetup-join/..." "Meeting Companion"
```

The script uses an installed Chrome by default. Set `TEAMS_BROWSER_PATH` to an Edge or Chrome executable when needed.

The meeting host may need to admit the participant. UI selectors can change as Teams evolves; this worker is a local POC, not the official Graph calling bot.
