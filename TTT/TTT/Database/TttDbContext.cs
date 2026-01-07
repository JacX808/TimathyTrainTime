using Microsoft.EntityFrameworkCore;
using TTT.DataSets;
using TTT.DataSets.RailLocations;
using TTT.DataSets.TrainAndRail;

namespace TTT.Database;

public sealed class TttDbContext(DbContextOptions<TttDbContext> options, DbConfig dbConfig) : DbContext(options)
{
    public DbSet<TrainRun> TrainRuns => Set<TrainRun>();
    public DbSet<MovementEvent> MovementEvents => Set<MovementEvent>();
    public DbSet<CurrentTrainPosition> CurrentTrainPosition => Set<CurrentTrainPosition>();
    public DbSet<RailLocation> RailLocations => Set<RailLocation>();
    public DbSet<RailLocationLite> RailLocationLite => Set<RailLocationLite>();
    public DbSet<TrainAndRailMergeLite>  TrainAndRailMergeLite => Set<TrainAndRailMergeLite>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (dbConfig.DatabaseName.Equals("test"))
        {
            optionsBuilder.UseInMemoryDatabase("TestDatabase");
            return;
        }
        
        optionsBuilder.UseMySQL(
            $"server={dbConfig.Host}," +
            $"{dbConfig.Port};" +
            $"database={dbConfig.DatabaseName};" +
            $"user id={dbConfig.UserName};" +
            $"password={dbConfig.Password};");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TrainRun>().HasKey(movementEvent => movementEvent.TrainId); // natural key
        modelBuilder.Entity<TrainRun>().Property(movementEvent => movementEvent.TrainId).HasMaxLength(32);

        modelBuilder.Entity<MovementEvent>().HasKey(movementEvent => movementEvent.Id);
        modelBuilder.Entity<MovementEvent>().HasIndex(movementEvent =>
            new
            {
                movementEvent.TrainId, movementEvent.ActualTimestampMs, movementEvent.LocStanox,
                movementEvent.EventType
            }).IsUnique();

        modelBuilder.Entity<MovementEvent>().Property(movementEvent => movementEvent.TrainId).HasMaxLength(32);
        modelBuilder.Entity<MovementEvent>().Property(movementEvent => movementEvent.EventType).HasMaxLength(16);
        modelBuilder.Entity<MovementEvent>().Property(movementEvent => movementEvent.LocStanox).HasMaxLength(16);

        modelBuilder.Entity<CurrentTrainPosition>().HasKey(x => x.TrainId);
        modelBuilder.Entity<CurrentTrainPosition>().Property(x => x.TrainId).HasMaxLength(32);
        modelBuilder.Entity<CurrentTrainPosition>().Property(x => x.LocStanox).HasMaxLength(16);

        modelBuilder.Entity<RailLocation>(entityTypeBuilder =>
        {
            entityTypeBuilder.HasIndex(railLocation => new { railLocation.Stanox, railLocation.Tiploc }).IsUnique();
            entityTypeBuilder.HasIndex(railLocation => railLocation.Stanox);
            entityTypeBuilder.HasIndex(railLocation => railLocation.Tiploc);
        });

        modelBuilder.Entity<RailLocationLite>(entityTypeBuilder =>
        {
            entityTypeBuilder.HasIndex(lite => new { lite.Stanox }).IsUnique();
            entityTypeBuilder.Property(lite => lite.Stanox).HasMaxLength(5);
        });

        modelBuilder.Entity<TrainAndRailMergeLite>(entityTypeBuilder =>
        {
            entityTypeBuilder.HasIndex(lite => new { lite.TrainId, lite.LocStanox }).IsUnique();
            entityTypeBuilder.Property(lite => lite.TrainId).HasMaxLength(32);
            entityTypeBuilder.Property(lite => lite.LocStanox).HasMaxLength(5);
            entityTypeBuilder.Property(lite => lite.Direction).HasMaxLength(4);
        });
    }
}
