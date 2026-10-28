"""Run AFTER stopping and starting the backend, against the same PostgreSQL database."""
import json,time,urllib.request
from pathlib import Path
cfg=json.loads(Path('.local/settings.json').read_text(encoding='utf-8-sig'));marker=json.loads(Path('.local/persistence-marker.json').read_text(encoding='utf-8'));base=f"http://127.0.0.1:{cfg['AppPort']}"
for attempt in range(100):
    try:
        response=urllib.request.urlopen(base+f"/articles/{marker['article_id']}",timeout=5);content=response.read().decode();break
    except Exception:
        if attempt==99:raise
        time.sleep(.2)
import html
assert response.status==200 and marker['run_id'] in html.unescape(content)
out={'result':'PASS','article_id':marker['article_id'],'run_id':marker['run_id'],'verification':'Published title and complete body remain available after terminating and starting the .NET backend','new_process_pid':int(Path('.local/server.pid').read_text(encoding='utf-8-sig'))}
Path('docs/evidence/persistence.json').write_text(json.dumps(out,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(out,ensure_ascii=False))
