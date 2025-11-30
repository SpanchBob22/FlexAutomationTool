using FlexAutomator.Models;
using Microsoft.EntityFrameworkCore;
namespace FlexAutomator.Data;
public class AppDbContext : DbContext
{
    private readonly string _connectionString;
    public DbSet<Scenario> Scenarios { get; set; } = null!;
    public DbSet<AppSettings> Settings { get; set; } = null!;
    public AppDbContext(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite(_connectionString);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Scenario>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.BlocksJson).IsRequired();
        });

        modelBuilder.Entity<AppSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
        });
    }
}