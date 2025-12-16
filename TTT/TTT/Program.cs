using Microsoft.OpenApi.Models;
using TTT.Database;
using TTT.OpenRail;
using TTT.TrainData.Controller;
using TTT.TrainData.Model;
using TTT.TrainData.Services;

namespace TTT;

internal abstract class Program
{
    static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // DB Connection
        DbConfig dbConfig = new DbConfig(
            builder.Configuration["DB_HOST"] ?? "localhost",
            3307,
            builder.Configuration["DB_DATABASE"] ?? "ttt",
            builder.Configuration["DB_USERNAME"] ?? "root",
            builder.Configuration["DB_PASSWORD"] ?? "train");
        
        builder.Services.AddSingleton(dbConfig);
        builder.Services.AddDbContext<TttDbContext>(options =>
        {
            var dev = builder.Environment.IsDevelopment();
            options.EnableSensitiveDataLogging(dev);
            options.EnableDetailedErrors(dev);
        });
        
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Information);
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Query", LogLevel.Warning);
        
        builder.Services.AddScoped<ITrainDataModel, TrainDataModel>();
        builder.Services.AddScoped<IMovementsIngestionService, MovementsIngestionService>();
        
        // NR config & services
        builder.Services.Configure<NetRailOptions>(builder.Configuration.GetSection("OpenRail"));
        builder.Services.AddSingleton<OpenRailNrodReceiver>();
        builder.Services.AddHostedService<MessageBaordObserver>();
        
        // Swagger & controllers
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
           options.SwaggerDoc("v1", new OpenApiInfo
           {
               Title = "TTT",
               Version = "v1",
               Description = "Nation rail train mapper",
           });
        });

        var app = builder.Build();

        app.UseSwagger();
        app.UseSwaggerUI(uiOption => uiOption.SwaggerEndpoint("/swagger/v1/swagger.json", "TTT"));
        app.MapControllers();
        app.Run();
    }
}