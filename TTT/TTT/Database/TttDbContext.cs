using Microsoft.EntityFrameworkCore;
using TTT.DataSets;
using TTT.DataSets.RailLocations;
using TTT.DataSets.Train;
using TTT.DataSets.TrainAndRail;

namespace TTT.Database;

public sealed class TttDbContext(DbContextOptions<TttDbContext> options, DbConfig dbConfig) : DbContext(options)
{
    public DbSet<TrainRun> TrainRuns => Set<TrainRun>();
    public DbSet<MovementEvent> MovementEvents => Set<MovementEvent>();
    public DbSet<CurrentTrainPosition> CurrentTrainPosition => Set<CurrentTrainPosition>();
    public DbSet<RailLocations> RailLocations => Set<RailLocations>();
    public DbSet<RailLocationLite> RailLocationLite => Set<RailLocationLite>();
    public DbSet<TrainAndRailMergeLite>  TrainAndRailMergeLite => Set<TrainAndRailMergeLite>();
    public DbSet<TrainMinimumData>  TrainMinimumData => Set<TrainMinimumData>();

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

        modelBuilder.Entity<MovementEvent>().HasKey(movementEvent => movementEvent.TrainId);
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

        modelBuilder.Entity<RailLocationLite>(entityTypeBuilder =>
        {
            entityTypeBuilder.ToTable("RailLocationLite");
            entityTypeBuilder.HasKey(lite => lite.Id);
            entityTypeBuilder.Property(lite => lite.Id).ValueGeneratedOnAdd();
            entityTypeBuilder.Property(lite => lite.Stanox).IsRequired().HasMaxLength(5).HasColumnType("char(5)"); 
            entityTypeBuilder.Property(lite => lite.Latitude).HasColumnType("double");
            entityTypeBuilder.Property(lite => lite.Longitude).HasColumnType("double");
            entityTypeBuilder.HasIndex(lite => lite.Stanox).IsUnique();
            entityTypeBuilder.HasIndex(lite => new { lite.Latitude, lite.Longitude });
        });
        
        modelBuilder.Entity<RailLocations>(entityTypeBuilder =>
        {
            entityTypeBuilder.ToTable("RailLocations");

            entityTypeBuilder.HasKey(railLocation => railLocation.Id);

            entityTypeBuilder.Property(railLocation => railLocation.Stanox).IsRequired().HasMaxLength(5)
                .HasColumnType("char(5)");
            entityTypeBuilder.Property(railLocation => railLocation.Tiploc).IsRequired().HasMaxLength(7)
                .HasColumnType("char(7)");
            entityTypeBuilder.Property(railLocation => railLocation.Name).HasMaxLength(32);
            entityTypeBuilder.Property(railLocation => railLocation.Source).IsRequired().HasMaxLength(32);

            entityTypeBuilder.HasIndex(x => new { x.Stanox, x.Tiploc }).IsUnique();
            entityTypeBuilder.HasIndex(x => x.Stanox);
            entityTypeBuilder.HasIndex(x => x.Tiploc);
        });

        modelBuilder.Entity<TrainAndRailMergeLite>(entityTypeBuilder =>
        {
            entityTypeBuilder.HasKey(lite => new { lite.Id });
            entityTypeBuilder.HasIndex(lite => new { lite.TrainId, lite.LocStanox });
            entityTypeBuilder.Property(lite => lite.TrainId).HasMaxLength(32);
            entityTypeBuilder.Property(lite => lite.LocStanox).HasMaxLength(5);
            entityTypeBuilder.Property(lite => lite.Direction).HasMaxLength(4);
        });

        modelBuilder.Entity<TrainMinimumData>(entityTypeBuilder =>
        {
            entityTypeBuilder.ToTable("TrainMinimumData");

            entityTypeBuilder.HasKey(x => x.TrainId);

            entityTypeBuilder.Property(x => x.TrainId).IsRequired().HasMaxLength(32)
                .HasColumnType("varchar(32)");

            entityTypeBuilder.Property(x => x.LocStanox).IsRequired().HasMaxLength(5)
                .HasColumnType("char(5)");
            
            entityTypeBuilder.Property(x => x.NextLocStanox).IsRequired().HasMaxLength(5)
                .HasColumnType("char(5)");
            
            entityTypeBuilder.Property(x => x.LastSeenUtc).IsRequired().HasColumnType("datetime(6)");

            entityTypeBuilder.Property(x => x.VariationStatus).HasMaxLength(32)
                .HasColumnType("varchar(32)");
        });
    }
}
