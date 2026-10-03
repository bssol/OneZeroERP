using System.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using OneZeroErp.Api.Common;
using OneZeroErp.Application;
using OneZeroErp.IdentityAccess;
using OneZeroErp.Infrastructure;
using OneZeroErp.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var signingKey = builder.Configuration["Authentication:SigningKey"];
if (string.IsNullOrWhiteSpace(signingKey) && builder.Environment.IsDevelopment())
{
    signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    builder.Configuration["Authentication:SigningKey"] = signingKey;
}
if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
    throw new InvalidOperationException("Authentication:SigningKey must be supplied with at least 32 characters outside local development.");

builder.Services.AddOneZeroPlatform(builder.Configuration);
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<IAuthenticationService, AppUserAuthenticationService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddHealthChecks().AddDbContextCheck<ErpDbContext>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "OneZero ERP API",
        Version = "v1",
        Description = "Application API for OneZero ERP."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a JWT access token as: Bearer {token}"
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "OneZero ERP API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await Results.Problem("An unexpected error occurred.").ExecuteAsync(context);
}));
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    var requiredPermission = context.GetEndpoint()?.Metadata.GetMetadata<PermissionMetadata>()?.Permission;
    if (requiredPermission is not null && !context.User.HasPermission(requiredPermission))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }
    await next(context);
});

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready");
app.MapApiEndpoints();

app.Run();
