using System.Globalization;
using Contratos.Core.Enums;
using Contratos.Core.Interfaces;
using Contratos.Infrastructure.Persistence;
using Contratos.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configuração de Cultura pt-BR para valores monetários e datas
var ptBrCulture = new CultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentCulture = ptBrCulture;
CultureInfo.DefaultThreadCurrentUICulture = ptBrCulture;

builder.Services.AddControllersWithViews();

// Serviços de Criptografia AES-256, LDAP e Autenticação
builder.Services.AddSingleton<AesCryptoService>();
builder.Services.AddSingleton<ICryptoService>(sp => sp.GetRequiredService<AesCryptoService>());
builder.Services.AddScoped<ILdapService, LdapService>();
builder.Services.AddScoped<AuthenticationService>();

// Provedor de Banco de Dados Dual (Json Thread-Safe Dev / EF Core SQL Server Prod)
var databaseProvider = builder.Configuration.GetValue<string>("DatabaseProvider") ?? "Json";

if (string.Equals(databaseProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddDbContext<ClmDbContext>((sp, options) =>
    {
        var crypto = sp.GetRequiredService<AesCryptoService>();
        var rawConn = builder.Configuration.GetConnectionString("SqlServerConnection") ?? string.Empty;
        var resolvedConn = crypto.ResolveConnectionStringTokens(rawConn);
        options.UseSqlServer(resolvedConn);
    });

    builder.Services.AddScoped<IClmRepository, EfClmRepository>();
}
else
{
    var relativePath = builder.Configuration.GetValue<string>("JsonDatabasePath") ?? "App_Data/clm_database.json";
    var fullJsonPath = Path.Combine(builder.Environment.ContentRootPath, relativePath);

    builder.Services.AddSingleton<IClmRepository>(sp =>
        new JsonClmRepository(fullJsonPath, sp.GetRequiredService<ICryptoService>()));
}

// Autenticação por Cookie & Políticas RBAC (Regra do Mais Permissivo)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "ContratosCLM.Auth";
        options.Cookie.HttpOnly = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(10);
        options.SlidingExpiration = true;
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("MasterOnly", policy =>
        policy.RequireRole(nameof(UserRole.Master)));

    options.AddPolicy("FinanceiroAccess", policy =>
        policy.RequireRole(nameof(UserRole.Master), nameof(UserRole.Financeiro)));

    options.AddPolicy("GestorContratosAccess", policy =>
        policy.RequireRole(nameof(UserRole.Master), nameof(UserRole.Financeiro), nameof(UserRole.GestorContratos)));

    options.AddPolicy("AuthenticatedRead", policy =>
        policy.RequireAuthenticatedUser());
});

var app = builder.Build();

var supportedCultures = new[] { ptBrCulture };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("pt-BR"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
