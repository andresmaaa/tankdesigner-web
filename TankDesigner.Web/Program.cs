using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using System.IO;
using TankDesigner.Core.Services;
using TankDesigner.Infrastructure.Services;
using TankDesigner.Web.Components;
using TankDesigner.Web.Data;
using TankDesigner.Web.Services;
using TankDesigner.Web.Services.Ai;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<EmailService>();

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

var dataProtectionPath = Environment.GetEnvironmentVariable("DATA_PROTECTION_KEYS_PATH");

if (string.IsNullOrWhiteSpace(dataProtectionPath))
{
    dataProtectionPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys");
}

Directory.CreateDirectory(dataProtectionPath);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetApplicationName("TankDesigner");

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "TankDesigner.Auth";
    options.LoginPath = "/login";
    options.LogoutPath = "/login";
    options.AccessDeniedPath = "/login";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddAuthorizationCore();
builder.Services.AddAuthorization();

builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<InvitacionesService>();
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

QuestPDF.Settings.License = LicenseType.Community;

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
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    InvitacionesService invitacionesService) =>
{
    var form = await httpContext.Request.ReadFormAsync();

    var email = form["email"].ToString().Trim();
    var password = form["password"].ToString();
    var returnUrl = form["returnUrl"].ToString();

    bool rememberMe = false;
    var rememberRaw = form["rememberMe"].ToString();
    if (!string.IsNullOrWhiteSpace(rememberRaw))
    {
        rememberMe = rememberRaw.Equals("true", StringComparison.OrdinalIgnoreCase)
                     || rememberRaw.Equals("on", StringComparison.OrdinalIgnoreCase);
    }

    if (string.IsNullOrWhiteSpace(returnUrl))
        returnUrl = "/mis-proyectos";

    if (!Uri.IsWellFormedUriString(returnUrl, UriKind.Relative))
        returnUrl = "/mis-proyectos";

    var result = await signInManager.PasswordSignInAsync(
        email,
        password,
        isPersistent: rememberMe,
        lockoutOnFailure: false);

    if (result.Succeeded)
    {
        var usuario = await userManager.FindByEmailAsync(email);
        if (usuario is not null)
            await invitacionesService.AplicarInvitacionPendienteAsync(usuario);

        return Results.Redirect(returnUrl);
    }

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

await IdentitySeedData.InicializarAsync(app.Services, app.Configuration);

app.Run();