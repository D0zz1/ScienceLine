"""Loopback-only volume and concurrent-load experiment against an isolated DB.
python tests/quality_experiments.py --settings PRIVATE_JSON --psql PATH
Never targets the normal scienceline database. Raw evidence retains samples.
"""
from pathlib import Path
import argparse,json,os,subprocess,time,datetime,concurrent.futures,statistics,http.client,urllib.request,urllib.parse,urllib.error,http.cookiejar,re,html,uuid,platform,threading
p=argparse.ArgumentParser();p.add_argument('--settings',required=True);p.add_argument('--psql',required=True);p.add_argument('--out',default='docs/evidence/quality');p.add_argument('--only-load',action='store_true');a=p.parse_args()
cfg=json.loads(Path(a.settings).read_text(encoding='utf-8-sig'));db=cfg['Database'];port=int(cfg['AppPort']);assert db.startswith('scienceline_quality_')and port==5181,'Only the isolated loopback quality service is permitted'
out=Path(a.out);out.mkdir(parents=True,exist_ok=True);env=dict(os.environ,PGPASSWORD=cfg['DbPassword'],PGCLIENTENCODING='UTF8');started=datetime.datetime.now(datetime.timezone.utc).isoformat()
def sql(s):
 r=subprocess.run([a.psql,'-X','-v','ON_ERROR_STOP=1','-h','127.0.0.1','-p',str(cfg['DbPort']),'-U','scienceline','-d',db,'-At'],input=s,encoding='utf-8',capture_output=True,env=env,check=True);return r.stdout.strip()
def pct(xs,n):return round(sorted(xs)[max(0,min(len(xs)-1,int((len(xs)-1)*n)))],2)
def request(path,connection=None):
 own=connection is None;c=connection or http.client.HTTPConnection('127.0.0.1',port,timeout=20);t=time.perf_counter()
 try:c.request('GET',path);r=c.getresponse();body=r.read();return r.status,body.decode('utf-8'),(time.perf_counter()-t)*1000
 finally:
  if own:c.close()
def sample(path,n=40):
 c=http.client.HTTPConnection('127.0.0.1',port,timeout=20)
 try:
  for _ in range(5):assert request(path,c)[0]==200
  rows=[request(path,c)for _ in range(n)];assert all(r[0]==200 for r in rows)
  xs=[r[2]for r in rows];return {'requests':n,'p50_ms':pct(xs,.5),'p95_ms':pct(xs,.95),'p99_ms':pct(xs,.99),'samples_ms':[round(v,3)for v in xs]}
 finally:c.close()
def resource():
 command="Get-CimInstance Win32_PerfFormattedData_PerfProc_Process | Where-Object {$_.Name -like '*ScienceLine*' -or $_.Name -like 'dotnet*' -or $_.Name -like 'postgres*'} | Select-Object Name,IDProcess,PercentProcessorTime,WorkingSetPrivate | ConvertTo-Json -Compress"
 r=subprocess.run(['powershell.exe','-NoProfile','-Command',command],capture_output=True,text=True)
 try:return json.loads(r.stdout)if r.stdout.strip()else[]
 except ValueError:return[]
class NoRedirect(urllib.request.HTTPRedirectHandler):
 def redirect_request(self,*args,**kwargs):return None
client=urllib.request.build_opener(urllib.request.ProxyHandler({}),urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()),NoRedirect())
base=f'http://127.0.0.1:{port}'
def form(path,data=None):
 req=urllib.request.Request(base+path,None if data is None else urllib.parse.urlencode(data).encode())
 try:r=client.open(req,timeout=20)
 except urllib.error.HTTPError as e:r=e
 return r.code,r.read().decode('utf-8'),dict(r.headers)
