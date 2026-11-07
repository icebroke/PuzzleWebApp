using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using PuzzleWebApp.Models;

namespace PuzzleWebApp.Data;

public static class AdminUserSeed
{
    public const string DefaultUsername = "admin";
    public const string DefaultPassword = "ChangeMe123";

    public static void EnsureSeeded(AppDbContext db, IPasswordHasher<AdminUser> passwordHasher)
    {
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "AdminUsers" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_AdminUsers" PRIMARY KEY AUTOINCREMENT,
                "Username" TEXT NOT NULL,
                "NormalizedUsername" TEXT NOT NULL,
                "PasswordHash" TEXT NOT NULL,
                "MustChangePassword" INTEGER NOT NULL
            );
            """);
        db.Database.ExecuteSqlRaw("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_AdminUsers_NormalizedUsername"
            ON "AdminUsers" ("NormalizedUsername");
            """);

        if (db.AdminUsers.Any())
        {
            return;
        }

        var admin = new AdminUser
        {
            Username = DefaultUsername,
            NormalizedUsername = NormalizeUsername(DefaultUsername),
            MustChangePassword = true
        };
        admin.PasswordHash = passwordHasher.HashPassword(admin, DefaultPassword);
        db.AdminUsers.Add(admin);
        db.SaveChanges();
    }

    public static string NormalizeUsername(string username) => username.Trim().ToUpperInvariant();
}