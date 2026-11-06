using Microsoft.EntityFrameworkCore;
using PuzzleWebApp.Models;

namespace PuzzleWebApp.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Crossword> Crosswords => Set<Crossword>();
    public DbSet<CrosswordClue> CrosswordClues => Set<CrosswordClue>();
    public DbSet<CrosswordAttempt> CrosswordAttempts => Set<CrosswordAttempt>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdminUser>()
            .Property(user => user.Username)
            .HasMaxLength(100);

        modelBuilder.Entity<AdminUser>()
            .Property(user => user.NormalizedUsername)
            .HasMaxLength(100);

        modelBuilder.Entity<AdminUser>()
            .Property(user => user.PasswordHash)
            .HasMaxLength(500);

        modelBuilder.Entity<AdminUser>()
            .HasIndex(user => user.NormalizedUsername)
            .IsUnique();

        modelBuilder.Entity<Crossword>()
            .Property(crossword => crossword.Title)
            .HasMaxLength(160);

        modelBuilder.Entity<CrosswordClue>()
            .Property(clue => clue.Direction)
            .HasConversion<string>();

        modelBuilder.Entity<CrosswordClue>()
            .HasIndex(clue => new { clue.CrosswordId, clue.Number, clue.Direction })
            .IsUnique();

        modelBuilder.Entity<CrosswordClue>()
            .HasOne(clue => clue.Crossword)
            .WithMany(crossword => crossword.Clues)
            .HasForeignKey(clue => clue.CrosswordId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CrosswordAttempt>()
            .Property(attempt => attempt.PlayerId)
            .HasMaxLength(80);

        modelBuilder.Entity<CrosswordAttempt>()
            .Property(attempt => attempt.PlayerName)
            .HasMaxLength(100);

        modelBuilder.Entity<CrosswordAttempt>()
            .HasIndex(attempt => new { attempt.CrosswordId, attempt.PlayerId })
            .IsUnique();

        modelBuilder.Entity<CrosswordAttempt>()
            .HasOne(attempt => attempt.Crossword)
            .WithMany(crossword => crossword.Attempts)
            .HasForeignKey(attempt => attempt.CrosswordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}