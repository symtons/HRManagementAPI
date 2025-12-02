using HRManagementAPI.Data;
using HRManagementAPI.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// CONFIGURE FILE LOGGING
// ============================================
builder.Logging.ClearProviders(); // Clear default providers
builder.Logging.AddConsole();     // Keep console logging
builder.Logging.AddDebug();       // Keep debug logging

// Add File Logging - CORRECTED VERSION
var logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
if (!Directory.Exists(logDirectory))
{
    Directory.CreateDirectory(logDirectory);
}

var logFilePath = Path.Combine(logDirectory, $"errors-{DateTime.Now:yyyy-MM-dd}.txt");

// Use the correct AddFile method signature
builder.Logging.AddFile(logFilePath, minimumLevel: LogLevel.Warning);

// Add services to the container.
builder.Services.AddControllers();

// Add CORS policy for React app
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy => policy
            .WithOrigins("http://localhost:3000",
            "http://localhost:7144")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

// Add HttpContextAccessor for accessing HTTP context in middleware
builder.Services.AddHttpContextAccessor();

// Configure Entity Framework and SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"];

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

// Swagger configuration with JWT support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "HR Management API",
        Version = "v1",
        Description = "API for HR Management System with JWT Authentication"
    });

    // Add JWT Authentication to Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token in the text input below.\n\nExample: \"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...\""
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
    options.OperationFilter<HRManagementAPI.FileUploadOperationFilter>();
});

var app = builder.Build();


app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var exceptionHandlerPathFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
        var exception = exceptionHandlerPathFeature?.Error;

        if (exception != null)
        {
            logger.LogError(exception,
                "Unhandled exception occurred. Path: {Path}, Method: {Method}, User: {User}",
                context.Request.Path,
                context.Request.Method,
                context.User?.Identity?.Name ?? "Anonymous");
        }

        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(new
        {
            success = false,
            message = "An internal server error occurred. Please contact support if the problem persists.",
            timestamp = DateTime.UtcNow
        });
    });
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowReactApp");

// Add Authentication & Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Add Audit Logging Middleware (after authentication so we have user context)
app.UseAuditLogging();

app.MapControllers();

// Log application startup
var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
startupLogger.LogInformation("HR Management API started at {Time}", DateTime.UtcNow);

app.Run();