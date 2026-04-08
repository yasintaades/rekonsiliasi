using Microsoft.AspNetCore.Http.Features;
using Reconciliation.Api.Services;
using Reconciliation.Api.Repositories;

var builder = WebApplication.CreateBuilder(args);

// =====================
// CORS
// =====================
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// =====================
// LIMIT UPLOAD
// =====================
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 104857600; // 100 MB
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104857600;
});

// =====================
// DEPENDENCY INJECTION
// =====================
builder.Services.AddScoped<ReconService>();
builder.Services.AddScoped<ReconRepository>();
builder.Services.AddScoped<ReconPOVService>();
builder.Services.AddScoped<ReconPOVRepository>();

builder.Services.AddControllers();
app.MapControllers();

builder.Services.AddControllers();

// =====================
// BUILD APP
// =====================
var app = builder.Build();

// =====================
// MIDDLEWARE
// =====================
app.UseCors();

app.MapControllers();

app.MapGet("/", () => "API is running...");

// =====================
app.Run();
