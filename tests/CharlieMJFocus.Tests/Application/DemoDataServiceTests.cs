using CharlieMJFocus.Application.Services;
using CharlieMJFocus.Domain.Enums;
using CharlieMJFocus.Tests.Fakes;

namespace CharlieMJFocus.Tests.Application;

public sealed class DemoDataServiceTests
{
    private readonly ManualTimeProvider _clock = new();
    private readonly InMemoryTodoRepository _todos = new();
    private readonly InMemorySessionRepository _sessions = new();

    private DemoDataService Create() => new(new TodoService(_todos, _clock), _todos, _sessions, _clock);

    [Fact]
    public async Task SeedsRealisticDataIntoAnEmptyDatabase()
    {
        var seeded = await Create().SeedIfEmptyAsync();

        Assert.True(seeded);
        var all = await _todos.GetAllAsync();
        Assert.True(all.Count >= 8);
        Assert.Contains(all, t => t.Status == TodoStatus.Completed);
        Assert.Contains(all, t => t.IsScheduled);
        Assert.True(_sessions.Sessions.Count > 20);
    }

    [Fact]
    public async Task NeverTouchesAnExistingDatabase()
    {
        var service = Create();
        Assert.True(await service.SeedIfEmptyAsync());
        var count = (await _todos.GetAllAsync()).Count;

        Assert.False(await service.SeedIfEmptyAsync());
        Assert.Equal(count, (await _todos.GetAllAsync()).Count);
    }

    [Fact]
    public async Task ProducesStatisticsWithAStreakAndCategories()
    {
        await Create().SeedIfEmptyAsync();

        var stats = await new StatisticsService(_sessions, _todos, _clock).GetSummaryAsync(30);

        Assert.True(stats.TotalFocusMinutes > 0);
        Assert.True(stats.CurrentStreakDays >= 3);   // today, yesterday and the day before have sessions
        Assert.True(stats.ByCategory.Count >= 2);
    }

    [Fact]
    public async Task IncludesAnOverlapSoThePlannerShowsAConflict()
    {
        await Create().SeedIfEmptyAsync();

        var plan = await new PlannerService(_todos, _sessions, _clock).GetDayAsync(new DateOnly(2026, 11, 20));

        Assert.True(plan.ConflictCount >= 2);
    }
}
