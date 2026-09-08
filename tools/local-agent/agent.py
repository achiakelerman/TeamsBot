import json, os, sys, urllib.request

MODEL = os.getenv("OLLAMA_MODEL", "gemma4:26b")
BASE = os.getenv("OLLAMA_URL", "http://localhost:11434")
ROOT = os.path.abspath(os.getenv("AGENT_ROOT", os.getcwd()))

TOOLS = [
    {"type":"function", "function":{"name":"list_files", "description":"List files in a folder", "parameters":{"type":"object","properties":{"path":{"type":"string"}},"required":[]}}},
    {"type":"function", "function":{"name":"read_file", "description":"Read a UTF-8 text file", "parameters":{"type":"object","properties":{"path":{"type":"string"}},"required":["path"]}}}
]

def safe_path(path):
    p = os.path.abspath(os.path.join(ROOT, path)) if not os.path.isabs(path) else os.path.abspath(path)
    if os.path.commonpath([ROOT, p]) != ROOT: raise ValueError("Path is outside the agent workspace")
    return p

def run_tool(name, args):
    if name == "list_files":
        p = safe_path(args.get("path", ".")); return "\n".join(sorted(os.listdir(p)))
    if name == "read_file":
        with open(safe_path(args["path"]), encoding="utf-8") as f: return f.read()[:30000]
    raise ValueError("Unknown tool")

messages = [{"role":"system","content":"אתה סוכן אישי מקומי. השתמש בכלים. אל תנחש."}]
print(f"Local agent: {MODEL}. Workspace: {ROOT}. Type 'exit' to quit.")
while True:
    try: prompt = input("\nאתה> ").strip()
    except EOFError: break
    if prompt.lower() in ("exit", "quit"): break
    messages.append({"role":"user","content":prompt})
    while True:
        body = json.dumps({"model":MODEL,"messages":messages,"tools":TOOLS,"stream":False}, ensure_ascii=False).encode()
        req = urllib.request.Request(BASE + "/api/chat", body, {"Content-Type":"application/json"})
        with urllib.request.urlopen(req, timeout=300) as r: data=json.load(r)
        msg=data.get("message",{}); messages.append(msg)
        calls=msg.get("tool_calls",[])
        if not calls:
            print("\nAgent> " + msg.get("content", "")); break
        for call in calls:
            fn=call.get("function",{}); name=fn.get("name"); args=fn.get("arguments",{})
            try: result=run_tool(name,args)
            except Exception as e: result="ERROR: "+str(e)
            messages.append({"role":"tool","content":result})
