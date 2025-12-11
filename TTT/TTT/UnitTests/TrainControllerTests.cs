using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using TTT.Database;
using TTT.DataSets;
using TTT.TrainData.Controller;

namespace TTT.UnitTests;

[TestFixture]
public class TrainsControllerTests
{
    private static readonly DateOnly Today = new(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day);
    
    private static TttDbContext MakeDb()
    {
        var opts = new DbContextOptionsBuilder<TttDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors()
            .Options;

        var dbContext = new TttDbContext(opts, new DbConfig(
            "localhost",
            1433,
            "postgres",
             "root",
            "train"));

        // Seed
        dbContext.TrainRuns.AddRange(
            new TrainRun { TrainId = "A1", ServiceDate = Today },
            new TrainRun { TrainId = "B2", ServiceDate = Today.AddDays(1)},
            new TrainRun { TrainId = "C3", ServiceDate = Today}
        );

        dbContext.CurrentTrainPosition.Add(
            new CurrentTrainPosition { TrainId = "A1", LocStanox = "123", ReportedAt = DateTimeOffset.UtcNow });

        dbContext.MovementEvents.AddRange(
            new MovementEvent
            {
                TrainId = "A1",
                LocStanox = "123",
                PlannedEventType = "ARRIVAL",
                EventType = "ARRIVAL",
                EventSource = "AUTOMATIC",
                ActualTimestampMs = 1000
            },
            new MovementEvent
            {
                TrainId = "A1",
                LocStanox = "124",
                PlannedEventType = "DEPARTURE",
                EventType = "DEPARTURE",
                EventSource = "AUTOMATIC",
                ActualTimestampMs = 2000
            }
        );

        dbContext.SaveChanges();
        return dbContext;
    }

    [Test]
    public async Task GetTrainIds_NoDate_ReturnsAll()
    {
        await using var db = MakeDb();
        var sut = new TrainsController(db);

        var result = await sut.GetTrainIds(null, CancellationToken.None) as OkObjectResult;
        Assert.That(result, Is.Not.Null, "Expected 200 OK");

        var ids = result!.Value as IReadOnlyList<string>;
        Assert.That(ids, Is.Not.Null, "Expected payload list");
        Assert.That(ids!, Is.EquivalentTo(new[] { "A1", "B2", "C3" }));
    }

    [Test]
    public async Task GetTrainIds_WithDate_ReturnsOnlyThatDate()
    {
        await using var db = MakeDb();
        var sut = new TrainsController(db);

        var result = await sut.GetTrainIds(new DateOnly(Today.Year, Today.Month, Today.Day)
            , CancellationToken.None) as OkObjectResult;
        
        Assert.That(result, Is.Not.Null, "Expected 200 OK");

        var ids = result!.Value as IReadOnlyList<string>;
        Assert.That(ids, Is.Not.Null, "Expected payload list");
        Assert.That(ids!, Is.EquivalentTo(new[] { "A1", "C3" }));
    }

    [Test]
    public async Task GetPosition_Found_ReturnsOk()
    {
        await using var db = MakeDb();
        var sut = new TrainsController(db);

        var result = await sut.GetPosition("A1", CancellationToken.None);
        Assert.That(result, Is.InstanceOf<OkObjectResult>());
    }

    [Test]
    public async Task GetPosition_NotFound_Returns404()
    {
        await using var db = MakeDb();
        var sut = new TrainsController(db);

        var result = await sut.GetPosition("ZZZ", CancellationToken.None);
        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    [Test]
    public async Task GetMovements_ReturnsChronologicalList()
    {
        await using var db = MakeDb();
        var sut = new TrainsController(db);

        var result = await sut.GetMovements("A1", null, null, CancellationToken.None) as OkObjectResult;
        Assert.That(result, Is.Not.Null, "Expected 200 OK");

        var list = result!.Value as IReadOnlyList<MovementEvent>;
        Assert.That(list, Is.Not.Null, "Expected payload list");
        Assert.That(list!.Count, Is.EqualTo(2));
        Assert.That(list!.First().ActualTimestampMs, Is.LessThan(list!.Last().ActualTimestampMs));
    }
}
