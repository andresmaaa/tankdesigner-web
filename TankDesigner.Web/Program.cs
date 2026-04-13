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

// Servicio para envío de emails (invitaciones, etc.)
builder.Services.AddScoped<EmailService>();

// Configuración de Blazor Server (componentes interactivos)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Permite usar estado de autenticación en Blazor
builder.Services.AddCascadingAuthenticationState();

// Permite acceder al HttpContext desde servicios
builder.Services.AddHttpContextAccessor();

// Obtiene la cadena de conexión desde configuración (Railway en tu caso)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("No se ha encontrado la cadena de conexión 'DefaultConnection'.");

// Registro del DbContext principal con PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Factory para crear DbContext manualmente en servicios scoped
builder.Services.AddDbContextFactory<ApplicationDbContext>(
    options => options.UseNpgsql(connectionString),
    ServiceLifetime.Scoped);

// Configuración de Identity (usuarios, roles, login, etc.)
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        // Reglas de contraseña
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 6;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>() // guarda en PostgreSQL
    .AddDefaultTokenProviders(); // tokens (reset password, etc.)

// Ruta donde se guardan las claves de DataProtection (IMPORTANTE para sesiones)
var dataProtectionPath = Environment.GetEnvironmentVariable("DATA_PROTECTION_KEYS_PATH");

// Si no hay variable de entorno, usa carpeta local
if (string.IsNullOrWhiteSpace(dataProtectionPath))
{
    dataProtectionPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys");
}

// Asegura que la carpeta existe
Directory.CreateDirectory(dataProtectionPath);

// Configuración de DataProtection (clave para que NO se cierre sesión)
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetApplicationName("TankDesigner");

// Configuración de la cookie de autenticación
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "TankDesigner.Auth";

    // Rutas de login/logout
    options.LoginPath = "/login";
    options.LogoutPath = "/login";
    options.AccessDeniedPath = "/login";

    // Duración de la sesión (30 días si rememberMe)
    options.ExpireTimeSpan = TimeSpan.FromDays(30);

    // Renovación automática mientras navegas
    options.SlidingExpiration = true;

    // Seguridad de la cookie
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;

    // IMPORTANTE: en Railway puede afectar si no es HTTPS
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// Autorización (roles, políticas)
builder.Services.AddAuthorizationCore();
builder.Services.AddAuthorization();

// Registro de servicios propios de la aplicación
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

// Configuración del servicio de IA (Gemini)
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection("Gemini"));
builder.Services.AddHttpClient<AiEngineeringService>();

// Licencia de QuestPDF
QuestPDF.Settings.License = LicenseType.Community;

var app = builder.Build();

// Railway usa puerto dinámico, esto lo adapta automáticamente
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    app.Urls.Add($"http://0.0.0.0:{port}");
}

// Configuración de errores en producción
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Middleware básicos
app.UseHttpsRedirection();
app.UseAuthentication(); // MUY IMPORTANTE para login
app.UseAuthorization();
app.UseAntiforgery();

// Endpoint POST para login manual (formulario)
app.MapPost("/auth/login", async (
    HttpContext httpContext,
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    InvitacionesService invitacionesService) =>
{
    var form = await httpContext.Request.ReadFormAsync();

    // Datos del formulario
    var email = form["email"].ToString().Trim();
    var password = form["password"].ToString();
    var returnUrl = form["returnUrl"].ToString();

    // Detecta si se marcó "recordarme"
    bool rememberMe = false;
    var rememberRaw = form["rememberMe"].ToString();
    if (!string.IsNullOrWhiteSpace(rememberRaw))
    {
        rememberMe = rememberRaw.Equals("true", StringComparison.OrdinalIgnoreCase)
                     || rememberRaw.Equals("on", StringComparison.OrdinalIgnoreCase);
    }

    // URL de redirección por defecto
    if (string.IsNullOrWhiteSpace(returnUrl))
        returnUrl = "/mis-proyectos";

    // Seguridad: evita redirecciones externas
    if (!Uri.IsWellFormedUriString(returnUrl, UriKind.Relative))
        returnUrl = "/mis-proyectos";

    // Login con Identity
    var result = await signInManager.PasswordSignInAsync(
        email,
        password,
        isPersistent: rememberMe, // esto activa cookie persistente
        lockoutOnFailure: false);

    if (result.Succeeded)
    {
        // Aplica invitaciones pendientes (ej: convertir en admin)
        var usuario = await userManager.FindByEmailAsync(email);
        if (usuario is not null)
            await invitacionesService.AplicarInvitacionPendienteAsync(usuario);

        return Results.Redirect(returnUrl);
    }

    // Si falla, vuelve al login con error
    return Results.Redirect($"/login?error=1&returnUrl={Uri.EscapeDataString(returnUrl)}");
});

// Endpoint POST para logout
app.MapPost("/auth/logout", async (
    SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/login");
});

// Archivos estáticos
app.MapStaticAssets();

// Configuración de Blazor
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}
// Seed de roles y SuperAdmin al arrancar
await IdentitySeedData.InicializarAsync(app.Services, app.Configuration);

// Arranque de la aplicación
app.Run();