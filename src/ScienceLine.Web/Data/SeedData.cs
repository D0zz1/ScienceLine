using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ScienceLine.Web.Models;

namespace ScienceLine.Web.Data;
public static class SeedData
{
    public static async Task Initialize(IServiceProvider services,IConfiguration cfg)
    {
        var roles=services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach(var role in new[]{"Reader","Author","Editor"})if(!await roles.RoleExistsAsync(role))await roles.CreateAsync(new IdentityRole(role));
        if(!cfg.GetValue<bool>("Demo:Enabled"))return;
        var password=cfg["Demo:Password"];
        if(string.IsNullOrWhiteSpace(password)||password.Length<12)throw new InvalidOperationException("Demo__Password must be set to a generated local password (12+ characters).");
        var users=services.GetRequiredService<UserManager<PortalUser>>();
        foreach(var (email,name,role) in new[]{("author@scienceline.example","Олена Дослідниця","Author"),("second@scienceline.example","Тарас Автор","Author"),("editor@scienceline.example","Редакція порталу","Editor"),("reader@scienceline.example","Читач порталу","Reader")})
        {
            var u=await users.FindByEmailAsync(email);
            if(u is null){u=new PortalUser{Email=email,UserName=email,DisplayName=name,EmailConfirmed=true};var created=await users.CreateAsync(u,password);if(!created.Succeeded)throw new InvalidOperationException("Demo account initialization failed: "+string.Join(", ",created.Errors.Select(e=>e.Code)));}
            if(!await users.IsInRoleAsync(u,role))await users.AddToRoleAsync(u,role);
        }
        var db=services.GetRequiredService<PortalDbContext>();
        if(await db.Categories.AnyAsync())return;
        var categories=new[]{new Category{Name="Космос",Slug="space"},new Category{Name="Планета",Slug="planet"},new Category{Name="Технології",Slug="technology"},new Category{Name="Дослідження",Slug="research"}};
        var tags=new[]{"Спостереження","Дані","Клімат","Енергія","Метод","Генетика"}.Select(x=>new Tag{Name=x}).ToArray();
        db.Categories.AddRange(categories);db.Tags.AddRange(tags);await db.SaveChangesAsync();
        var author=await users.FindByEmailAsync("author@scienceline.example");
        var second=await users.FindByEmailAsync("second@scienceline.example");
        var articles=new[]{
            ("Що показує телескоп James Webb","Інфрачервоні спостереження відкривають деталі, які складно побачити у видимому світлі. Розбираємося, як читати космічні зображення.","Телескоп James Webb проводить спостереження переважно в інфрачервоному діапазоні. Це допомагає вивчати далекі галактики, зорі та планетні системи.\n\nКольори опублікованих зображень можуть відображати різні діапазони вимірювання. Для розуміння знімка важливо прочитати пояснення дослідників і підписи, а не робити висновок лише за кольором.\n\nЦей навчальний матеріал показує, як редакція пов’язує пояснення з першоджерелом. Докладні відомості про місію доступні на сторінці NASA.",0,"orbit","Умовна ілюстрація орбіт і зірок навколо телескопа","https://science.nasa.gov/mission/webb/"),
            ("Клімат у даних: навіщо потрібні довгі ряди спостережень","Окремий спекотний день і довгострокова зміна клімату відповідають на різні питання. Пояснюємо роль спостережень.","Погода описує поточний стан атмосфери, тоді як для розмови про клімат потрібні спостереження за тривалі періоди. Порівняння окремих днів не замінює аналізу довгих рядів даних.\n\nСупутникові й наземні спостереження доповнюють одне одного. Разом із результатом важливо бачити спосіб вимірювання та його обмеження.\n\nДля перевірки фактів редакція посилається на матеріали NASA про кліматичні зміни. Наведений текст є навчальним поясненням, а не прогнозом для конкретного міста.",1,"earth","Умовна ілюстрація Землі та шарів атмосфери","https://science.nasa.gov/climate-change/"),
            ("Квантові обчислення без обіцянок магії","Кубіт, вимірювання та алгоритм: чому квантовий процесор не є універсальним прискорювачем усіх програм.","Квантові обчислення використовують властивості квантових систем для подання й оброблення інформації. Базова одиниця такого подання — кубіт.\n\nКористь залежить від конкретного алгоритму й задачі. Наявність квантового процесора не означає автоматичного прискорення будь-якої програми.\n\nЧитаючи повідомлення про технологію, варто запитати, що порівнювали, за яких умов та як вимірювали результат. Огляд IBM пояснює основні поняття; він зазначений як джерело цього навчального матеріалу.",2,"digital","Абстрактна схема з’єднаних обчислювальних вузлів","https://www.ibm.com/think/topics/quantum-computing"),
            ("Як прочитати послідовність ДНК","Послідовність нуклеотидів є даними для дослідження. Що означає її визначення та чому результат потребує інтерпретації.","Секвенування ДНК визначає послідовність нуклеотидів у досліджуваному фрагменті. Отриманий запис стає початком подальшого аналізу.\n\nМіж вимірюванням та науковим висновком є перевірка якості, зіставлення й інтерпретація. Один результат не пояснює автоматично всі властивості організму.\n\nМатеріал призначений для ознайомлення з методом і не містить медичних порад. Пояснення процесу та термінів наведено в першоджерелі Національного інституту дослідження геному людини.",3,"lab","Умовна графічна ілюстрація структури молекули ДНК","https://www.genome.gov/about-genomics/fact-sheets/DNA-Sequencing-Fact-Sheet"),
            ("Сонячна енергія: від світла до електричного струму","Фотоелектричні елементи перетворюють енергію світла на електричну. Розглядаємо принцип без перебільшених обіцянок.","Фотоелектричні елементи використовують напівпровідникові матеріали для перетворення енергії світла на електричну. Сонячна панель об’єднує такі елементи в систему.\n\nПрактичний результат залежить від освітлення та умов роботи обладнання. Тому показник потужності слід читати разом з описом умов вимірювання.\n\nНавчальна стаття знайомить із принципом перетворення енергії. Докладніше його пояснює Міністерство енергетики США на сторінці, зазначеній як джерело.",2,"digital","Умовна ілюстрація світлових променів і модулів сонячної панелі","https://www.energy.gov/eere/solar/how-does-solar-work"),
            ("Чому науковому поясненню потрібне джерело","Назва установи, опис методу та доступний документ допомагають перевірити, як отримано твердження.","Першоджерело дає читачеві змогу перевірити контекст твердження. У матеріалі про космічну місію це може бути сторінка наукової організації з описом інструмента та результатів спостереження.\n\nРедакційне пояснення має розмежовувати виміряні дані, припущення та власне узагальнення. Підпис до ілюстрації також має значення: схема і фотографія показують різні речі.\n\nУ цьому демонстраційному матеріалі посилання веде до опису місії NASA. Такий підхід використовується й у формі підготовки нових статей порталу.",3,"lab","Абстрактна ілюстрація дослідження та перевірки наукових даних","https://science.nasa.gov/mission/webb/")
        };
        for(int i=0;i<articles.Length;i++)
        {
            var (title,summary,body,cat,cover,alt,source)=articles[i];
            var a=new Article{Title=title,Summary=summary,Body=body,CategoryId=categories[cat].Id,AuthorId=author!.Id,CoverKey=cover,ImageAlt=alt,SourceUrl=source,ClientRequestId=Guid.NewGuid(),Status=ArticleStatus.Published,PublishedAtUtc=DateTime.UtcNow.AddMinutes(-i)};
            a.ArticleTags.Add(new(){TagId=tags[i%tags.Length].Id});a.Revisions.Add(new(){Number=1,Title=title,Summary=summary,Body=body,SourceUrl=source});db.Articles.Add(a);
        }
        foreach(var (owner,title,status) in new[]{(author!,"Чернетка про спостереження за Місяцем",ArticleStatus.Draft),(second!,"Матеріал другого автора для перевірки доступу",ArticleStatus.Draft),(author!,"Як відрізнити спостереження від припущення",ArticleStatus.UnderReview)})
        {
            var a=new Article{AuthorId=owner.Id,CategoryId=categories[0].Id,Title=title,Summary="Навчальний матеріал для демонстрації редакційного процесу та обмеження доступу.",Body="Цей текст збережений у базі даних як непублічний матеріал. Автор може працювати зі своєю чернеткою, а редактор — переглянути матеріал, надісланий на перевірку. У публічному каталозі цей запис з’явиться лише після редакційної публікації.",SourceUrl="https://science.nasa.gov/mission/webb/",ImageAlt="Умовна ілюстрація орбіт і наукових спостережень",ClientRequestId=Guid.NewGuid(),Status=status};
            a.Revisions.Add(new(){Number=1,Title=a.Title,Summary=a.Summary,Body=a.Body,SourceUrl=a.SourceUrl});db.Articles.Add(a);
        }
        await db.SaveChangesAsync();
    }
}
