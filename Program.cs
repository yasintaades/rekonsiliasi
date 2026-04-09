using Microsoft.AspNetCore.Http.Features;
using Reconciliation.Api.Endpoints;

    var builder = WebApplication.CreateBuilder(args);

    // CORS
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });

    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.MaxRequestBodySize = 104857600; // 100 MB
    });
        builder.Services.Configure<FormOptions>(options =>
    {
        options.MultipartBodyLengthLimit = 104857600;
    });


    var app = builder.Build();

    app.UseCors();

    // 🔥 panggil endpoint dari file lain
    app.MapReconB2BEndpoint();
    app.MapGet("/", () => "API is running...");
    app.MapGet("/reconciliations/upload/details", () => "Details endpoint is running...");
    app.MapReconPOVEndpoints();

// =====pemisah=====


    app.Run();
