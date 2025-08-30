using Microsoft.EntityFrameworkCore;
using SplitDuo.Api.Extensions;
using SplitDuo.Core.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.AddServices();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.MigrateAsync().Wait();
}

app.ConfigureServices();

app.Run();