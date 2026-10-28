using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ScienceLine.Web.Data;
using ScienceLine.Web.Models;
using ScienceLine.Web.Services;
using System.Threading.RateLimiting;

var migrateOnly=args.Contains("--migrate");
var builder=WebApplication.CreateBuilder(args.Where(a=>a!="--migrate").ToArray());
var connection=builder.Configuration.GetConnectionString("Portal")
    ?? throw new InvalidOperationException("Set ConnectionStrings__Portal. See README and scripts/start-local.ps1.");
builder.Services.AddDbContext<PortalDbContext>(o=>o.UseNpgsql(connection));
builder.Services.AddIdentity<PortalUser,IdentityRole>(o=>{
    o.User.RequireUniqueEmail=true;o.Password.RequiredLength=12;
    o.Password.RequireDigit=true;o.Password.RequireUppercase=true;o.Password.RequireLowercase=true;
    o.Password.RequireNonAlphanumeric=false;o.Lockout.MaxFailedAccessAttempts=5;
    o.Lockout.DefaultLockoutTimeSpan=TimeSpan.FromMinutes(5);
}).AddEntityFrameworkStores<PortalDbContext>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(o=>{
    o.Cookie.Name="ScienceLine.Auth";o.Cookie.HttpOnly=true;o.Cookie.SameSite=SameSiteMode.Lax;
    o.Cookie.SecurePolicy=builder.Environment.IsDevelopment()?CookieSecurePolicy.SameAsRequest:CookieSecurePolicy.Always;
    o.LoginPath="/account/login";o.AccessDeniedPath="/error/403";
    o.ExpireTimeSpan=TimeSpan.FromMinutes(30);o.SlidingExpiration=true;
    o.Events.OnRedirectToAccessDenied=c=>{c.Response.StatusCode=403;return Task.CompletedTask;};
});
var keyPath=builder.Configuration["DataProtection:KeysPath"]??Path.Combine(builder.Environment.ContentRootPath,".local","keys");
Directory.CreateDirectory(keyPath);
builder.Services.AddDataProtection().SetApplicationName("ScienceLine").PersistKeysToFileSystem(new DirectoryInfo(keyPath));
builder.Services.AddControllersWithViews(o=>{
    o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());o.Filters.Add<PortalExceptionFilter>();
    o.MaxModelBindingCollectionSize=128;
});
builder.Services.AddScoped<EditorialWorkflow>();
builder.Services.AddRateLimiter(o=>{
    o.RejectionStatusCode=429;
    o.AddFixedWindowLimiter("signin",l=>{l.PermitLimit=30;l.Window=TimeSpan.FromMinutes(1);l.QueueLimit=0;l.AutoReplenishment=true;});
});
builder.WebHost.ConfigureKestrel(o=>o.Limits.MaxRequestBodySize=256*1024);
var app=builder.Build();
using(var scope=app.Services.CreateScope())
{
    var db=scope.ServiceProvider.GetRequiredService<PortalDbContext>();
    if(migrateOnly||builder.Configuration.GetValue<bool>("Database:AutoMigrate"))await db.Database.MigrateAsync();
    await SeedData.Initialize(scope.ServiceProvider,builder.Configuration);
}
if(migrateOnly)return;
if(!app.Environment.IsDevelopment()){app.UseExceptionHandler("/error/500");app.UseHsts();app.UseHttpsRedirection();}
app.Use(async(c,next)=>{
    c.Response.Headers["X-Content-Type-Options"]="nosniff";
    c.Response.Headers["X-Frame-Options"]="DENY";
    c.Response.Headers["Referrer-Policy"]="strict-origin-when-cross-origin";
    c.Response.Headers["Content-Security-Policy"]="default-src 'self'; img-src 'self' data:; style-src 'self'; script-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
    c.Response.Headers["Permissions-Policy"]="camera=(), microphone=(), geolocation=()";
    await next();
});
app.UseStatusCodePages(async context=>{
    var response=context.HttpContext.Response;
    response.ContentType="text/html; charset=utf-8";
    var message=response.StatusCode switch {400=>"Запит не пройшов перевірку. Оновіть форму та повторіть дію.",403=>"Ваш обліковий запис не має доступу до цього розділу.",404=>"Сторінку або матеріал не знайдено.",429=>"Забагато спроб. Зачекайте хвилину та спробуйте знову.",_=>"Не вдалося виконати запит."};
    await response.WriteAsync($"<!doctype html><html lang=\"uk\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>{response.StatusCode} · Наука поруч</title><link rel=\"stylesheet\" href=\"/lib/bootstrap/bootstrap.min.css\"><link rel=\"stylesheet\" href=\"/css/site.css\"><main class=\"container py-5\"><p class=\"eyebrow\">НАУКА ПОРУЧ · {response.StatusCode}</p><h1>{message}</h1><a href=\"/\" class=\"btn btn-dark mt-4\">На головну</a></main></html>");
});app.UseStaticFiles();app.UseRouting();app.UseRateLimiter();
app.UseAuthentication();app.UseAuthorization();app.MapControllers();app.MapGet("/health",()=>Results.Json(new{status="ok",service="ScienceLine"}));
app.Run();
public partial class Program { }
