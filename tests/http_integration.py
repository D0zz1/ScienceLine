"""Real HTTP + PostgreSQL integration tests. Destructive only to records marked RUN_ID.
Usage: python tests/http_integration.py --settings .local/settings.json --psql PATH
Requires the local application started with Demo enabled. No third-party Python packages.
"""
import argparse, concurrent.futures, datetime, html, http.cookiejar, json, os, re, statistics, subprocess, time, urllib.error, urllib.parse, urllib.request, uuid
from pathlib import Path

parser=argparse.ArgumentParser();parser.add_argument('--settings',default='.local/settings.json');parser.add_argument('--psql',required=True);parser.add_argument('--out',default='docs/evidence/http-results.json')
args=parser.parse_args();cfg=json.loads(Path(args.settings).read_text(encoding='utf-8-sig'));base=f"http://127.0.0.1:{cfg['AppPort']}";run='E2E-'+uuid.uuid4().hex[:10];results=[]
class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self,*a,**k):return None
class Client:
    def __init__(self):self.opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()),NoRedirect())
    def request(self,path,data=None):
        url=base+path;encoded=None if data is None else urllib.parse.urlencode(data,doseq=True).encode()
        try:r=self.opener.open(urllib.request.Request(url,encoded),timeout=20)
        except urllib.error.HTTPError as e:r=e
        return r.code,r.read().decode('utf-8'),dict(r.headers)
    def get(self,path):return self.request(path)
    def post(self,path,data):return self.request(path,data)
    def token(self,path):
        status,text,_=self.get(path);assert status==200,(path,status)
        return html.unescape(re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"',text).group(1))
    def login(self,email):
        token=self.token('/account/login');r=self.post('/account/login',{'Email':email,'Password':cfg['DemoPassword'],'__RequestVerificationToken':token});assert r[0]==302,('login',r[0])
def sql(query):
    env=os.environ.copy();env['PGPASSWORD']=cfg['DbPassword'];env['PGCLIENTENCODING']='UTF8'
    p=subprocess.run([args.psql,'-h','127.0.0.1','-p',str(cfg['DbPort']),'-U','scienceline','-d','scienceline','-At','-v','ON_ERROR_STOP=1','-c',query],env=env,capture_output=True,encoding='utf-8',check=True);return p.stdout.strip()
def check(name,fn):
    start=time.perf_counter()
    try:detail=fn();results.append({'id':f'T{len(results)+1:02}','name':name,'result':'PASS','detail':detail,'ms':round((time.perf_counter()-start)*1000,2)})
    except Exception as e:results.append({'id':f'T{len(results)+1:02}','name':name,'result':'FAIL','detail':str(e),'ms':round((time.perf_counter()-start)*1000,2)});raise
def expect(value,target):assert value==target,(value,target);return str(value)
guest=Client();author=Client();second=Client();editor=Client();reader=Client()
for _ in range(80):
    try:
        if guest.get('/health')[0]==200:break
    except Exception:pass
    time.sleep(.25)
try:
    check('Public catalogue reads PostgreSQL',lambda:expect(guest.get('/articles')[0],200))
    check('Guest editor access requires sign-in',lambda:expect(guest.get('/editor')[0],302))
    for client,email in [(author,'author'),(second,'second'),(editor,'editor'),(reader,'reader')]:client.login(email+'@scienceline.example')
    check('Reader has no editorial permission',lambda:expect(reader.get('/editor')[0],403))
    check('Author cannot impersonate editor',lambda:expect(author.get('/editor')[0],403))
    token=author.token('/author/create');key=str(uuid.uuid4())
    form={'__RequestVerificationToken':token,'ClientRequestId':key,'Title':run+' — як працює наукова публікація','Summary':'Перевірка редакційного циклу на справжній базі даних PostgreSQL.','Body':'Перший абзац навчального матеріалу пояснює перевірюваний факт.\nДругий абзац містить контекст дослідження та його обмеження.','SourceUrl':'https://science.nasa.gov/mission/webb/','CategoryId':'1','TagIds':['1','2'],'CoverKey':'orbit','ImageAlt':'Схематичні орбіти планет на світлому тлі'}
    check('POST without CSRF is rejected',lambda:expect(author.post('/author/create',{k:v for k,v in form.items() if k!='__RequestVerificationToken'})[0],400))
    for field,value,name in [('Title','', 'Empty title rejected'),('Body','', 'Empty body rejected'),('SourceUrl','javascript:alert(1)','Non-HTTP source rejected'),('CategoryId','999999','Unknown category rejected'),('TagIds',['999999'],'Unknown tag rejected'),('ImageAlt','','Missing image alternative rejected'),('CoverKey','../../etc/passwd','Unknown illustration rejected')]:
        check(name,lambda field=field,value=value:expect(author.post('/author/create',dict(form,**{field:value}))[0],400))
    # An overposting attempt must have no effect on author, state or role.
    created=author.post('/author/create',dict(form,AuthorId='someone-else',Status='Published',Role='Editor'));check('Valid draft is created',lambda:expect(created[0],302));aid=int(re.search(r'/author/(\d+)',created[2]['Location']).group(1))
    check('Draft is invisible at public direct URL',lambda:expect(guest.get(f'/articles/{aid}')[0],404))
    check('Foreign draft details are denied',lambda:expect(second.get(f'/author/{aid}')[0],404))
    check('Foreign history is denied',lambda:expect(second.get(f'/history/{aid}')[0],404))
    check('Overposted state ignored by DTO',lambda:expect(sql(f'SELECT "Status" FROM "Articles" WHERE "Id"={aid}'),'Draft'))
    check('Same request ID returns same draft',lambda:expect(author.post('/author/create',form)[2]['Location'],created[2]['Location']))
    check('Unique request key stored once',lambda:expect(sql(f'SELECT count(*) FROM "Articles" WHERE "ClientRequestId"=\'{key}\''),'1'))
    version=int(sql(f'SELECT "Version" FROM "Articles" WHERE "Id"={aid}'));token=author.token(f'/author/{aid}')
    check('Foreign draft submission denied',lambda:expect(second.post(f'/author/{aid}/submit',{'__RequestVerificationToken':second.token('/author'),'version':version})[0],404))
    check('Author submits current version',lambda:expect(author.post(f'/author/{aid}/submit',{'__RequestVerificationToken':token,'version':version})[0],302))
    check('Draft cannot be edited during review',lambda:expect(author.get(f'/author/{aid}/edit')[0],409))
    etoken=editor.token(f'/editor/{aid}');version=int(sql(f'SELECT "Version" FROM "Articles" WHERE "Id"={aid}'))
    check('Return without explanation rejected',lambda:expect(editor.post(f'/editor/{aid}/return',{'__RequestVerificationToken':etoken,'version':version,'comment':''})[0],400))
    check('Editor returns with explanation',lambda:expect(editor.post(f'/editor/{aid}/return',{'__RequestVerificationToken':etoken,'version':version,'comment':'Уточніть висновок і поясніть межі застосування.'})[0],302))
    text=author.get(f'/author/{aid}')[1];check('Return comment reaches author',lambda:expect('Уточніть' in html.unescape(text),True))
    current=int(sql(f'SELECT "Version" FROM "Articles" WHERE "Id"={aid}'));edit=dict(form,Id=aid,Version=current,__RequestVerificationToken=author.token(f'/author/{aid}/edit'));edit['Body']+='\nУточнений висновок: результати слід перевіряти за першоджерелом.'
    check('Stale edit returns conflict',lambda:expect(author.post(f'/author/{aid}/edit',dict(edit,Version=1))[0],409))
    check('Corrected version is saved',lambda:expect(author.post(f'/author/{aid}/edit',edit)[0],302))
    check('Revision history contains two snapshots',lambda:expect(sql(f'SELECT count(*) FROM "ArticleRevisions" WHERE "ArticleId"={aid}'),'2'))
    diff=author.get(f'/history/{aid}?from=1&to=2');check('Text comparison displays added paragraph',lambda:expect(diff[0]==200 and 'diff-added' in diff[1],True))
    current=int(sql(f'SELECT "Version" FROM "Articles" WHERE "Id"={aid}'));stoken=author.token(f'/author/{aid}');author.post(f'/author/{aid}/submit',{'__RequestVerificationToken':stoken,'version':current})
    check('Repeated submission does not duplicate decision',lambda:expect(author.post(f'/author/{aid}/submit',{'__RequestVerificationToken':stoken,'version':current})[0],302))
    current=int(sql(f'SELECT "Version" FROM "Articles" WHERE "Id"={aid}'));etoken=editor.token(f'/editor/{aid}');payload={'__RequestVerificationToken':etoken,'version':current}
    check('Editor publishes reviewed article',lambda:expect(editor.post(f'/editor/{aid}/publish',payload)[0],302))
    check('Repeated publication is idempotent',lambda:expect(editor.post(f'/editor/{aid}/publish',payload)[0],302))
    check('Publication event exists once',lambda:expect(sql(f'SELECT count(*) FROM "EditorialDecisions" WHERE "ArticleId"={aid} AND "Kind"=\'Published\''),'1'))
    check('Published material becomes public',lambda:expect(guest.get(f'/articles/{aid}')[0],200))
    check('Search finds published title',lambda:expect(run in html.unescape(guest.get('/articles?q='+run)[1]),True))
    check('Search without matches has helpful empty state',lambda:expect('За цим запитом матеріалів немає' in html.unescape(guest.get('/articles?q=zzzznoresult987654321')[1]),True))
    check('Search SQL metacharacters are harmless',lambda:expect(guest.get('/articles?q='+urllib.parse.quote("' OR 1=1; --"))[0],200))
    check('Published records cannot be edited by author',lambda:expect(author.get(f'/author/{aid}/edit')[0],409))
    # Browser interpretation is tested separately; here verify server encoding in returned HTML.
    xtoken=author.token('/author/create');xform=dict(form,__RequestVerificationToken=xtoken,ClientRequestId=str(uuid.uuid4()),Title=run+' <script>alert(1)</script>')
    xr=author.post('/author/create',xform);xid=int(re.search(r'/author/(\d+)',xr[2]['Location']).group(1));xt=author.get(f'/author/{xid}')[1]
    check('Article text is HTML encoded',lambda:expect('<script>alert(1)</script>' not in xt and '&lt;script&gt;' in xt,True))
    check('Security headers are present',lambda:expect(all(k in guest.get('/')[2] for k in ['Content-Security-Policy','X-Content-Type-Options','X-Frame-Options']),True))
    timings=[]
    for _ in range(50):
        t=time.perf_counter();assert guest.get('/articles')[0]==200;timings.append((time.perf_counter()-t)*1000)
    perf={'requests':50,'concurrency':1,'median_ms':round(statistics.median(timings),2),'p95_ms':round(sorted(timings)[47],2),'min_ms':round(min(timings),2),'max_ms':round(max(timings),2),'scope':'Loopback HTTP, warm application, small demo database; not a load capacity claim'}
    check('50 warm catalogue requests completed',lambda:perf)
    marker={'run_id':run,'article_id':aid,'title':form['Title'],'request_id':key,'revision_count':2,'status':'Published'}
    Path('.local/persistence-marker.json').write_text(json.dumps(marker,ensure_ascii=False,indent=2),encoding='utf-8')
finally:
    report={'run_id':run,'time_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'base_url':base,'database':'PostgreSQL 17.11 (real server)','tests':results,'passed':sum(x['result']=='PASS' for x in results),'failed':sum(x['result']=='FAIL' for x in results)}
    out=Path(args.out);out.parent.mkdir(parents=True,exist_ok=True);out.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps({'passed':report['passed'],'failed':report['failed'],'report':str(out)},ensure_ascii=False))
