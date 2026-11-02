using Microsoft.EntityFrameworkCore;
using PuzzleWebApp.Models;

namespace PuzzleWebApp.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Crossword> Crosswords => Set<Crossword>();
    public DbSet<CrosswordClue> CrosswordClues => Set<CrosswordClue>();
    public DbSet<CrosswordAttempt> CrosswordAttempts => Set<CrosswordAttempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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