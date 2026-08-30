using System.Text;
using System.Text.Json.Serialization;
using JoyeriaStock.Api.Application.Abstractions;
using JoyeriaStock.Api.Application.Services;
using JoyeriaStock.Api.Domain.Entities;
using JoyeriaStock.Api.Infrastructure;
using JoyeriaStock.Api.Infrastructure.Auth;
using JoyeriaStock.Api.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuración
// ---------------------------------------------------------------------------
// Todo lo sensible se puede sobreescribir por variable de entorno con doble guión bajo:
//   ConnectionStrings__Default, Jwt__Key, Cors__AllowedOrigins__0
// Es el mecanismo estándar de .NET y es el que van a usar los TPs 2 y 6.

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "Falta la cadena de conexión. Definí ConnectionStrings:Default en appsettings.json " +
        "o la variable de entorno ConnectionStrings__Default.");

builder.Services.AddDbContext<AppDbContext>(options => options
    .UseNpgsql(connectionString)
    // Tablas y columnas en snake_case, para que el SQL a mano no necesite comillas.
    .UseSnakeCaseNamingConvention());

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SeccionConfig));

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SeccionConfig).Get<JwtOptions>()
                 ?? new JwtOptions();

if (string.IsNullOrWhiteSpace(jwtOptions.Key) || Encoding.UTF8.GetByteCount(jwtOptions.Key) < 32)
{
    // Falla al arrancar y no en el primer login: un error de configuración tiene que ser ruidoso.
    throw new InvalidOperationException(
        "Jwt:Key es obligatoria y necesita al menos 32 bytes para firmar con HMAC-SHA256. " +
        "Definila en appsettings.Development.json o en la variable de entorno Jwt__Key.");
}

// ---------------------------------------------------------------------------
// Autenticación y autorización
// ---------------------------------------------------------------------------

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Sin el mapeo automático a URIs largas: los claims quedan como "sub", "role", "local_id".
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),

            // Le dice a [Authorize(Roles = "Admin")] y a User.Identity.Name dónde mirar.
            NameClaimType = ClaimsPersonalizados.Nombre,
            RoleClaimType = ClaimsPersonalizados.Rol
        };
    });

builder.Services.AddAuthorization();

// ---------------------------------------------------------------------------
// Servicios de la aplicación
// ---------------------------------------------------------------------------

builder.Services.AddHttpContextAccessor();

// TimeProvider en lugar de DateTime.UtcNow: en los tests del TP5 se reemplaza por
// FakeTimeProvider y las fechas dejan de ser un dato incontrolable.
builder.Services.AddSingleton(TimeProvider.System);

// Sólo el hasher de Identity (PBKDF2). No se usa ASP.NET Core Identity completo.
builder.Services.AddSingleton<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();

builder.Services.AddScoped<IGeneradorToken, GeneradorTokenJwt>();
builder.Services.AddScoped<IUsuarioActual, UsuarioActualHttp>();

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<LocalService>();
builder.Services.AddScoped<CategoriaService>();
builder.Services.AddScoped<GeneradorSku>();
builder.Services.AddScoped<ProductoService>();
builder.Services.AddScoped<StockService>();
builder.Services.AddScoped<MovimientoService>();

// ---------------------------------------------------------------------------
// MVC, CORS y Swagger
// ---------------------------------------------------------------------------

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Los enums viajan como texto ("Venta", "Admin") y no como números:
        // el JSON queda legible y el frontend no depende del orden del enum.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

var origenesPermitidos = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? ["http://localhost:5173"];

builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy
    .WithOrigins(origenesPermitidos)
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Joyería · API de stock",
        Version = "v1",
        Description = "Gestión de stock por local para una joyería de 3 sucursales."
    });

    // Botón "Authorize" en Swagger para pegar el JWT y probar los endpoints protegidos.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Pegá acá el token que devuelve POST /api/auth/login (sin la palabra 'Bearer')."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// Endpoint de salud, que el TP6 va a necesitar para los health checks del deploy.
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("base-de-datos");

var app = builder.Build();

// ---------------------------------------------------------------------------
// Pipeline HTTP
// ---------------------------------------------------------------------------

// Primero de todo: así atrapa también las excepciones de los middlewares siguientes.
app.UseMiddleware<ManejadorExcepcionesMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Joyería API v1"));
}

app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// ---------------------------------------------------------------------------
// Migraciones y datos iniciales
// ---------------------------------------------------------------------------
// Dos interruptores separados, porque son dos decisiones distintas:
//
//   AplicarMigracionesAlArrancar : crear/actualizar el esquema
//   SembrarDatosIniciales        : cargar locales, productos y usuarios de ejemplo
//
// En Development los dos valen true por comodidad. En cualquier otro entorno hay que
// activarlos explícitamente: el docker-compose del TP2 lo hace, porque ahí el requisito
// es que un solo comando deje el sistema usable.
//
// En un despliegue real los dos van en false: la migración es un paso del pipeline
// (TP6), y sembrar un usuario admin con contraseña conocida en producción es un agujero.

var aplicarMigraciones = app.Configuration.GetValue(
    "AplicarMigracionesAlArrancar", app.Environment.IsDevelopment());

var sembrarDatos = app.Configuration.GetValue(
    "SembrarDatosIniciales", app.Environment.IsDevelopment());

if (aplicarMigraciones || sembrarDatos)
{
    using var scope = app.Services.CreateScope();
    var servicios = scope.ServiceProvider;
    var logger = servicios.GetRequiredService<ILogger<Program>>();
    var db = servicios.GetRequiredService<AppDbContext>();

    if (aplicarMigraciones)
    {
        logger.LogInformation("Aplicando migraciones pendientes.");
        await db.Database.MigrateAsync();
    }

    if (sembrarDatos)
    {
        await DbSeeder.SembrarAsync(
            db,
            servicios.GetRequiredService<IPasswordHasher<Usuario>>(),
            servicios.GetRequiredService<IConfiguration>(),
            logger);
    }
}

app.Run();
// TODO: endpoint de salud
using NoExiste;
