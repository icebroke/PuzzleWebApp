using PuzzleWebApp.Models;

namespace PuzzleWebApp.Data;

public static class RoundQuestionSeed
{
    public static void EnsureSeeded(AppDbContext db)
    {
        const int roundNumber = 2;
        var question = db.RoundQuestions.FirstOrDefault(item => item.RoundNumber == roundNumber);
        if (question is null)
        {
            question = new RoundQuestion { RoundNumber = roundNumber };
            db.RoundQuestions.Add(question);
        }

        question.Title = "Bahagian B: Cabaran Kreativiti Integriti";
        question.QuestionText = "Bayangkan anda berpeluang menyampaikan satu pesanan kepada seluruh warga Majlis Daerah Hulu Terengganu (MDHT) yang mampu menyentuh hati, membuka mata dan memberi inspirasi untuk mengamalkan integriti. Apakah pesanan anda? Sampaikan dengan cara yang paling kreatif dan bermakna.";
        question.AnswerGuidance = "Jawapan mestilah hasil idea sendiri.\nBoleh disampaikan dalam bentuk slogan, pantun, kata-kata inspirasi, analogi atau pesanan ringkas.\nJawapan mestilah berkaitan integriti dalam persekitaran kerja.";

        db.SaveChanges();
    }
}