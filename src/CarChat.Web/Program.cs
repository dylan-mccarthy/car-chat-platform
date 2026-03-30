using CarChat.Core.Extensions;
using CarChat.Web.Components;
using CarChat.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCarChatCore(builder.Configuration);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<UserSessionService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opts =>
    {
        opts.LoginPath = "/login";
        opts.LogoutPath = "/authentication/logout";
        opts.ExpireTimeSpan = TimeSpan.FromDays(30);
        opts.SlidingExpiration = true;
    })
    .AddGoogle(opts =>
    {
        opts.ClientId = builder.Configuration["OAuth:Google:ClientId"] ?? "placeholder";
        opts.ClientSecret = builder.Configuration["OAuth:Google:ClientSecret"] ?? "placeholder";
        opts.CallbackPath = "/signin-google";
        opts.Events.OnCreatingTicket = ctx =>
        {
            ctx.Identity?.AddClaim(new Claim("provider", "google"));
            return Task.CompletedTask;
        };
    })
    .AddGitHub(opts =>
    {
        opts.ClientId = builder.Configuration["OAuth:GitHub:ClientId"] ?? "placeholder";
        opts.ClientSecret = builder.Configuration["OAuth:GitHub:ClientSecret"] ?? "placeholder";
        opts.CallbackPath = "/signin-github";
        opts.Scope.Add("user:email");
        opts.Events.OnCreatingTicket = ctx =>
        {
            ctx.Identity?.AddClaim(new Claim("provider", "github"));
            return Task.CompletedTask;
        };
    })
    .AddMicrosoftAccount(opts =>
    {
        opts.ClientId = builder.Configuration["OAuth:Microsoft:ClientId"] ?? "placeholder";
        opts.ClientSecret = builder.Configuration["OAuth:Microsoft:ClientSecret"] ?? "placeholder";
        opts.CallbackPath = "/signin-microsoft";
        opts.Events.OnCreatingTicket = ctx =>
        {
            ctx.Identity?.AddClaim(new Claim("provider", "microsoft"));
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CarChat.Core.Data.AppDbContext>();
    db.Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

// OAuth redirect endpoints
app.MapGet("/auth/google", () => Results.Challenge(
    new AuthenticationProperties { RedirectUri = "/dashboard" }, ["Google"]));

app.MapGet("/auth/github", () => Results.Challenge(
    new AuthenticationProperties { RedirectUri = "/dashboard" }, ["GitHub"]));

app.MapGet("/auth/microsoft", () => Results.Challenge(
    new AuthenticationProperties { RedirectUri = "/dashboard" }, ["MicrosoftAccount"]));

app.MapGet("/authentication/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
