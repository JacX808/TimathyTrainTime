using System.Collections.Concurrent;
using System.Threading.Channels;

namespace TTT.TrainData.Connection;

public sealed record TrainMovementEvent(
    string TrainId,
    string EventType,
    string Stanox,
    DateTimeOffset ReportedAt,
    string? Platform,
    string? VariationStatus,
    string? Direction,
    string? Line,
    string? NextReportStanox,
    string? TocId
);

public sealed class LiveMovementCache
{
    private readonly ConcurrentDictionary<string, TrainMovementEvent> _latest = new();
    private readonly Channel<TrainMovementEvent> _stream = Channel.CreateUnbounded<TrainMovementEvent>();

    public void Upsert(TrainMovementEvent e)
    {
        _latest[e.TrainId] = e;
        _stream.Writer.TryWrite(e);
    }

    public IReadOnlyList<TrainMovementEvent> Latest(int take = 200)
        => _latest.Values.OrderByDescending(x => x.ReportedAt).Take(take).ToList();

    public IAsyncEnumerable<TrainMovementEvent> ReadStream(CancellationToken ct)
        => _stream.Reader.ReadAllAsync(ct);
}
