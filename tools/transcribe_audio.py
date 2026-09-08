import json, sys
from faster_whisper import WhisperModel

audio = sys.argv[1]
model = WhisperModel("small", device="cpu", compute_type="int8")
segments, info = model.transcribe(audio, beam_size=5, vad_filter=True)
print(json.dumps({"language": info.language, "segments": [{"start": s.start, "end": s.end, "text": s.text.strip()} for s in segments if s.text.strip()]}, ensure_ascii=False))
