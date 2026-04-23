using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.RateLimiting;
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

// Configuración para Railway / proxy inverso
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Obtiene la cadena de conexión desde configuración
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
        // Política de contraseña segura y profesional
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 8;
        options.Password.RequiredUniqueChars = 1;

        // Bloqueo por intentos fallidos
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        // De momento sin confirmación de email obligatoria
        // hasta que el sistema de correo esté totalmente cerrado
        options.SignIn.RequireConfirmedEmail = false;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();
    

// Ruta donde se guardan las claves de DataProtection
var dataProtectionPath = Environment.GetEnvironmentVariable("DATA_PROTECTION_KEYS_PATH");

// Si no hay variable de entorno, usa carpeta local
if (string.IsNullOrWhiteSpace(dataProtectionPath))
{
    dataProtectionPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys");
}

// Asegura que la carpeta existe
Directory.CreateDirectory(dataProtectionPath);

// Configuración de DataProtection
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

    // Duración de la sesión
    options.ExpireTimeSpan = TimeSpan.FromDays(30);

    // Renovación automática mientras navega
    options.SlidingExpiration = true;

    // Seguridad de la cookie
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("login", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("register", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("logout", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });
});

// Autorización
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
builder.Services.AddScoped<InformeResumenProyectosService>();

// Configuración del servicio de IA (Gemini)
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection("Gemini"));
builder.Services.AddHttpClient<AiEngineeringService>();

// Licencia de QuestPDF
QuestPDF.Settings.License = LicenseType.Community;

var app = builder.Build();

// Railway usa puerto dinámico
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    app.Urls.Add($"http://0.0.0.0:{port}");
}

// Procesa encabezados reenviados del proxy
app.UseForwardedHeaders();

// Configuración de errores en producción
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Redirección a HTTPS
app.UseHttpsRedirection();

// Cabeceras de seguridad básicas
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "img-src 'self' data: https:; " +
        "style-src 'self' 'unsafe-inline'; " +
        "script-src 'self' 'unsafe-inline'; " +
        "font-src 'self' data:; " +
        "connect-src 'self' https: wss:; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self';";

    await next();
});

// Rate limiting
app.UseRateLimiter();

// Middleware de autenticación / autorización
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Endpoint POST para registro manual
app.MapPost("/auth/register", async (
    HttpContext httpContext,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    InvitacionesService invitacionesService) =>
{
    var form = await httpContext.Request.ReadFormAsync();

    string nombreCompleto = (form["nombreCompleto"].ToString() ?? string.Empty).Trim();
    string email = (form["email"].ToString() ?? string.Empty).Trim().ToLowerInvariant();
    string password = form["password"].ToString() ?? string.Empty;
    string confirmPassword = form["confirmPassword"].ToString() ?? string.Empty;
    string token = (form["token"].ToString() ?? string.Empty).Trim();
    string returnUrl = (form["returnUrl"].ToString() ?? string.Empty).Trim();

    if (string.IsNullOrWhiteSpace(returnUrl) || !Uri.IsWellFormedUriString(returnUrl, UriKind.Relative))
        returnUrl = "/mis-proyectos";

    string BuildRegisterRedirect(string error)
    {
        string url = $"/register?error={Uri.EscapeDataString(error)}";

        if (!string.IsNullOrWhiteSpace(token))
            url += $"&token={Uri.EscapeDataString(token)}";

        if (!string.IsNullOrWhiteSpace(email))
            url += $"&email={Uri.EscapeDataString(email)}";

        return url;
    }

    if (string.IsNullOrWhiteSpace(nombreCompleto))
        return Results.Redirect(BuildRegisterRedirect("Debes indicar un nombre completo válido."));

    if (string.IsNullOrWhiteSpace(email))
        return Results.Redirect(BuildRegisterRedirect("Debes indicar un correo electrónico válido."));

    if (!new EmailAddressAttribute().IsValid(email))
        return Results.Redirect(BuildRegisterRedirect("El correo electrónico no es válido."));

    if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        return Results.Redirect(BuildRegisterRedirect("La contraseña debe tener al menos 6 caracteres."));

    if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        return Results.Redirect(BuildRegisterRedirect("Las contraseñas no coinciden."));

    var usuarioExistente = await userManager.FindByEmailAsync(email);

    if (usuarioExistente != null)
    {
        bool tienePassword = await userManager.HasPasswordAsync(usuarioExistente);

        if (tienePassword)
            return Results.Redirect(BuildRegisterRedirect("Ya existe una cuenta registrada con ese correo electrónico."));

        usuarioExistente.UserName = email;
        usuarioExistente.Email = email;
        usuarioExistente.NombreCompleto = nombreCompleto;
        usuarioExistente.TelefonoContacto ??= string.Empty;
        usuarioExistente.Cargo ??= string.Empty;
        usuarioExistente.EmpresaNombre ??= string.Empty;
        usuarioExistente.EmpresaDireccion ??= string.Empty;
        usuarioExistente.EmpresaCiudad ??= string.Empty;
        usuarioExistente.EmpresaProvincia ??= string.Empty;
        usuarioExistente.EmpresaCodigoPostal ??= string.Empty;
        usuarioExistente.EmpresaPais ??= string.Empty;
        usuarioExistente.EmpresaWeb ??= string.Empty;
        usuarioExistente.EmpresaIdentificacionFiscal ??= string.Empty;

        var resultadoUpdate = await userManager.UpdateAsync(usuarioExistente);
        if (!resultadoUpdate.Succeeded)
            return Results.Redirect(BuildRegisterRedirect(string.Join(" ", resultadoUpdate.Errors.Select(e => e.Description))));

        var resultadoPassword = await userManager.AddPasswordAsync(usuarioExistente, password);
        if (!resultadoPassword.Succeeded)
            return Results.Redirect(BuildRegisterRedirect(string.Join(" ", resultadoPassword.Errors.Select(e => e.Description))));

        var rolesActuales = await userManager.GetRolesAsync(usuarioExistente);
        if (!rolesActuales.Contains(RolesAplicacion.Usuario))
        {
            var resultadoRolExistente = await userManager.AddToRoleAsync(usuarioExistente, RolesAplicacion.Usuario);
            if (!resultadoRolExistente.Succeeded)
                return Results.Redirect(BuildRegisterRedirect("La cuenta existe, pero no se pudo asignar el rol Usuario."));
        }

        if (!string.IsNullOrWhiteSpace(token))
            await invitacionesService.AplicarInvitacionPorTokenAsync(usuarioExistente, token);
        else
            await invitacionesService.AplicarInvitacionPendienteAsync(usuarioExistente);

        await signInManager.SignInAsync(usuarioExistente, isPersistent: true);
        return Results.Redirect(returnUrl);
    }

    var usuario = new ApplicationUser
    {
        UserName = email,
        Email = email,
        NombreCompleto = nombreCompleto,
        TelefonoContacto = string.Empty,
        Cargo = string.Empty,
        EmpresaNombre = string.Empty,
        EmpresaDireccion = string.Empty,
        EmpresaCiudad = string.Empty,
        EmpresaProvincia = string.Empty,
        EmpresaCodigoPostal = string.Empty,
        EmpresaPais = string.Empty,
        EmpresaWeb = string.Empty,
        EmpresaIdentificacionFiscal = string.Empty
    };

    var resultado = await userManager.CreateAsync(usuario, password);
    if (!resultado.Succeeded)
        return Results.Redirect(BuildRegisterRedirect(string.Join(" ", resultado.Errors.Select(e => e.Description))));

    var resultadoRol = await userManager.AddToRoleAsync(usuario, RolesAplicacion.Usuario);
    if (!resultadoRol.Succeeded)
        return Results.Redirect(BuildRegisterRedirect("La cuenta se creó, pero no se pudo asignar el rol Usuario."));

    if (!string.IsNullOrWhiteSpace(token))
        await invitacionesService.AplicarInvitacionPorTokenAsync(usuario, token);
    else
        await invitacionesService.AplicarInvitacionPendienteAsync(usuario);

    await signInManager.SignInAsync(usuario, isPersistent: true);
    return Results.Redirect(returnUrl);
}).RequireRateLimiting("register");

