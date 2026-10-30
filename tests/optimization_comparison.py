"""Controlled ablation of the already deployed index, only in an isolated clone."""
from pathlib import Path
import argparse,json,os,subprocess,datetime,http.client,time
p=argparse.ArgumentParser();p.add_argument('--settings',required=True);p.add_argument('--psql',required=True);p.add_argument('--out',default='docs/evidence/quality');a=p.parse_args()
c=json.loads(Path(a.settings).read_text(encoding='utf-8-sig'));assert c['Database'].startswith('scienceline_quality_') and c['AppPort']==5181
out=Path(a.out);out.mkdir(parents=True,exist_ok=True)
def sql(s):
 r=subprocess.run([a.psql,'-XqAt','-v','ON_ERROR_STOP=1','-h','127.0.0.1','-p',str(c['DbPort']),'-U','scienceline','-d',c['Database']],input=s,encoding='utf-8',capture_output=True,env=dict(os.environ,PGPASSWORD=c['DbPassword'],PGCLIENTENCODING='UTF8'),check=True);return r.stdout.strip()
name='IX_Articles_Status_PublishedAtUtc';definition=sql(f"SELECT pg_get_indexdef(indexrelid) FROM pg_index WHERE indexrelid='\"{name}\"'::regclass;");assert definition.startswith('CREATE INDEX')
assert int(sql('SELECT count(*) FROM "Articles";'))==10000
query='SELECT "Id","Title" FROM "Articles" WHERE "Status"=\'Published\' ORDER BY "PublishedAtUtc" DESC,"Id" DESC LIMIT 9'
def http_sample():
 con=http.client.HTTPConnection('127.0.0.1',5181,timeout=15);rows=[];ids=[]
 try:
  for i in range(45):
   t=time.perf_counter();con.request('GET','/articles');r=con.getresponse();b=r.read().decode('utf-8');assert r.status==200
   if i>=5:rows.append(round((time.perf_counter()-t)*1000,4))
 finally:con.close()
 return rows
def measure(label):
 plans=[json.loads(sql('EXPLAIN (ANALYZE,BUFFERS,FORMAT JSON) '+query+';'))[0] for _ in range(20)]
 rows=sql(query+';');size=int(sql('SELECT pg_database_size(current_database());'))
 # Rolled back write workload, with all constraints and indexes active for that phase.
 write='''BEGIN; EXPLAIN (ANALYZE,BUFFERS,FORMAT JSON) INSERT INTO "Articles" ("Title","Summary","Body","SourceUrl","CoverKey","ImageAlt","CategoryId","AuthorId","ClientRequestId","Status","Version","RevisionNumber","CreatedAtUtc","UpdatedAtUtc","PublishedAtUtc") SELECT 'INDEX comparison '||g,'Synthetic summary',repeat('Synthetic controlled comparison body. ',5),'https://example.org/comparison','orbit','Synthetic diagram',(SELECT min("Id") FROM "Categories"),(SELECT "Id" FROM "AspNetUsers" WHERE "Email"='author@scienceline.example'),md5(random()::text||g)::uuid,'Published',1,1,now(),now(),now() FROM generate_series(1,100) g; ROLLBACK;'''
 writes=[json.loads(sql(write))[0]['Execution Time'] for _ in range(5)]
 return {'phase':label,'sqlPlans':plans,'sqlExecutionMs':[x['Execution Time'] for x in plans],'catalogHttpMs':http_sample(),'firstNineRows':rows,'databaseBytes':size,'write100RowsMs':writes,'writePolicy':'100 synthetic INSERTs per transaction, 5 repeats, every transaction rolled back'}
result={'date':datetime.datetime.now(datetime.timezone.utc).isoformat(),'articles':10000,'index':name,'definition':definition,'method':'Already implemented composite index ablation; same clone and query; warm caches; 20 SQL and 40 HTTP samples after 5 warmups per phase. Baseline first, final indexed state second; order effects remain possible.'}
try:
 sql(f'DROP INDEX "{name}"; ANALYZE "Articles";');result['before']=measure('without existing composite index')
finally:
 if sql(f"SELECT to_regclass('\"{name}\"') IS NOT NULL;")!='t':sql(definition+';')
sql('ANALYZE "Articles";');result['after']=measure('with deployed composite index')
assert result['before']['firstNineRows']==result['after']['firstNineRows'];assert int(sql('SELECT count(*) FROM "Articles";'))==10000
result['indexBytes']=int(sql(f'SELECT pg_relation_size(\'"{name}"\');'));result['sameRows']=True;result['indexRestored']=True
(out/'index-comparison.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print('INDEX COMPARISON PASS; same rows; deployed index restored.')
