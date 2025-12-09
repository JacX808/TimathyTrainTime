using Microsoft.EntityFrameworkCore;
using TTT.DataSets;

namespace TTT.Database;

public sealed class TttDbContext : DbContext
{
    public TttDbContext(DbContextOptions<TttDbContext> options) : base(options) { }

    public DbSet<TrainRun> TrainRuns => Set<TrainRun>();
    public DbSet<MovementEvent> MovementEvents => Set<MovementEvent>();
    public DbSet<CurrentTrainPosition> CurrentPositions => Set<CurrentTrainPosition>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<TrainRun>().HasKey(x => x.TrainId); // natural key
        b.Entity<TrainRun>().Property(x => x.TrainId).HasMaxLength(32);

        b.Entity<MovementEvent>().HasKey(x => x.Id);
        b.Entity<MovementEvent>().HasIndex(x => new { x.TrainId, x.ActualTimestampMs, x.LocStanox, x.EventType }).IsUnique();
        b.Entity<MovementEvent>().Property(x => x.TrainId).HasMaxLength(32);
        b.Entity<MovementEvent>().Property(x => x.EventType).HasMaxLength(16);
        b.Entity<MovementEvent>().Property(x => x.LocStanox).HasMaxLength(16);

        b.Entity<CurrentTrainPosition>().HasKey(x => x.TrainId);
        b.Entity<CurrentTrainPosition>().Property(x => x.TrainId).HasMaxLength(32);
        b.Entity<CurrentTrainPosition>().Property(x => x.LocStanox).HasMaxLength(16);
    }
}
