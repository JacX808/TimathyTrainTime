using Microsoft.EntityFrameworkCore;
using TTT.TrainData.DataSets;

namespace TTT.Database;

// DbContext (in Infrastructure)
public sealed class TttDbContext : DbContext
{
    public DbSet<MovementEvent> MovementEvents => Set<MovementEvent>();
    public DbSet<CurrentTrainPosition> CurrentPositions => Set<CurrentTrainPosition>();
    public DbSet<Location> Locations => Set<Location>();

    public TttDbContext(DbContextOptions<TttDbContext> options) : base(options) {}
    
    public DbSet<TrainRun> TrainRuns => Set<TrainRun>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<MovementEvent>()
            .HasIndex(e => new { e.TrainId, e.ActualTimestampMs, e.LocStanox, e.EventType })
            .IsUnique();

        b.Entity<CurrentTrainPosition>()
            .HasKey(p => p.TrainId);

        b.Entity<Location>()
            .HasKey(l => l.Stanox);
    }
    
    public sealed class TrainRun
    {
        public int Id { get; set; }                  
        public string TrainId { get; set; } = default!;
        public DateTimeOffset FirstSeenUtc { get; set; }
    }
}