def token(text):return html.unescape(re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"',text)[1])
s,b,h=form('/account/login');s,b,h=form('/account/login',{'Email':'author@scienceline.example','Password':cfg['DemoPassword'],'__RequestVerificationToken':token(b)});assert s==302
def mutation():
 s,b,h=form('/author/create');assert s==200
 rid=uuid.uuid4();data={'Id':0,'Version':0,'ClientRequestId':str(rid),'Title':'QUALITY контроль збереження '+str(rid)[:8],'Summary':'Синтетичний матеріал для контрольованого досліду обсягу бази даних.','Body':'Це спеціально створений синтетичний матеріал ізольованої бази. Перевіряється збереження статті, текстової версії та правильність зовнішніх ключів.','SourceUrl':'https://example.org/quality','CoverKey':'orbit','ImageAlt':'Синтетична ілюстрація контрольованого досліду','CategoryId':int(sql('SELECT min("Id") FROM "Categories";')),'__RequestVerificationToken':token(b)}
 t=time.perf_counter();s,b,h=form('/author/create',data);ms=(time.perf_counter()-t)*1000;assert s==302,('mutation',s,b[:100]);rows=sql(f'''SELECT a."Id"||','||count(r."Id") FROM "Articles" a JOIN "ArticleRevisions" r ON r."ArticleId"=a."Id" WHERE a."ClientRequestId"='{rid}' GROUP BY a."Id";''');ident,revisions=map(int,rows.split(','));assert revisions==1
 sql(f'''BEGIN; DELETE FROM "ArticleRevisions" WHERE "ArticleId"={ident}; DELETE FROM "Articles" WHERE "Id"={ident}; COMMIT;''');return {'status':s,'persistedRevisionCount':revisions,'elapsed_ms':round(ms,2),'cleanup':'isolated synthetic record removed'}
volumes=json.loads((out/'volume-results.json').read_text(encoding='utf-8'))['volumes'] if a.only_load else []
for size in ([] if a.only_load else [100,1000,10000]):
 current=int(sql('SELECT count(*) FROM "Articles";'));assert current<=size,(current,size)
 needed=size-current
 sql(f'''INSERT INTO "Articles" ("Title","Summary","Body","SourceUrl","CoverKey","ImageAlt","CategoryId","AuthorId","ClientRequestId","Status","Version","RevisionNumber","CreatedAtUtc","UpdatedAtUtc","PublishedAtUtc")
 SELECT (ARRAY['Космос і спостереження','Клімат і вимірювання','Технології обробки даних','Науковий метод'])[1+g%4]||' · QUALITY '||g||'/{size}', 'Синтетичний анонс для повторюваного навантажувального експерименту.',repeat('Контрольовані синтетичні дані пояснюють метод, вибірку та похибку вимірювання. ',12),'https://example.org/quality/'||g,'orbit','Схематична ілюстрація тестового матеріалу',(SELECT "Id" FROM "Categories" ORDER BY "Id" OFFSET (g%4) LIMIT 1),(SELECT "Id" FROM "AspNetUsers" WHERE "Email"='author@scienceline.example'),md5('quality-{size}-'||g)::uuid,(ARRAY['Published','Published','Published','Published','Published','Draft','Draft','UnderReview','UnderReview','ChangesRequested'])[1+g%10],1,1,now()-g*interval '2 hour',now()-g*interval '1 hour',CASE WHEN g%10<5 THEN now()-g*interval '1 hour' ELSE NULL END FROM generate_series(1,{needed}) g;
 INSERT INTO "ArticleRevisions" ("ArticleId","Number","Title","Summary","Body","SourceUrl","CreatedAtUtc") SELECT a."Id",1,a."Title",a."Summary",a."Body",a."SourceUrl",a."CreatedAtUtc" FROM "Articles" a WHERE NOT EXISTS(SELECT 1 FROM "ArticleRevisions" r WHERE r."ArticleId"=a."Id");
 INSERT INTO "ArticleTags" ("ArticleId","TagId") SELECT a."Id",(SELECT "Id" FROM "Tags" ORDER BY "Id" OFFSET (a."Id"%6) LIMIT 1) FROM "Articles" a WHERE NOT EXISTS(SELECT 1 FROM "ArticleTags" t WHERE t."ArticleId"=a."Id"); ANALYZE "Articles";''')
 ident=int(sql('SELECT min("Id") FROM "Articles" WHERE "Status"=\'Published\';'));published=int(sql('SELECT count(*) FROM "Articles" WHERE "Status"=\'Published\';'))
 paths={'catalog':'/articles','search':'/articles?q='+urllib.parse.quote('метод'),'category':'/articles?category=1','pagination':'/articles?page=2','detail':f'/articles/{ident}'}
 measures={name:sample(path)for name,path in paths.items()}
 first=request(paths['catalog'])[1];second=request(paths['pagination'])[1];ids1=set(re.findall(r'href="/articles/(\d+)"',first));ids2=set(re.findall(r'href="/articles/(\d+)"',second));assert len(ids1)==9 and len(ids2)==9 and not(ids1&ids2),(ids1,ids2)
 mutate=mutation();count=int(sql('SELECT count(*) FROM "Articles";'));assert count==size
 plan=json.loads(sql('EXPLAIN (ANALYZE,BUFFERS,FORMAT JSON) SELECT "Id","Title" FROM "Articles" WHERE "Status"=\'Published\' ORDER BY "PublishedAtUtc" DESC,"Id" DESC LIMIT 9;'))
 (out/f'query-plan-{size}.json').write_text(json.dumps(plan,indent=2),encoding='utf-8')
 volume={'articles':size,'published':published,'database_bytes':int(sql('SELECT pg_database_size(current_database());')),'paths':paths,'measurements':measures,'mutation':mutate,'pagination':{'firstIds':sorted(ids1),'secondIds':sorted(ids2),'disjoint':True},'queryPlanFile':f'query-plan-{size}.json'};volumes.append(volume)
 print('VOLUME',size,'PASS', {k:v['p95_ms']for k,v in measures.items()},flush=True)
 (out/'volume-results.json').write_text(json.dumps({'startedUtc':started,'database':db,'synthetic':True,'volumes':volumes},ensure_ascii=False,indent=2),encoding='utf-8')
resources=[];load=[];thresholds={'error_rate_max':.01,'p95_ms_max':500,'p99_ms_max':1000,'timeout_seconds':20,'scope':'project-specific local acceptance criteria, not a field SLA'}
assert int(sql('SELECT count(*) FROM "Articles";'))==10000
ident=int(sql('SELECT min("Id") FROM "Articles" WHERE "Status"=\'Published\';'))
for users in [1,5,10]:
 for repeat in [1,2]:
  resources.append({'phase':'before','users':users,'repeat':repeat,'processes':resource(),'dbConnections':int(sql('SELECT count(*) FROM pg_stat_activity WHERE datname=current_database();'))})
  start=time.perf_counter();warm=2;duration=10
  def monitor():
   for target in [3,6,9]:
    time.sleep(max(0,target-(time.perf_counter()-start)))
    resources.append({'phase':'during','elapsedSeconds':round(time.perf_counter()-start,3),'users':users,'repeat':repeat,'processes':resource(),'dbConnections':int(sql('SELECT count(*) FROM pg_stat_activity WHERE datname=current_database();'))})
  monitor_thread=threading.Thread(target=monitor,daemon=True);monitor_thread.start()
  paths=['/articles','/articles?page=2','/articles?category=1','/articles?q='+urllib.parse.quote('метод'),f'/articles/{ident}']
  def worker(seed):
   c=http.client.HTTPConnection('127.0.0.1',port,timeout=20);rows=[];i=seed
   try:
    while time.perf_counter()-start<warm+duration:
     t=time.perf_counter();phase=t-start
     try:status,body,ms=request(paths[i%len(paths)],c)
     except Exception as e:status=0;ms=(time.perf_counter()-t)*1000;c.close();c=http.client.HTTPConnection('127.0.0.1',port,timeout=20)
     if phase>=warm:rows.append([round(phase-warm,3),status,round(ms,3)])
     i+=1
    return rows
   finally:c.close()
  with concurrent.futures.ThreadPoolExecutor(max_workers=users)as pool:rows=sum(list(pool.map(worker,range(users))),[])
  xs=[r[2]for r in rows];errors=sum(r[1]!=200 for r in rows);elapsed=time.perf_counter()-start-warm
  monitor_thread.join()
  row={'concurrentUsers':users,'repeat':repeat,'warmupSeconds':warm,'scheduledSeconds':duration,'observedSeconds':round(elapsed,3),'requests':len(rows),'errors':errors,'errorRate':errors/len(rows),'rps':round(len(rows)/elapsed,2),'p50_ms':pct(xs,.5),'p95_ms':pct(xs,.95),'p99_ms':pct(xs,.99),'max_ms':round(max(xs),2),'samplesFile':f'load-{users}-{repeat}.json'};row['pass']=row['errorRate']<=thresholds['error_rate_max']and row['p95_ms']<=thresholds['p95_ms_max']and row['p99_ms']<=thresholds['p99_ms_max'];load.append(row)
  (out/row['samplesFile']).write_text(json.dumps(rows),encoding='utf-8');resources.append({'phase':'after','users':users,'repeat':repeat,'processes':resource(),'dbConnections':int(sql('SELECT count(*) FROM pg_stat_activity WHERE datname=current_database();'))});print('LOAD',users,repeat,'PASS'if row['pass']else'FAIL',row['requests'],row['p95_ms'],flush=True)
  (out/'load-results.json').write_text(json.dumps({'startedUtc':started,'databaseArticles':10000,'client':'Python HTTPConnection persistent connections; closed model; no think time','thresholdsDefinedBeforeRun':thresholds,'runs':load,'resources':resources,'limits':'Local single host; process CPU and memory snapshots at 3, 6 and 9 s plus phase boundaries; no production SLA inference.'},ensure_ascii=False,indent=2),encoding='utf-8')
facts={'startedUtc':started,'finishedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'os':platform.platform(),'python':platform.python_version(),'processors':os.cpu_count(),'postgres':sql('SHOW server_version;'),'scope':'Isolated clone, normal database unchanged','volumePass':len(volumes)==3,'loadPass':all(r['pass']for r in load)}
(out/'environment.json').write_text(json.dumps(facts,ensure_ascii=False,indent=2),encoding='utf-8');assert facts['volumePass']and facts['loadPass'],facts
