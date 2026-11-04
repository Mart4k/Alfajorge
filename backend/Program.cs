using System.Text;
using AlfajorgeBackend.Data;
using AlfajorgeBackend.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);

var dataDirectory = builder.Configuration["ALFAJORGE_DATA_DIRECTORY"];
if (string.IsNullOrWhiteSpace(dataDirectory))
{
    dataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Alfajorge");
}
Directory.CreateDirectory(dataDirectory);

var databasePath = builder.Configuration["ALFAJORGE_DB_PATH"];
if (string.IsNullOrWhiteSpace(databasePath))
{
    databasePath = Path.Combine(dataDirectory, "alfajorge.db");
}
databasePath = Path.GetFullPath(databasePath);
Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
var connectionString = new SqliteConnectionStringBuilder
{
    DataSource = databasePath,
    ForeignKeys = true
}.ToString();

var tokenSecret = builder.Configuration["ALFAJORGE_TOKEN_SECRET"];
if (string.IsNullOrWhiteSpace(tokenSecret))
{
    var secretPath = Path.Combine(dataDirectory, "jwt-signing.key");
    if (!File.Exists(secretPath))
    {
        tokenSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        File.WriteAllText(secretPath, tokenSecret);
    }
    else
    {
        tokenSecret = File.ReadAllText(secretPath).Trim();
    }
}
if (tokenSecret.Length < 32)
{
    throw new InvalidOperationException("ALFAJORGE_TOKEN_SECRET debe tener al menos 32 caracteres.");
}
builder.Configuration["ALFAJORGE_TOKEN_SECRET"] = tokenSecret;

var issuer = builder.Configuration["Jwt:Issuer"];
var audience = builder.Configuration["Jwt:Audience"];
if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
{
    throw new InvalidOperationException("Configurá Jwt:Issuer y Jwt:Audience.");
}

var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var environmentOrigins = builder.Configuration["ALFAJORGE_ALLOWED_ORIGINS"]?
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? [];
var allowedOrigins = configuredOrigins
    .Concat(environmentOrigins)
    .Concat([
        "https://alfajorge.vercel.app",
        "https://alfajorge-clientes1.vercel.app",
        "https://alfajorge-empleados1.vercel.app"
    ])
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
builder.Services.AddCors(options => options.AddPolicy("FrontendOrigins", policy =>
{
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
}));

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<IClaimsTransformation, DatabaseRoleClaimsTransformation>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<OperacionService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenSecret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = System.Security.Claims.ClaimTypes.Name,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Cliente", policy => policy.RequireRole("CLIENTE"));
    options.AddPolicy("Vendedor", policy => policy.RequireRole("VENDEDOR"));
    options.AddPolicy("Administrativo", policy => policy.RequireRole("ADMINISTRATIVO"));
    options.AddPolicy("Empleado", policy => policy.RequireRole("VENDEDOR", "ADMINISTRATIVO"));
});
builder.Services.AddControllers();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await LocalDatabaseInitializer.InitializeAsync(db, app.Configuration);
    app.Logger.LogInformation("Base de datos local lista: {DatabasePath}", databasePath);
    if (!await db.Empleados.AnyAsync())
    {
        app.Logger.LogWarning(
            "No hay una cuenta administrativa. Para crearla en el primer inicio, configurá ALFAJORGE_INITIAL_ADMIN_EMAIL y ALFAJORGE_INITIAL_ADMIN_PASSWORD.");
    }
}

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    if (exception != null)
    {
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("UnhandledException");
        logger.LogError(exception, "Unhandled API exception.");
    }

    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new { error = "Ocurrió un error interno. Intentá nuevamente." });
}));
app.UseRouting();
app.UseCors("FrontendOrigins");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
