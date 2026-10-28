using System.ComponentModel.DataAnnotations;

namespace ScienceLine.Web.Models;

public class ArticleForm : IValidatableObject
{
    public int Id { get; set; }
    public int Version { get; set; }
    public Guid ClientRequestId { get; set; } = Guid.NewGuid();
    [Required(ErrorMessage="Введіть назву."),StringLength(160,MinimumLength=5,ErrorMessage="Назва має містити від 5 до 160 символів.")]
    [Display(Name="Назва матеріалу")] public string Title { get; set; } = "";
    [Required(ErrorMessage="Додайте короткий опис."),StringLength(500,MinimumLength=20,ErrorMessage="Короткий опис: від 20 до 500 символів.")]
    [Display(Name="Короткий опис")] public string Summary { get; set; } = "";
    [Required(ErrorMessage="Додайте основний текст."),StringLength(20000,MinimumLength=50,ErrorMessage="Основний текст: від 50 до 20 000 символів.")]
    [Display(Name="Основний текст")] public string Body { get; set; } = "";
    [Required(ErrorMessage="Оберіть рубрику."),Range(1,int.MaxValue,ErrorMessage="Оберіть рубрику.")]
    [Display(Name="Рубрика")] public int CategoryId { get; set; }
    public int[] TagIds { get; set; } = [];
    [Required(ErrorMessage="Вкажіть походження фактів."),StringLength(1000)]
    [Display(Name="Посилання на джерело фактів")] public string SourceUrl { get; set; } = "";
    [Display(Name="Ілюстрація")] public string CoverKey { get; set; } = "orbit";
    [Required(ErrorMessage="Опишіть ілюстрацію для читача."),StringLength(250,MinimumLength=5)]
    [Display(Name="Альтернативний текст ілюстрації")] public string ImageAlt { get; set; } = "";
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if(!Uri.TryCreate(SourceUrl,UriKind.Absolute,out var uri) || (uri.Scheme!="https" && uri.Scheme!="http"))
            yield return new("Вкажіть повне посилання http або https.",[nameof(SourceUrl)]);
        if(!new[]{"orbit","earth","lab","digital"}.Contains(CoverKey))yield return new("Оберіть ілюстрацію з переліку.",[nameof(CoverKey)]);
        if((Body??"").Split('\n').Length>200)yield return new("Не більше 200 абзаців в одному матеріалі.",[nameof(Body)]);
        if(ClientRequestId==Guid.Empty)yield return new("Оновіть форму й повторіть спробу.");
    }
    public static ArticleForm From(Article a) => new(){Id=a.Id,Version=a.Version,ClientRequestId=a.ClientRequestId,Title=a.Title,Summary=a.Summary,Body=a.Body,CategoryId=a.CategoryId,SourceUrl=a.SourceUrl,CoverKey=a.CoverKey,ImageAlt=a.ImageAlt,TagIds=a.ArticleTags.Select(t=>t.TagId).ToArray()};
}
public class LoginForm
{
    [Required,EmailAddress,Display(Name="Електронна пошта")] public string Email { get; set; } = "";
    [Required,DataType(DataType.Password),Display(Name="Пароль")] public string Password { get; set; } = "";
    public string? ReturnUrl { get; set; }
}
public class RegisterForm : LoginForm
{
    [Required,StringLength(100,MinimumLength=2),Display(Name="Ім’я для публікацій")] public string DisplayName { get; set; } = "";
    [Required,Compare(nameof(Password),ErrorMessage="Паролі не збігаються."),DataType(DataType.Password),Display(Name="Повторіть пароль")] public string ConfirmPassword { get; set; } = "";
}
public record CatalogView(List<Article> Articles,List<Category> Categories,string Query,int? CategoryId,int Page,int Total);
public record HomeView(List<Article> Articles,List<Category> Categories,int PublishedCount,int AuthorCount);
public record DiffLine(string Kind,string Text);
public record HistoryView(Article Article,ArticleRevision Before,ArticleRevision After,List<DiffLine> Lines);
public record RuleError(int Status,string Message);
