using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace PuzzleWebApp.Data;

public static class CrosswordScheduleSchema
{
    public static void EnsureUpToDate(DatabaseFacade database)
    {
        var connection = database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;
        if (wasClosed)
        {
            connection.Open();
        }

        try
        {
            AddColumnIfMissing(connection, "Crosswords", "ScheduledStartAtUtc", "TEXT NULL");
            AddColumnIfMissing(connection, "Crosswords", "ScheduledEndAtUtc", "TEXT NULL");
            AddColumnIfMissing(connection, "Crosswords", "ManualActivation", "INTEGER NOT NULL DEFAULT 0");
            AddColumnIfMissing(connection, "Crosswords", "RoundNumber", "INTEGER NOT NULL DEFAULT 1");
            AddColumnIfMissing(connection, "CrosswordAttempts", "CreativeAnswer", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfMissing(connection, "CrosswordAttempts", "CreativeStartedAtUtc", "TEXT NULL");
            AddColumnIfMissing(connection, "CrosswordAttempts", "CreativeSubmittedAtUtc", "TEXT NULL");
            AddColumnIfMissing(connection, "CrosswordAttempts", "CreativeJudgmentOrder", "INTEGER NULL CHECK (CreativeJudgmentOrder IS NULL OR CreativeJudgmentOrder >= 1)");
        }
        finally
        {
            if (wasClosed)
            {
                connection.Close();
            }
        }
    }

    private static void AddColumnIfMissing(
        System.Data.Common.DbConnection connection,
        string tableName,
        string columnName,
        string columnDefinition)
    {
        var exists = false;
        using (var command = connection.CreateCommand())
        {
            command.CommandText = $"PRAGMA table_info(\"{tableName}\")";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }
        }

        if (exists)
        {
            return;
        }

        using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" {columnDefinition}";
        alterCommand.ExecuteNonQuery();
    }
}