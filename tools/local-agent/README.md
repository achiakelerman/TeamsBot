# Local Ollama Agent

Create the customized model once:

```powershell
ollama create teams-agent -f tools/local-agent/Modelfile
```

Run the agent:

```powershell
$env:OLLAMA_MODEL="teams-agent"
$env:AGENT_ROOT="C:\Dev\TeamsBot"
python tools/local-agent/agent.py
```

The first version includes safe `list_files` and `read_file` tools. Add new tools in `agent.py`; keep write operations behind an explicit approval step.
