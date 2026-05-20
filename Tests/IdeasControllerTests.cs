using Xunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaaSFast.Domain.Entities;
using SaaSFast.Infrastructure.Data;
using SaaSFast.Presentation.Controllers;

namespace Tests;

public class IdeasControllerTests
{
    private static AppDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Get_ReturnsEmptyList_WhenNoIdeas()
    {
        using var db = CreateDbContext("IdeasEmpty");
        var controller = new IdeasController(db);

        var result = await controller.Get() as OkObjectResult;

        Assert.NotNull(result);
        var ideas = result.Value as List<Idea>;
        Assert.NotNull(ideas);
        Assert.Empty(ideas);
    }

    [Fact]
    public async Task Post_AddsIdeaAndReturnsCreated()
    {
        using var db = CreateDbContext("IdeasPost");
        var controller = new IdeasController(db);
        var idea = new Idea { Title = "Test Idea", Description = "Test Description" };

        var result = await controller.Post(idea) as CreatedAtActionResult;

        Assert.NotNull(result);
        Assert.Equal(nameof(IdeasController.GetById), result.ActionName);
        var returned = result.Value as Idea;
        Assert.NotNull(returned);
        Assert.Equal("Test Idea", returned.Title);
        Assert.Equal("Test Description", returned.Description);
    }

    [Fact]
    public async Task GetById_ReturnsIdea_WhenExists()
    {
        using var db = CreateDbContext("IdeasGetById");
        var controller = new IdeasController(db);
        var idea = new Idea { Title = "Existing", Description = "Desc" };
        db.Ideas.Add(idea);
        await db.SaveChangesAsync();

        var result = await controller.GetById(idea.Id) as OkObjectResult;

        Assert.NotNull(result);
        var returned = result.Value as Idea;
        Assert.NotNull(returned);
        Assert.Equal("Existing", returned.Title);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenMissing()
    {
        using var db = CreateDbContext("IdeasNotFound");
        var controller = new IdeasController(db);

        var result = await controller.GetById(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Get_ReturnsAllIdeas()
    {
        using var db = CreateDbContext("IdeasGetAll");
        var controller = new IdeasController(db);
        db.Ideas.AddRange(
            new Idea { Title = "Idea 1", Description = "Desc 1" },
            new Idea { Title = "Idea 2", Description = "Desc 2" }
        );
        await db.SaveChangesAsync();

        var result = await controller.Get() as OkObjectResult;

        Assert.NotNull(result);
        var ideas = result.Value as List<Idea>;
        Assert.NotNull(ideas);
        Assert.Equal(2, ideas.Count);
    }

    [Fact]
    public async Task Post_SetsGeneratedId()
    {
        using var db = CreateDbContext("IdeasGenId");
        var controller = new IdeasController(db);
        var idea = new Idea { Title = "Test", Description = "Test" };

        var result = await controller.Post(idea) as CreatedAtActionResult;

        Assert.NotNull(result);
        var returned = result.Value as Idea;
        Assert.NotNull(returned);
        Assert.True(returned.Id > 0);
    }
}
