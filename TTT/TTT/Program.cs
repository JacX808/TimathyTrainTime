using TTT.OpenRail;
using TTT.TrainData.Controller;

namespace TTT;

internal abstract class Program
{
    static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // DB Connection TODO Setup DB with new datasets
        /*string host = builder.Configuration["DB_HOST"] ?? "localhost";
        string port = builder.Configuration["DB_PORT"] ?? "5432";
        string user = builder.Configuration["DB_USERNAME"] ?? "app";
        string pass = builder.Configuration["DB_PASSWORD"] ?? "app";
        string db = builder.Configuration["DB_NAME"] ?? "ttt";

        var conn = $"Host={host};Port={port};Database={db};Username={user};Password={pass}";
        builder.Services.AddDbContext<TttDbContext>(o => o.UseNpgsql(conn));*/

        // Swagger & controllers
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        
        builder.Services.Configure<NetRailOptions>(builder.Configuration.GetSection("OpenRail"));
        builder.Services.AddSingleton<OpenRailNrodReceiver>();
        builder.Services.AddHostedService<MessageBaordObserver>();

        var app = builder.Build();

        app.UseSwagger();
        app.UseSwaggerUI();
        app.MapControllers();
        app.Run();
    }
}