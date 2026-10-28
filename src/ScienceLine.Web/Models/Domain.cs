using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace ScienceLine.Web.Models;

public class PortalUser : IdentityUser
{
    [MaxLength(100)] public string DisplayName { get; set; } = "Автор";
}
public enum ArticleStatus { Draft, UnderReview, ChangesRequested, Published }
public enum DecisionKind { Submitted, Returned, Published }
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public List<Article> Articles { get; set; } = [];
}
public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<ArticleTag> ArticleTags { get; set; } = [];
}
public class ArticleTag
{
    public int ArticleId { get; set; }
    public Article Article { get; set; } = null!;
    public int TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}
public class Article
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Body { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string CoverKey { get; set; } = "orbit";
    public string ImageAlt { get; set; } = "";
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public string AuthorId { get; set; } = "";
    public PortalUser Author { get; set; } = null!;
    public Guid ClientRequestId { get; set; }
    public ArticleStatus Status { get; set; } = ArticleStatus.Draft;
    public int Version { get; set; } = 1;
    public int RevisionNumber { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAtUtc { get; set; }
    public List<ArticleTag> ArticleTags { get; set; } = [];
    public List<ArticleRevision> Revisions { get; set; } = [];
    public List<EditorialDecision> Decisions { get; set; } = [];
    public string StatusLabel => Status switch {
        ArticleStatus.Draft => "Чернетка", ArticleStatus.UnderReview => "На перевірці",
        ArticleStatus.ChangesRequested => "На доопрацюванні", _ => "Опубліковано" };
}
public class ArticleRevision
{
    public int Id { get; set; }
    public int ArticleId { get; set; }
    public Article Article { get; set; } = null!;
    public int Number { get; set; }
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Body { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
public class EditorialDecision
{
    public int Id { get; set; }
    public int ArticleId { get; set; }
    public Article Article { get; set; } = null!;
    public int ArticleRevisionId { get; set; }
    public ArticleRevision ArticleRevision { get; set; } = null!;
    public string ActorId { get; set; } = "";
    public PortalUser Actor { get; set; } = null!;
    public DecisionKind Kind { get; set; }
    public string Comment { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
