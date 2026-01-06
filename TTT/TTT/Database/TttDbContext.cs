using Microsoft.EntityFrameworkCore;
using TTT.DataSets;
using TTT.DataSets.RailLocations;

namespace TTT.Database;

public sealed class TttDbContext(DbContextOptions<TttDbContext> options, DbConfig dbConfig) : DbContext(options)
{
    public DbSet<TrainRun> TrainRuns => Set<TrainRun>();
    public DbSet<MovementEvent> MovementEvents => Set<MovementEvent>();
    public DbSet<CurrentTrainPosition> CurrentTrainPosition => Set<CurrentTrainPosition>();
    public DbSet<RailLocation> RailLocations => Set<RailLocation>();

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
            new { movementEvent.TrainId, movementEvent.ActualTimestampMs, movementEvent.LocStanox, 
                movementEvent.EventType }).IsUnique();
        
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
    }
}
