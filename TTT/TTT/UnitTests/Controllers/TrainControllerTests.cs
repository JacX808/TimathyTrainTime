using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using TTT.Controller;
using TTT.Database;
using TTT.DataSets;
using TTT.DataSets.Train;
using TTT.Model;

namespace TTT.UnitTests.Controllers;

[TestFixture]
public class TrainsControllerTests
{
    private static readonly DateOnly Today = new(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day);
    private readonly TrainDataModel _trainDataModel;
    private readonly Logger<TrainsController> _logger;

    public TrainsControllerTests()
    {
        var database = MakeDb();
        
        // logs for unit tests
        _logger = new Logger<TrainsController>(new LoggerFactory());
        ILogger<TrainDataModel> trainLogger = new Logger<TrainDataModel>(new LoggerFactory());
        
        _trainDataModel = new TrainDataModel(database, trainLogger);
    }

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
            new TrainRun { TrainId = "B2", ServiceDate = Today.AddDays(1) },
            new TrainRun { TrainId = "C3", ServiceDate = Today }
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
}
