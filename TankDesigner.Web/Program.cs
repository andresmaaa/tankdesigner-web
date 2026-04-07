using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TankDesigner.Core.Services;
using TankDesigner.Infrastructure.Services;
using TankDesigner.Web.Components;
using TankDesigner.Web.Data;
using TankDesigner.Web.Services;
using TankDesigner.Web.Services.Ai;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("No se ha encontrado la cadena de conexión 'DefaultConnection'.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddDbContextFactory<ApplicationDbContext>(
    options => options.UseNpgsql(connectionString),
    ServiceLifetime.Scoped);

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 6;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthorizationCore();
builder.Services.AddAuthorization();

builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<ProyectoPersistenciaService>();
builder.Services.AddScoped<ProyectoState>();
builder.Services.AddScoped<CalculoWebService>();
builder.Services.AddScoped<CatalogoJsonService>();
builder.Services.AddScoped<OpcionesFormularioService>();
builder.Services.AddScoped<NormativaCargasUiService>();
builder.Services.AddScoped<FormularioValidacionService>();
builder.Services.AddScoped<CalculoGeometriaService>();
builder.Services.AddScoped<InformeHtmlService>();
builder.Services.AddScoped<PdfRenderService>();

builder.Services.Configure<AiOptions>(builder.Configuration.GetSection("Gemini"));
builder.Services.AddHttpClient<AiEngineeringService>();

var app = builder.Build();

var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    app.Urls.Add($"http://0.0.0.0:{port}");
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapPost("/auth/login", async (
    HttpContext httpContext,
    SignInManager<ApplicationUser> signInManager) =>
{
    var form = await httpContext.Request.ReadFormAsync();

    var email = form["email"].ToString();
    var password = form["password"].ToString();
    var rememberMe = form["rememberMe"] == "on";
    var returnUrl = form["returnUrl"].ToString();

    if (string.IsNullOrWhiteSpace(returnUrl))
        returnUrl = "/mis-proyectos";

    var result = await signInManager.PasswordSignInAsync(
        email,
        password,
        rememberMe,
        lockoutOnFailure: false);

    if (result.Succeeded)
        return Results.Redirect(returnUrl);

    return Results.Redirect($"/login?error=1&returnUrl={Uri.EscapeDataString(returnUrl)}");
});

app.MapPost("/auth/logout", async (
    SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/login");
});

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Inicializo roles y convierto en SuperAdmin al usuario indicado en appsettings
await IdentitySeedData.InicializarAsync(app.Services, app.Configuration);

app.Run();