// Endpoint POST para login manual
app.MapPost("/auth/login", async (
    HttpContext httpContext,
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    InvitacionesService invitacionesService) =>
{
    var form = await httpContext.Request.ReadFormAsync();

    // Datos del formulario
    string email = (form["email"].ToString() ?? string.Empty).Trim().ToLowerInvariant();
    string password = form["password"].ToString() ?? string.Empty;
    string returnUrl = form["returnUrl"].ToString() ?? string.Empty;
    string token = (form["token"].ToString() ?? string.Empty).Trim();

    // Detecta si se marcó "recordarme"
    bool rememberMe = false;
    var rememberRaw = form["rememberMe"].ToString();
    if (!string.IsNullOrWhiteSpace(rememberRaw))
    {
        rememberMe = rememberRaw.Equals("true", StringComparison.OrdinalIgnoreCase)
                     || rememberRaw.Equals("on", StringComparison.OrdinalIgnoreCase);
    }

    // URL por defecto
    if (string.IsNullOrWhiteSpace(returnUrl))
        returnUrl = "/mis-proyectos";

    // Seguridad: evita redirecciones externas
    if (!Uri.IsWellFormedUriString(returnUrl, UriKind.Relative))
        returnUrl = "/mis-proyectos";

    // Login con Identity
    var result = await signInManager.PasswordSignInAsync(
        email,
        password,
        isPersistent: rememberMe,
        lockoutOnFailure: true);

    if (result.Succeeded)
    {
        var usuario = await userManager.FindByEmailAsync(email);
        if (usuario is not null)
        {
            if (!string.IsNullOrWhiteSpace(token))
                await invitacionesService.AplicarInvitacionPorTokenAsync(usuario, token);
            else
                await invitacionesService.AplicarInvitacionPendienteAsync(usuario);
        }

        return Results.Redirect(returnUrl);
    }

    if (result.IsLockedOut)
    {
        return Results.Redirect($"/login?locked=1&returnUrl={Uri.EscapeDataString(returnUrl)}");
    }

    return Results.Redirect($"/login?error=1&returnUrl={Uri.EscapeDataString(returnUrl)}");
}).RequireRateLimiting("login");

// Endpoint POST para logout
app.MapPost("/auth/logout", async (SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/login");
}).RequireRateLimiting("logout");

// Archivos estáticos
app.MapStaticAssets();

// Configuración de Blazor
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Aplica migraciones al arrancar
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

// Seed de roles y SuperAdmin
await IdentitySeedData.InicializarAsync(app.Services, app.Configuration);

// Arranque de la aplicación
app.Run();
