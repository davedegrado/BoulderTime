using BoulderTime.Application.Community;
using BoulderTime.Domain.Community;
using BoulderTime.Domain.Users;

namespace BoulderTime.Application.Abstractions;

/// <summary>The wording of the moderation emails. In Italian: they go to the BoulderTime team only.</summary>
public static class ModerationAlerts
{
    public static ModerationAlert ForContent(ReportDto r)
    {
        var what = r.EntityType == ReportEntityType.Video ? "un video" : "un commento";
        var body = $"""
            Nuova segnalazione su {what} nella palestra {r.GymName}.

            Motivo: {Reason(r.Reason)}
            {(string.IsNullOrWhiteSpace(r.Description) ? "" : $"Dettagli di chi segnala: {r.Description}\n")}{(string.IsNullOrWhiteSpace(r.Excerpt) ? "" : $"Contenuto: «{r.Excerpt}»\n")}Segnalato da: {r.ReportedBy.DisplayName}

            La vede anche lo staff della palestra. Per gestirla:
            """;
        return new ModerationAlert($"Segnalazione: {what} a {r.GymName}", body, "/admin/reports");
    }

    public static ModerationAlert ForPerson(string reportedName, string reporterName, UserReportReason reason, string? description)
    {
        var body = $"""
            Nuova segnalazione di una persona: {reportedName}.

            Motivo: {Reason(reason)}
            {(string.IsNullOrWhiteSpace(description) ? "" : $"Dettagli di chi segnala: {description}\n")}Segnalato da: {reporterName}

            Per gestirla:
            """;
        return new ModerationAlert($"Segnalazione: {reportedName}", body, "/admin/reports");
    }

    private static string Reason(ReportReason reason) => reason switch
    {
        ReportReason.Inappropriate => "contenuto inappropriato",
        ReportReason.Spam => "spam",
        ReportReason.Misleading => "fuorviante",
        ReportReason.WrongBoulder => "blocco sbagliato",
        _ => "altro",
    };

    private static string Reason(UserReportReason reason) => reason switch
    {
        UserReportReason.Harassment => "molestie",
        UserReportReason.Spam => "spam",
        UserReportReason.Impersonation => "si spaccia per qualcun altro",
        UserReportReason.InappropriateProfile => "profilo inappropriato",
        _ => "altro",
    };
}
