using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using MySql.Data.EntityFrameworkCore.Extensions;
using TTT.Database;
using TTT.OpenRail;
using TTT.Services;
using TTT.TrainData.Controller;

namespace TTT;

internal abstract class Program
{
    static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // DB Connection TODO Setup DB with new datasets
        string host = builder.Configuration["DB_HOST"] ?? "localhost";
        string port = builder.Configuration["DB_PORT"] ?? "3306";
        string user = builder.Configuration["DB_USERNAME"] ?? "root";
        string pass = builder.Configuration["DB_PASSWORD"] ?? "app";
        string db   = builder.Configuration["DB_NAME"] ?? "ttt";
        var conn    = $"Host={host};Port={port};Database={db};Username={user};Password={pass}";
        
        builder.Services.AddDbContext<TttDbContext>(options =>
        {
            options.UseMySQL(conn);
            
            var dev = builder.Environment.IsDevelopment();
            options.EnableSensitiveDataLogging(dev);
            options.EnableDetailedErrors(dev);
        });
        
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Information);
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Query", LogLevel.Warning);
        
        // NR config & services
        builder.Services.Configure<NetRailOptions>(builder.Configuration.GetSection("OpenRail"));
        builder.Services.AddHostedService<MovementsIngestionService>();
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