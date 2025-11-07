using TTT.TrainData.Connection;
using TTT.TrainData.DataSets;

var builder = WebApplication.CreateBuilder(args);

// Swagger & controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// In-memory cache
builder.Services.AddSingleton<LiveMovementCache>();

// Options + background consumer
builder.Services.Configure<OpenRailOptions>(builder.Configuration.GetSection("OpenRail"));
//builder.Services.AddHostedService<KafkaMovementsConsumer);

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

// REST: latest snapshot (in-memory)
app.MapGet("/api/movements/latest", (LiveMovementCache cache, int? take) =>
{
    var items = cache.Latest(take is > 0 and < 5000 ? take.Value : 200);
    return Results.Ok(items);
});

// SSE: live stream of movements (in-memory, no DB)
app.MapGet("/api/stream/movements", async (HttpContext ctx, LiveMovementCache cache, CancellationToken ct) =>
{
    ctx.Response.Headers.Append("Content-Type", "text/event-stream");
    int count = 0;
    await foreach (var evt in cache.ReadStream(ct))
    {
        var json = System.Text.Json.JsonSerializer.Serialize(evt);
        await ctx.Response.WriteAsync($"event: movement\n", ct);
        await ctx.Response.WriteAsync($"data: {json}\n\n", ct);
        await ctx.Response.Body.FlushAsync(ct);
        count++;

        if (count > 1)
        {
            return Results.Ok();
        }
    }
    
    return  Results.Ok();
});

app.Run();