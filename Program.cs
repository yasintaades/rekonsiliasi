using Reconciliation.Api.Endpoints;


internal class Program
{
    private static void Main(string[] args){
        
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();

        app.MapGet("/", () => "Hello World!");
        // mapping endpoint

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy.AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        app.MapReconciliationEndpoints();
        app.UseCors("AllowAll");

        app.Run();


    }
}
