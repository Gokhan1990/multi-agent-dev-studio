using Microsoft.EntityFrameworkCore;
using SaaSFast.Application.Services;
using SaaSFast.Domain.Entities;
using SaaSFast.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    var conn = builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrWhiteSpace(conn))
    {
        options.UseNpgsql(conn);
    }
    else
    {
        options.UseInMemoryDatabase("SaaSFast");
    }
});

builder.Services.AddSingleton<VoiceService>();
builder.Services.AddSingleton<AgentRouterService>();
builder.Services.AddSingleton<OrchestratorService>();
builder.Services.AddSingleton<AiService>();
builder.Services.AddSingleton<AgentMemoryService>();
builder.Services.AddSingleton<AgentPerformanceTracker>();
builder.Services.AddHttpClient<CodeReviewService>(c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddSingleton<CommandQueueService>();
builder.Services.AddSingleton<OpencodeService>();
builder.Services.AddHttpClient<CodeExecutorService>(c => c.Timeout = TimeSpan.FromSeconds(120));
builder.Services.AddSingleton<SelfImprovementService>();
builder.Services.AddSingleton<AgentAbilityService>();
builder.Services.AddSingleton<AgentTrainingService>();
builder.Services.AddControllers();

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

app.UseCors();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    db.Rooms.AddRange(
        new Room { Name = "Strategy Room", Type = "strategy" },
        new Room { Name = "Engineering Room", Type = "engineering" }
    );
    db.SaveChanges();
}

app.MapControllers();
app.Run();