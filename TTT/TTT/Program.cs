using Microsoft.EntityFrameworkCore;
using TTT.Database;
using TTT.Movement;
using TTT.Movement.ConsumerBackgroundService;

var builder = WebApplication.CreateBuilder(args);

// EF Core
builder.Services.AddDbContext<TttDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.Configure<OpenRailOptions>(builder.Configuration.GetSection("OpenRail"));

// Background ingestion
builder.Services.AddHostedService<TrainMovementsConsumer>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();