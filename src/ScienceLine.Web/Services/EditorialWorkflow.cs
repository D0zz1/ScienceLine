using Microsoft.EntityFrameworkCore;
using Npgsql;
using ScienceLine.Web.Data;
using ScienceLine.Web.Models;

namespace ScienceLine.Web.Services;

public class PortalRuleException(int status,string message) : Exception(message) { public int Status { get; }=status; }
public class EditorialWorkflow(PortalDbContext db)
{
    public static bool CanEdit(ArticleStatus status) => status is ArticleStatus.Draft or ArticleStatus.ChangesRequested;
    public async Task ValidateReferences(ArticleForm f)
    {
        if(!await db.Categories.AnyAsync(c=>c.Id==f.CategoryId))throw new PortalRuleException(400,"Обрана рубрика не існує.");
        var ids=f.TagIds.Distinct().ToArray();
        if(ids.Length>6 || await db.Tags.CountAsync(t=>ids.Contains(t.Id))!=ids.Length)throw new PortalRuleException(400,"Перевірте вибрані теги.");
    }
    public async Task<Article> Own(int id,string authorId)
    {
        return await db.Articles.Include(a=>a.Category).Include(a=>a.Author).Include(a=>a.ArticleTags).Include(a=>a.Revisions).Include(a=>a.Decisions).ThenInclude(d=>d.Actor)
            .FirstOrDefaultAsync(a=>a.Id==id && a.AuthorId==authorId) ?? throw new PortalRuleException(404,"Матеріал не знайдено.");
    }
    static ArticleRevision Snapshot(Article a) => new(){Number=a.RevisionNumber,Title=a.Title,Summary=a.Summary,Body=a.Body,SourceUrl=a.SourceUrl};
    static void Apply(Article a,ArticleForm f)
    {
        a.Title=f.Title.Trim();a.Summary=f.Summary.Trim();a.Body=f.Body.Trim();a.SourceUrl=f.SourceUrl.Trim();a.CategoryId=f.CategoryId;a.CoverKey=f.CoverKey;a.ImageAlt=f.ImageAlt.Trim();
    }
    public async Task<Article> Create(ArticleForm f,string authorId)
    {
        var existing=await db.Articles.FirstOrDefaultAsync(a=>a.AuthorId==authorId && a.ClientRequestId==f.ClientRequestId);
        if(existing!=null)return existing;
        await ValidateReferences(f);
        var a=new Article{AuthorId=authorId,ClientRequestId=f.ClientRequestId};Apply(a,f);
        a.ArticleTags=f.TagIds.Distinct().Select(id=>new ArticleTag{TagId=id}).ToList();a.Revisions.Add(Snapshot(a));db.Articles.Add(a);
        try{await db.SaveChangesAsync();}
        catch(DbUpdateException ex) when(ex.InnerException is PostgresException {SqlState:"23505"})
        {
            db.ChangeTracker.Clear();
            return await db.Articles.SingleOrDefaultAsync(x=>x.AuthorId==authorId && x.ClientRequestId==f.ClientRequestId)
                ?? throw new PortalRuleException(409,"Матеріал уже змінено. Оновіть сторінку.");
        }
        return a;
    }
    public async Task Update(int id,ArticleForm f,string authorId)
    {
        var a=await Own(id,authorId);
        if(!CanEdit(a.Status))throw new PortalRuleException(409,"Цей стан не дозволяє редагування. Дочекайтеся рішення редактора.");
        if(a.Version!=f.Version)throw new PortalRuleException(409,"Матеріал уже змінено. Оновіть сторінку перед збереженням.");
        await ValidateReferences(f);Apply(a,f);a.Version++;a.RevisionNumber++;a.UpdatedAtUtc=DateTime.UtcNow;
        var wanted=f.TagIds.Distinct().ToHashSet();
        foreach(var old in a.ArticleTags.Where(t=>!wanted.Contains(t.TagId)).ToList()){db.ArticleTags.Remove(old);a.ArticleTags.Remove(old);}
        foreach(var tagId in wanted.Except(a.ArticleTags.Select(t=>t.TagId)))a.ArticleTags.Add(new(){TagId=tagId});
        a.Revisions.Add(Snapshot(a));
        await SaveConflictSafe();
    }
    public async Task Submit(int id,int version,string authorId)
    {
        var a=await Own(id,authorId);
        if(a.Status==ArticleStatus.UnderReview)return;
        if(!CanEdit(a.Status))throw new PortalRuleException(409,"Матеріал уже опубліковано.");
        if(a.Version!=version)throw new PortalRuleException(409,"Оновіть сторінку: версія матеріалу змінилася.");
        if(string.IsNullOrWhiteSpace(a.Title)||string.IsNullOrWhiteSpace(a.Body)||string.IsNullOrWhiteSpace(a.SourceUrl))throw new PortalRuleException(400,"Додайте назву, текст і джерело фактів.");
        a.Status=ArticleStatus.UnderReview;a.Version++;a.UpdatedAtUtc=DateTime.UtcNow;
        a.Decisions.Add(new(){ArticleRevisionId=a.Revisions.Single(r=>r.Number==a.RevisionNumber).Id,ActorId=authorId,Kind=DecisionKind.Submitted,Comment="Надіслано на редакційну перевірку."});
        await SaveConflictSafe();
    }
    public async Task Decide(int id,int version,string editorId,bool publish,string? comment)
    {
        var a=await db.Articles.Include(a=>a.Revisions).FirstOrDefaultAsync(a=>a.Id==id) ?? throw new PortalRuleException(404,"Матеріал не знайдено.");
        if(publish && a.Status==ArticleStatus.Published)return;
        if(!publish && a.Status==ArticleStatus.ChangesRequested)return;
        if(a.Status!=ArticleStatus.UnderReview)throw new PortalRuleException(409,"Матеріал не перебуває на перевірці.");
        if(a.Version!=version)throw new PortalRuleException(409,"Версія змінилася. Перегляньте актуальний матеріал.");
        comment=(comment??"").Trim();
        if((!publish && comment.Length<5)||comment.Length>1000)throw new PortalRuleException(400,"Для повернення залиште зауваження від 5 до 1000 символів.");
        if(publish && (string.IsNullOrWhiteSpace(a.Title)||string.IsNullOrWhiteSpace(a.Body)))throw new PortalRuleException(400,"Не можна опублікувати порожній матеріал.");
        a.Status=publish?ArticleStatus.Published:ArticleStatus.ChangesRequested;a.Version++;a.UpdatedAtUtc=DateTime.UtcNow;
        if(publish)a.PublishedAtUtc=DateTime.UtcNow;
        db.EditorialDecisions.Add(new(){ArticleId=a.Id,ArticleRevisionId=a.Revisions.Single(r=>r.Number==a.RevisionNumber).Id,ActorId=editorId,Kind=publish?DecisionKind.Published:DecisionKind.Returned,Comment=comment});
        await SaveConflictSafe();
    }
    async Task SaveConflictSafe()
    {
        try{await db.SaveChangesAsync();}
        catch(DbUpdateConcurrencyException){throw new PortalRuleException(409,"Матеріал змінив інший користувач. Оновіть сторінку.");}
        catch(DbUpdateException ex) when(ex.InnerException is PostgresException {SqlState:"23505"}){throw new PortalRuleException(409,"Цю версію вже змінено. Оновіть сторінку.");}
    }
}

public static class TextDiff
{
    public static List<DiffLine> Compare(string oldText,string newText)
    {
        var a=oldText.Replace("\r","").Split('\n');var b=newText.Replace("\r","").Split('\n');
        if(a.Length>200 || b.Length>200)throw new ArgumentException("Максимум 200 рядків.");
        var lcs=new int[a.Length+1,b.Length+1];
        for(int i=a.Length-1;i>=0;i--)for(int j=b.Length-1;j>=0;j--)lcs[i,j]=a[i]==b[j]?1+lcs[i+1,j+1]:Math.Max(lcs[i+1,j],lcs[i,j+1]);
        List<DiffLine> output=[];int x=0,y=0;
        while(x<a.Length||y<b.Length)
        {
            if(x<a.Length&&y<b.Length&&a[x]==b[y]){output.Add(new("same",a[x++]));y++;}
            else if(x<a.Length&&(y==b.Length||lcs[x+1,y]>=lcs[x,y+1]))output.Add(new("removed",a[x++]));
            else output.Add(new("added",b[y++]));
        }
        return output;
    }
}
