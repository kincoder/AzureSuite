using AzureSuite.Application.Abstractions;
using AzureSuite.Application.Messages;
using AzureSuite.Infrastructure.Persistence;
using AzureSuite.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options =>
{
    // Registers a Bearer scheme in the generated OpenAPI document so the Scalar UI
    // shows an "Authorize" button - without this it has no way to know the API
    // expects a JWT at all.
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Paste an access token acquired for api://edd37b21-2fdb-401b-992b-96723111682a"
        };
        return Task.CompletedTask;
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("AzureSuiteDb"),
        sqlOptions => sqlOptions.EnableRetryOnFailure()));

builder.Services.AddScoped<IPacs008MessageRepository, Pacs008MessageRepository>();
builder.Services.AddScoped<IPacs008MessageService, Pacs008MessageService>();

// The API is a resource server: it validates JWTs issued by Entra ID for the
// AzureSuite-Api app registration, it never issues or stores credentials itself.
builder.Services
    .AddAuthentication(Constants.Bearer)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.AddPreferredSecuritySchemes("Bearer");
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
