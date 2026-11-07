using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace PuzzleWebApp.Data;

public static class RoundQuestionSchema
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
            using var createTable = connection.CreateCommand();
            createTable.CommandText = """
                CREATE TABLE IF NOT EXISTS "RoundQuestions" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_RoundQuestions" PRIMARY KEY AUTOINCREMENT,
                    "RoundNumber" INTEGER NOT NULL,
                    "Title" TEXT NOT NULL,
                    "QuestionText" TEXT NOT NULL,
                    "AnswerGuidance" TEXT NOT NULL
                )
                """;
            createTable.ExecuteNonQuery();

            using var createIndex = connection.CreateCommand();
            createIndex.CommandText = """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_RoundQuestions_RoundNumber"
                ON "RoundQuestions" ("RoundNumber")
                """;
            createIndex.ExecuteNonQuery();
        }
        finally
        {
            if (wasClosed)
            {
                connection.Close();
            }
        }
    }
}