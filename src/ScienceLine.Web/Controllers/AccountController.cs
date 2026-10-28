using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ScienceLine.Web.Models;

namespace ScienceLine.Web.Controllers;
public class AccountController(UserManager<PortalUser> users,SignInManager<PortalUser> signin) : Controller
{
    [HttpGet("/account/login")]
    public IActionResult Login(string? returnUrl=null)=>View(new LoginForm{ReturnUrl=returnUrl});
    [HttpPost("/account/login"),EnableRateLimiting("signin")]
    public async Task<IActionResult> Login(LoginForm f)
    {
        if(ModelState.IsValid)
        {
            var result=await signin.PasswordSignInAsync(f.Email,f.Password,false,true);
            if(result.Succeeded)return Url.IsLocalUrl(f.ReturnUrl)?LocalRedirect(f.ReturnUrl!):Redirect("/");
            ModelState.AddModelError("",result.IsLockedOut?"Забагато невдалих спроб. Повторіть через 5 хвилин.":"Неправильна пошта або пароль.");
        }
        Response.StatusCode=400;return View(f);
    }
    [HttpGet("/account/register")]
    public IActionResult Register()=>View(new RegisterForm());
    [HttpPost("/account/register"),EnableRateLimiting("signin")]
    public async Task<IActionResult> Register(RegisterForm f)
    {
        if(ModelState.IsValid)
        {
            var user=new PortalUser{UserName=f.Email,Email=f.Email,DisplayName=f.DisplayName.Trim()};
            var result=await users.CreateAsync(user,f.Password);
            if(result.Succeeded){await users.AddToRoleAsync(user,"Author");await signin.SignInAsync(user,false);return Redirect("/author");}
            foreach(var error in result.Errors)ModelState.AddModelError("","Не вдалося створити обліковий запис: перевірте пошту та складність пароля.");
        }
        Response.StatusCode=400;return View(f);
    }
    [HttpPost("/account/logout")]
    public async Task<IActionResult> Logout(){await signin.SignOutAsync();return Redirect("/");}
}
