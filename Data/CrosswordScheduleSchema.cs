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
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA table_info(\"Crosswords\")";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    columns.Add(reader.GetString(1));
                }
            }

            AddColumnIfMissing(connection, columns, "ScheduledStartAtUtc", "TEXT NULL");
            AddColumnIfMissing(connection, columns, "ScheduledEndAtUtc", "TEXT NULL");
            AddColumnIfMissing(connection, columns, "ManualActivation", "INTEGER NOT NULL DEFAULT 0");
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
        ISet<string> columns,
        string columnName,
        string columnDefinition)
    {
        if (columns.Contains(columnName))
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.CommandText = $"ALTER TABLE \"Crosswords\" ADD COLUMN \"{columnName}\" {columnDefinition}";
        command.ExecuteNonQuery();
    }
}