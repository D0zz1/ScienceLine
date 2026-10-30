// Laboratory frontend timings and scoped accessibility checks, not a field CWV/WCAG certificate.
const fs=require('fs'),path=require('path'),assert=require('assert');
const req=process.env.CODEX_NODE_MODULES?require('node:module').createRequire(path.join(process.env.CODEX_NODE_MODULES,'package.json')):require;
const {chromium}=req('playwright');const base='http://127.0.0.1:5181',out=process.env.QUALITY_EVIDENCE_DIR||'docs/evidence/quality';fs.mkdirSync(out,{recursive:true});
const results={date:new Date().toISOString(),conditions:{origin:base,network:'10 Mbit/s down, 5 Mbit/s up, 40 ms latency',cpuSlowdown:4,repeats:3,coldContext:true,fieldINP:false,scope:'Headless local Edge laboratory measurements. Event Timing samples and input-to-paint are reported separately from field INP.'},performance:[],accessibility:[]};
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:process.env.BROWSER_EXECUTABLE||'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});results.browser=browser.version();
 try{
  for(const viewport of[{width:1440,height:1000},{width:390,height:844}])for(let repeat=1;repeat<=3;repeat++){
   const ctx=await browser.newContext({viewport,locale:'uk-UA'});const page=await ctx.newPage(),cdp=await ctx.newCDPSession(page);
   await cdp.send('Network.enable');await cdp.send('Network.emulateNetworkConditions',{offline:false,latency:40,downloadThroughput:10*1024*1024/8,uploadThroughput:5*1024*1024/8});await cdp.send('Emulation.setCPUThrottlingRate',{rate:4});
   await page.addInitScript(()=>{window.qm={lcp:0,cls:0,events:[],paint:[]};try{new PerformanceObserver(l=>{for(const x of l.getEntries())window.qm.lcp=x.startTime}).observe({type:'largest-contentful-paint',buffered:true});new PerformanceObserver(l=>{for(const x of l.getEntries())if(!x.hadRecentInput)window.qm.cls+=x.value}).observe({type:'layout-shift',buffered:true});new PerformanceObserver(l=>{for(const x of l.getEntries())if(x.interactionId)window.qm.events.push({name:x.name,duration:x.duration,interactionId:x.interactionId})}).observe({type:'event',buffered:true,durationThreshold:16});}catch(e){window.qm.error=e.message}document.addEventListener('input',()=>{const t=performance.now();requestAnimationFrame(()=>requestAnimationFrame(()=>window.qm.paint.push(performance.now()-t)))})});
   await page.goto(base+'/articles',{waitUntil:'networkidle'});await page.locator('input[name=q]').focus();for(const key of['m','e','t','o','d']){await page.keyboard.press(key);await page.waitForTimeout(80)}await page.waitForTimeout(150);
   const metrics=await page.evaluate(()=>{const n=performance.getEntriesByType('navigation')[0],r=performance.getEntriesByType('resource');return{...window.qm,ttfbMs:n.responseStart-n.requestStart,domContentLoadedMs:n.domContentLoadedEventEnd,loadMs:n.loadEventEnd,transferBytes:n.transferSize+r.reduce((s,x)=>s+x.transferSize,0),requests:r.length+1,horizontalOverflow:document.documentElement.scrollWidth>innerWidth+1}});
   assert(!metrics.horizontalOverflow);results.performance.push({viewport,repeat,...metrics});if(repeat===1)await page.screenshot({path:path.join(out,`frontend-${viewport.width}.png`),fullPage:true});await ctx.close();console.log('FRONTEND',viewport.width,repeat,'LCP',Math.round(metrics.lcp),'CLS',metrics.cls);
  }
  for(const width of[320,360,768,1440]){
   const ctx=await browser.newContext({viewport:{width,height:900},locale:'uk-UA'}),page=await ctx.newPage();await page.goto(base+'/articles',{waitUntil:'networkidle'});
   const facts=await page.evaluate(()=>({overflow:document.documentElement.scrollWidth>innerWidth+1,imagesMissingAlt:[...document.images].filter(i=>!i.hasAttribute('alt')).length,missingInputNames:[...document.querySelectorAll('input:not([type=hidden]),select,textarea')].filter(e=>!e.labels?.length&&!e.getAttribute('aria-label')&&!e.getAttribute('aria-labelledby')).map(e=>e.name),lang:document.documentElement.lang}));
   await page.keyboard.press('Tab');const focus=await page.evaluate(()=>{const e=document.activeElement,s=getComputedStyle(e);return{tag:e.tagName,text:(e.textContent||e.getAttribute('aria-label')||'').trim().slice(0,80),visibleOutline:s.outlineStyle!=='none'&&parseFloat(s.outlineWidth)>0||s.boxShadow!=='none'}});
   assert(!facts.overflow&&facts.imagesMissingAlt===0&&facts.missingInputNames.length===0&&facts.lang==='uk'&&focus.visibleOutline,JSON.stringify({facts,focus}));results.accessibility.push({width,...facts,focus,pass:true});await ctx.close();
  }
  const ctx=await browser.newContext({viewport:{width:1440,height:1000}}),p=await ctx.newPage();await p.goto(base+'/account/login',{waitUntil:'networkidle'});results.loginLabels=await p.locator('input:not([type=hidden])').evaluateAll(es=>es.map(e=>({name:e.name,hasLabel:e.labels.length>0})));assert(results.loginLabels.every(x=>x.hasLabel));await ctx.close();
 }finally{await browser.close();fs.writeFileSync(path.join(out,'frontend-results.json'),JSON.stringify(results,null,2));}
})();
