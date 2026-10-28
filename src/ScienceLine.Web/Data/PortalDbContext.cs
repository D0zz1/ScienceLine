using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ScienceLine.Web.Models;

namespace ScienceLine.Web.Data;

public class PortalDbContext(DbContextOptions<PortalDbContext> options) : IdentityDbContext<PortalUser>(options)
{
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ArticleTag> ArticleTags => Set<ArticleTag>();
    public DbSet<ArticleRevision> ArticleRevisions => Set<ArticleRevision>();
    public DbSet<EditorialDecision> EditorialDecisions => Set<EditorialDecision>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<PortalUser>().Property(x=>x.DisplayName).HasMaxLength(100);
        b.Entity<Category>().Property(x=>x.Name).HasMaxLength(80).IsRequired();
        b.Entity<Category>().Property(x=>x.Slug).HasMaxLength(80).IsRequired();
        b.Entity<Category>().HasIndex(x=>x.Slug).IsUnique();
        b.Entity<Tag>().Property(x=>x.Name).HasMaxLength(60).IsRequired();
        b.Entity<Tag>().HasIndex(x=>x.Name).IsUnique();
        var a=b.Entity<Article>();
        a.Property(x=>x.Title).HasMaxLength(160).IsRequired();
        a.Property(x=>x.Summary).HasMaxLength(500).IsRequired();
        a.Property(x=>x.Body).HasMaxLength(20000).IsRequired();
        a.Property(x=>x.SourceUrl).HasMaxLength(1000).IsRequired();
        a.Property(x=>x.ImageAlt).HasMaxLength(250).IsRequired();
        a.Property(x=>x.CoverKey).HasMaxLength(20).IsRequired();
        a.Property(x=>x.Status).HasConversion<string>().HasMaxLength(30);
        a.Property(x=>x.Version).IsConcurrencyToken();
        a.Ignore(x=>x.StatusLabel);
        a.HasIndex(x=>new{x.AuthorId,x.ClientRequestId}).IsUnique();
        a.HasIndex(x=>new{x.Status,x.PublishedAtUtc});
        a.HasOne(x=>x.Author).WithMany().HasForeignKey(x=>x.AuthorId).OnDelete(DeleteBehavior.Restrict);
        a.HasOne(x=>x.Category).WithMany(x=>x.Articles).HasForeignKey(x=>x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        a.ToTable(t=>{
            t.HasCheckConstraint("CK_Article_Version","\"Version\" > 0 AND \"RevisionNumber\" > 0");
            t.HasCheckConstraint("CK_Article_Publication","\"Status\" <> 'Published' OR (length(trim(\"Title\")) > 0 AND length(trim(\"Body\")) > 0 AND \"PublishedAtUtc\" IS NOT NULL)");
        });
        b.Entity<ArticleTag>().HasKey(x=>new{x.ArticleId,x.TagId});
        b.Entity<ArticleTag>().HasOne(x=>x.Article).WithMany(x=>x.ArticleTags).HasForeignKey(x=>x.ArticleId);
        b.Entity<ArticleTag>().HasOne(x=>x.Tag).WithMany(x=>x.ArticleTags).HasForeignKey(x=>x.TagId).OnDelete(DeleteBehavior.Restrict);
        var rev=b.Entity<ArticleRevision>();
        rev.HasIndex(x=>new{x.ArticleId,x.Number}).IsUnique();
        rev.Property(x=>x.Title).HasMaxLength(160);rev.Property(x=>x.Summary).HasMaxLength(500);
        rev.Property(x=>x.Body).HasMaxLength(20000);rev.Property(x=>x.SourceUrl).HasMaxLength(1000);
        rev.HasOne(x=>x.Article).WithMany(x=>x.Revisions).HasForeignKey(x=>x.ArticleId).OnDelete(DeleteBehavior.Cascade);
        var ed=b.Entity<EditorialDecision>();ed.Property(x=>x.Kind).HasConversion<string>().HasMaxLength(30);ed.Property(x=>x.Comment).HasMaxLength(1000);
        ed.HasOne(x=>x.Article).WithMany(x=>x.Decisions).HasForeignKey(x=>x.ArticleId).OnDelete(DeleteBehavior.Cascade);
        ed.HasOne(x=>x.ArticleRevision).WithMany().HasForeignKey(x=>x.ArticleRevisionId).OnDelete(DeleteBehavior.Restrict);
        ed.HasOne(x=>x.Actor).WithMany().HasForeignKey(x=>x.ActorId).OnDelete(DeleteBehavior.Restrict);
        ed.ToTable(t=>t.HasCheckConstraint("CK_Decision_Comment","\"Kind\" <> 'Returned' OR length(trim(\"Comment\")) >= 5"));
    }
}

public class DesignTimeFactory : IDesignTimeDbContextFactory<PortalDbContext>
{
    public PortalDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<PortalDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__Portal") ?? "Host=localhost;Database=scienceline;Username=design;Password=design-only").Options);
}
