using AzureSuite.Application.Abstractions;
using AzureSuite.Application.Messages;
using AzureSuite.Infrastructure.Persistence;
using AzureSuite.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

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
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
