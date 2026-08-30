using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using CUIDAPP_ADMINISTRATIVO.Components;
using CUIDAPP_ADMINISTRATIVO.Services;

var builder = WebApplication.CreateBuilder(args);
// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient<AdminAuthService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"]!);
});
builder.Services.AddHttpClient<PagoAdminApiService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"]!);
});
builder.Services.AddHttpClient<TicketAdminApiService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"]!);
});
builder.Services.AddHttpClient<CuidadorAdminApiService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"]!);
});
builder.Services.AddHttpClient<ClienteAdminApiService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"]!);
});
builder.Services.AddHttpClient<AdminAccountApiService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"]!);
});

// Autenticación por cookie: la sesión vive en el navegador (no en el circuito de
// Blazor Server), así que sobrevive a un refresh completo de la página. El JWT que
// devuelve CUIDAPP_API en auth/login es lo que prueba la identidad; aquí se guarda
// como claim dentro de la cookie para no tener que volver a llamar a la API.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "CuidappAdminAuth";
        options.LoginPath = "/";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnet/openapi
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

const int RolAdmin = 1;

// Único punto donde se completa el login: recibe el código de un solo uso generado
// por Login.razor tras validar credenciales + rol contra CUIDAPP_API, arma la cookie
// de sesión y redirige al dashboard. Tiene que ser un endpoint HTTP normal (no un
// componente de Blazor) porque solo así se puede escribir el header Set-Cookie.
app.MapGet("/account/login-complete", async (HttpContext http, string code) =>
{
    if (!PendingLoginStore.TryTomar(code, out var data) || data == null || data.RolId != RolAdmin)
        return Results.Redirect("/");

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, data.UserId.ToString()),
        new(ClaimTypes.Email, data.Email),
        new(ClaimTypes.Name, string.IsNullOrWhiteSpace(data.NombreCompleto) ? data.Email : data.NombreCompleto),
        new(ClaimTypes.Role, data.RolId.ToString()),
    };
    if (!string.IsNullOrWhiteSpace(data.FotoUrl))
        claims.Add(new Claim("foto_url", data.FotoUrl));

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    var principal = new ClaimsPrincipal(identity);

    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
    {
        IsPersistent = true,
        ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
    });

    return Results.Redirect("/dashboard");
});

app.MapGet("/account/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
