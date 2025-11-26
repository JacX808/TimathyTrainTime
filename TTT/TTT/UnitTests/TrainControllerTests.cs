using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using TTT.Database;
using TTT.DataSets;
using TTT.TrainData.Controller;

namespace TTT.UnitTests;

[TestFixture]
public class TrainsControllerTests
{
    private static TttDbContext MakeDb()
    {
        var opts = new DbContextOptionsBuilder<TttDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors()
            .Options;
        var dbContext = new TttDbContext(opts);

        // Seed
        dbContext.TrainRuns.AddRange(
            new TrainRun { TrainId = "A1", ServiceDate = new DateOnly(2025, 11, 25) },
            new TrainRun { TrainId = "B2", ServiceDate = new DateOnly(2025, 11, 26) },
            new TrainRun { TrainId = "C3", ServiceDate = new DateOnly(2025, 11, 25) }
        );
        dbContext.CurrentPositions.Add(new CurrentTrainPosition { TrainId = "A1", LocStanox = "123", ReportedAt = DateTimeOffset.UtcNow });
        dbContext.MovementEvents.AddRange(
            new MovementEvent { TrainId = "A1", LocStanox = "123", EventType = "ARRIVAL", ActualTimestampMs = 1000 },
            new MovementEvent { TrainId = "A1", LocStanox = "124", EventType = "DEPARTURE", ActualTimestampMs = 2000 }
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
        Assert.That(result, Is.Not.Null);

        var ids = (result!.Value as IReadOnlyList<string>)!;
        CollectionAssert.AreEquivalent(new[] { "A1", "B2", "C3" }, ids);
    }

    [Test]
    public async Task GetTrainIds_WithDate_ReturnsOnlyThatDate()
    {
        await using var db = MakeDb();
        var sut = new TrainsController(db);

        var result = await sut.GetTrainIds(new DateOnly(2025, 11, 25), CancellationToken.None) as OkObjectResult;
        Assert.That(result, Is.Not.Null);

        var ids = (result!.Value as IReadOnlyList<string>)!;
        CollectionAssert.AreEquivalent(new[] { "A1", "C3" }, ids);
    }

    [Test]
    public async Task GetPosition_Found_ReturnsOk()
    {
        await using var db = MakeDb();
        var sut = new TrainsController(db);

        var result = await sut.GetPosition("A1", CancellationToken.None);
        Assert.That(result, Is.TypeOf<OkObjectResult>());
    }

    [Test]
    public async Task GetPosition_NotFound_Returns404()
    {
        await using var db = MakeDb();
        var sut = new TrainsController(db);

        var result = await sut.GetPosition("ZZZ", CancellationToken.None);
        Assert.That(result, Is.TypeOf<NotFoundResult>());
    }

    [Test]
    public async Task GetMovements_ReturnsChronologicalList()
    {
        await using var db = MakeDb();
        var sut = new TrainsController(db);

        var result = await sut.GetMovements("A1", null, null, CancellationToken.None) as OkObjectResult;
        Assert.That(result, Is.Not.Null);

        var list = (result!.Value as IReadOnlyList<MovementEvent>)!;
        Assert.That(list.Count, Is.EqualTo(2));
        Assert.That(list.First().ActualTimestampMs, Is.LessThan(list.Last().ActualTimestampMs));
    }
}
