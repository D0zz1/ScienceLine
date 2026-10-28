using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScienceLine.Web.Data;
using ScienceLine.Web.Models;
using ScienceLine.Web.Services;

namespace ScienceLine.Web.Controllers;
[Authorize(Roles="Author,Editor")]
public class HistoryController(PortalDbContext db) : Controller
{
    [HttpGet("/history/{id:int}")]
    public async Task<IActionResult> Index(int id,int? from,int? to)
    {
        var uid=User.FindFirstValue(ClaimTypes.NameIdentifier)!;var editor=User.IsInRole("Editor");
        var a=await db.Articles.AsNoTracking().Include(x=>x.Revisions).FirstOrDefaultAsync(x=>x.Id==id && (editor||x.AuthorId==uid));
        if(a is null)return NotFound();
        var versions=a.Revisions.OrderBy(x=>x.Number).ToList();
        var after=versions.FirstOrDefault(x=>x.Number==(to??a.RevisionNumber));
        var before=versions.FirstOrDefault(x=>x.Number==(from??Math.Max(1,a.RevisionNumber-1)));
        if(after is null||before is null)return NotFound();
        return View(new HistoryView(a,before,after,TextDiff.Compare(before.Body,after.Body)));
    }
}
