const fs=require('fs'),path=require('path'),assert=require('assert');
const req=process.env.CODEX_NODE_MODULES?require('node:module').createRequire(path.join(process.env.CODEX_NODE_MODULES,'package.json')):require;
const {chromium}=req('playwright');
const cfg=JSON.parse(fs.readFileSync('.local/settings.json','utf8').replace(/^\uFEFF/,''));
const origin=`http://127.0.0.1:${cfg.AppPort}`,out='docs/evidence';fs.mkdirSync(out,{recursive:true});
(async()=>{
for(let i=0;i<100;i++){try{if((await fetch(origin+'/health')).ok)break}catch{}await new Promise(r=>setTimeout(r,200))}
const browser=await chromium.launch({headless:true,executablePath:process.env.BROWSER_EXECUTABLE||'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
const context=await browser.newContext({viewport:{width:1440,height:1000},locale:'uk-UA'});const page=await context.newPage();let errors=[];page.on('pageerror',e=>errors.push(String(e)));const checks=[];
await page.goto(origin);await page.screenshot({path:out+'/01-home.png',fullPage:true});assert(await page.locator('h1').innerText());checks.push('Desktop public home rendered');
await page.goto(origin+'/articles?q=zzzznoresult987654321');assert(await page.getByText('За цим запитом матеріалів немає').isVisible());await page.screenshot({path:out+'/02-empty.png',fullPage:true});checks.push('Search empty state visible');
await page.goto(origin+'/account/login');await page.locator('[name=Email]').fill('author@scienceline.example');await page.locator('[name=Password]').fill(cfg.DemoPassword);await page.locator('button[type=submit]').click();await page.waitForURL(origin+'/');
await page.goto(origin+'/author/create');await page.getByRole('button',{name:'Зберегти чернетку'}).click();assert(await page.getByText('Введіть назву.',{exact:true}).first().isVisible());await page.screenshot({path:out+'/03-validation.png',fullPage:true});checks.push('Server validation messages visible');
await page.goto(origin+'/author');await page.screenshot({path:out+'/04-author.png',fullPage:true});
await page.goto(origin+'/');await page.setViewportSize({width:390,height:844});await page.screenshot({path:out+'/05-mobile.png',fullPage:true});assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));checks.push('390px home has no horizontal overflow');
await page.goto(origin+'/articles');assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));await page.screenshot({path:out+'/06-mobile-catalog.png',fullPage:true});checks.push('390px catalogue has no horizontal overflow');
await page.goto(origin+'/author/create');assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));checks.push('390px author form has no horizontal overflow');
await context.close();const ec=await browser.newContext({viewport:{width:1440,height:1000},locale:'uk-UA'});const ep=await ec.newPage();await ep.goto(origin+'/account/login');await ep.locator('[name=Email]').fill('editor@scienceline.example');await ep.locator('[name=Password]').fill(cfg.DemoPassword);await ep.locator('button[type=submit]').click();await ep.goto(origin+'/editor');await ep.screenshot({path:out+'/07-editor.png',fullPage:true});checks.push('Editor review queue rendered');assert.equal(errors.length,0);checks.push('No page JavaScript exceptions');
fs.writeFileSync(out+'/browser-results.json',JSON.stringify({time_utc:new Date().toISOString(),browser:'Microsoft Edge (Chromium) headless',version:browser.version(),checks,passed:checks.length,errors},null,2));await browser.close();console.log(JSON.stringify({passed:checks.length,errors:errors.length}));
})().catch(e=>{console.error(e);process.exit(1)});
