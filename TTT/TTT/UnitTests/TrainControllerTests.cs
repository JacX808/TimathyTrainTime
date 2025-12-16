using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using TTT.Database;
using TTT.TrainData.Controller;
using TTT.TrainData.DataSets;
using TTT.TrainData.Model;

namespace TTT.UnitTests;

[TestFixture]
public class TrainsControllerTests
{
    private static readonly DateOnly Today = new(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day);
    private readonly TrainDataModel _trainDataModel;
    
    private static TttDbContext MakeDb()
    {
        var options = new DbContextOptionsBuilder<TttDbContext>()
            .UseInMemoryDatabase("TestingDB")
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors()
            .Options;
        
        DbConfig dbConfig = new DbConfig(
            "localhost",
            1433,
            "test", // only way to get in memory db is by naming the db test
            "root",
            "");

        var dbContext = new TttDbContext(options, dbConfig);

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

    public TrainsControllerTests()
    {
        var database = MakeDb();
        _trainDataModel = new TrainDataModel(database, new Logger<TrainDataModel>(new LoggerFactory()));
    }

    [Test]
    public async Task GetTrainIds_NoDate_ReturnsAll()
    {
        var trainsController = new TrainsController(_trainDataModel);

        var result = await trainsController.GetTrainIds(null, CancellationToken.None);
        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task GetTrainIds_WithDate_ReturnsOnlyThatDate()
    {
        var trainsController = new TrainsController(_trainDataModel);

        var result = await trainsController.GetTrainIds(new DateOnly(Today.Year, Today.Month, Today.Day),
            CancellationToken.None) as OkObjectResult;
        
        Assert.That(result, Is.Not.Null, "Expected 200 OK");

        var ids = result!.Value as IReadOnlyList<string>;
        Assert.That(ids, Is.Not.Null, "Expected payload list");
        Assert.That(ids!, Is.EquivalentTo(new[] { "A1", "C3" }));
    }

    [Test]
    public async Task GetPosition_Found_ReturnsOk()
    {
        var trainsController = new TrainsController(_trainDataModel);

        var result = await trainsController.GetPosition("A1", CancellationToken.None);
        Assert.That(result, Is.InstanceOf<OkObjectResult>());
    }

    [Test]
    public async Task GetPosition_NotFound_Returns404()
    {
        
        var trainsController = new TrainsController(_trainDataModel);

        var result = await trainsController.GetPosition("ZZZ", CancellationToken.None);
        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task GetMovements_ReturnsChronologicalList()
    {
        var trainsController = new TrainsController(_trainDataModel);

        var result = await trainsController.GetMovements("A1", null, null, CancellationToken.None) as OkObjectResult;
        Assert.That(result, Is.Not.Null, "Expected 200 OK");

        var list = result!.Value as IReadOnlyList<MovementEvent>;
        Assert.That(list, Is.Not.Null, "Expected payload list");
        Assert.That(list!.Count, Is.EqualTo(2));
        Assert.That(list!.First().ActualTimestampMs, Is.LessThan(list!.Last().ActualTimestampMs));
    }
}
