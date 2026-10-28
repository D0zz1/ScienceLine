using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScienceLine.Web.Data;
using ScienceLine.Web.Models;

namespace ScienceLine.Web.Controllers;
public class PublicController(PortalDbContext db) : Controller
{
    IQueryable<Article> Published()=>db.Articles.AsNoTracking().Where(a=>a.Status==ArticleStatus.Published).Include(a=>a.Category).Include(a=>a.Author).Include(a=>a.ArticleTags).ThenInclude(t=>t.Tag);
    [HttpGet("/")]
    public async Task<IActionResult> Home()=>View(new HomeView(await Published().OrderByDescending(a=>a.PublishedAtUtc).ThenByDescending(a=>a.Id).Take(6).ToListAsync(),await db.Categories.AsNoTracking().OrderBy(x=>x.Id).ToListAsync(),await Published().CountAsync(),await Published().Select(a=>a.AuthorId).Distinct().CountAsync()));
    [HttpGet("/articles")]
    public async Task<IActionResult> Catalog(string? q,int? category,int page=1)
    {
        q=(q??"").Trim();if(q.Length>200 || page<1 || page>100000)return BadRequest();
        var query=Published();
        if(category.HasValue)query=query.Where(a=>a.CategoryId==category.Value);
        if(q.Length>0){var escaped=q.Replace("\\","\\\\").Replace("%","\\%").Replace("_","\\_");var pattern="%"+escaped+"%";query=query.Where(a=>EF.Functions.ILike(a.Title,pattern,"\\")||EF.Functions.ILike(a.Body,pattern,"\\"));}
        var total=await query.CountAsync();var articles=await query.OrderByDescending(a=>a.PublishedAtUtc).ThenByDescending(a=>a.Id).Skip((page-1)*9).Take(9).ToListAsync();
        return View(new CatalogView(articles,await db.Categories.AsNoTracking().OrderBy(x=>x.Id).ToListAsync(),q,category,page,total));
    }
    [HttpGet("/articles/{id:int}")]
    public async Task<IActionResult> Article(int id)
    {
        var a=await Published().FirstOrDefaultAsync(a=>a.Id==id);return a is null?NotFound():View(a);
    }
    [HttpGet("/about")] public IActionResult About()=>View();
    [HttpGet("/error/{code:int}")]
    public IActionResult ErrorPage(int code)
    {
        Response.StatusCode=code;
        return View("~/Views/Shared/RuleError.cshtml",new RuleError(code,code switch{404=>"Сторінку не знайдено або матеріал ще не опубліковано.",403=>"Ця дія недоступна для вашої ролі.",400=>"Запит некоректний. Оновіть сторінку та повторіть дію.",429=>"Забагато спроб. Спробуйте трохи пізніше.",_=>"Не вдалося виконати дію. Спробуйте пізніше."}));
    }
}
