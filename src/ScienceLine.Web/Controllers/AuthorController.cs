using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScienceLine.Web.Data;
using ScienceLine.Web.Models;
using ScienceLine.Web.Services;

namespace ScienceLine.Web.Controllers;
[Authorize(Roles="Author")]
public class AuthorController(PortalDbContext db,EditorialWorkflow workflow) : Controller
{
    string UserId=>User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    async Task FillChoices(){ViewBag.Categories=await db.Categories.AsNoTracking().OrderBy(x=>x.Id).ToListAsync();ViewBag.Tags=await db.Tags.AsNoTracking().OrderBy(x=>x.Id).ToListAsync();}
    [HttpGet("/author")]
    public async Task<IActionResult> Index()=>View(await db.Articles.AsNoTracking().Include(a=>a.Category).Where(a=>a.AuthorId==UserId).OrderByDescending(a=>a.UpdatedAtUtc).ToListAsync());
    [HttpGet("/author/create")]
    public async Task<IActionResult> Create(){await FillChoices();return View("Form",new ArticleForm());}
    [HttpPost("/author/create")]
    public async Task<IActionResult> Create(ArticleForm f)
    {
        if(!ModelState.IsValid){await FillChoices();Response.StatusCode=400;return View("Form",f);}
        var a=await workflow.Create(f,UserId);TempData["Success"]="Чернетку збережено.";return RedirectToAction(nameof(Details),new{id=a.Id});
    }
    [HttpGet("/author/{id:int}")]
    public async Task<IActionResult> Details(int id)=>View(await workflow.Own(id,UserId));
    [HttpGet("/author/{id:int}/edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var a=await workflow.Own(id,UserId);if(!EditorialWorkflow.CanEdit(a.Status))throw new PortalRuleException(409,"Цей матеріал зараз не можна редагувати.");
        await FillChoices();return View("Form",ArticleForm.From(a));
    }
    [HttpPost("/author/{id:int}/edit")]
    public async Task<IActionResult> Edit(int id,ArticleForm f)
    {
        await workflow.Own(id,UserId);
        if(!ModelState.IsValid){await FillChoices();Response.StatusCode=400;return View("Form",f);}
        await workflow.Update(id,f,UserId);TempData["Success"]="Зміни збережено як нову версію.";return RedirectToAction(nameof(Details),new{id});
    }
    [HttpPost("/author/{id:int}/submit")]
    public async Task<IActionResult> Submit(int id,int version){await workflow.Submit(id,version,UserId);TempData["Success"]="Матеріал надіслано редактору.";return RedirectToAction(nameof(Details),new{id});}
}
