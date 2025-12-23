using Microsoft.EntityFrameworkCore;
using TTT.TrainData.DataSets;

namespace TTT.TrainData.Database;

public sealed class TttDbContext(DbContextOptions<TttDbContext> options, DbConfig dbConfig) : DbContext(options)
{
    public DbSet<TrainRun> TrainRuns => Set<TrainRun>();
    public DbSet<MovementEvent> MovementEvents => Set<MovementEvent>();
    public DbSet<CurrentTrainPosition> CurrentTrainPosition => Set<CurrentTrainPosition>();

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
