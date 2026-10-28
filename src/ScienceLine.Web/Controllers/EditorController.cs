using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScienceLine.Web.Data;
using ScienceLine.Web.Models;
using ScienceLine.Web.Services;

namespace ScienceLine.Web.Controllers;
[Authorize(Roles="Editor")]
public class EditorController(PortalDbContext db,EditorialWorkflow workflow) : Controller
{
    [HttpGet("/editor")]
    public async Task<IActionResult> Index(string? state)
    {
        var query=db.Articles.AsNoTracking().Include(a=>a.Author).Include(a=>a.Category).AsQueryable();
        if(Enum.TryParse<ArticleStatus>(state,out var filter))query=query.Where(a=>a.Status==filter);
        else query=query.Where(a=>a.Status==ArticleStatus.UnderReview);
        return View(await query.OrderBy(a=>a.UpdatedAtUtc).ToListAsync());
    }
    [HttpGet("/editor/{id:int}")]
    public async Task<IActionResult> Review(int id)
    {
        var a=await db.Articles.AsNoTracking().Include(a=>a.Author).Include(a=>a.Category).Include(a=>a.Revisions).Include(a=>a.Decisions).ThenInclude(x=>x.Actor).FirstOrDefaultAsync(a=>a.Id==id);
        return a is null?NotFound():View(a);
    }
    [HttpPost("/editor/{id:int}/publish")]
    public async Task<IActionResult> Publish(int id,int version){await workflow.Decide(id,version,User.FindFirstValue(ClaimTypes.NameIdentifier)!,true,null);TempData["Success"]="Матеріал опубліковано й додано до каталогу.";return RedirectToAction(nameof(Review),new{id});}
    [HttpPost("/editor/{id:int}/return")]
    public async Task<IActionResult> Return(int id,int version,string? comment){await workflow.Decide(id,version,User.FindFirstValue(ClaimTypes.NameIdentifier)!,false,comment);TempData["Success"]="Матеріал повернуто автору із зауваженням.";return RedirectToAction(nameof(Review),new{id});}
}
