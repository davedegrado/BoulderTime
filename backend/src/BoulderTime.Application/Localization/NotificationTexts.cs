using BoulderTime.Domain.Notifications;

namespace BoulderTime.Application.Localization;

/// <summary>Title and body of a notification, already in one reader's language.</summary>
public readonly record struct NotificationText(string Title, string? Body);

/// <summary>
/// Notification wording in every supported language. A notification is stored per recipient, so each row is written
/// in that person's language: an Italian climber reads Italian even if the staff member who acted uses English.
/// </summary>
public static class NotificationTexts
{
    private static bool It(string language) => Language.Normalize(language) == Language.Italian;

    public static string Plural(string language, int count, string itOne, string itMany, string enOne, string enMany) =>
        It(language) ? (count == 1 ? itOne : itMany) : (count == 1 ? enOne : enMany);

    public static NotificationText Announcement(string language, string gymName, AnnouncementType type, string title, string content, string? sectorName)
    {
        var prefix = type switch
        {
            AnnouncementType.Event => It(language) ? "Evento" : "Event",
            AnnouncementType.Competition => It(language) ? "Gara" : "Competition",
            AnnouncementType.ScheduleChange => It(language) ? "Cambio orari" : "Schedule change",
            AnnouncementType.Maintenance => It(language) ? "Manutenzione" : "Maintenance",
            _ => null,
        };
        var head = prefix is null ? title : $"{prefix} · {title}";
        var body = sectorName is null ? content : $"{sectorName} · {content}";
        return new NotificationText($"{gymName}: {head}", body);
    }

    public static NotificationText SectorRetraced(string language, string gymName, string sectorName, int removed) => It(language)
        ? new($"Il settore {sectorName} è stato ritracciato",
            $"{gymName} ha rimosso {removed} {(removed == 1 ? "blocco" : "blocchi")}. Presto arrivano quelli nuovi.")
        : new($"Sector {sectorName} has been retraced",
            $"{gymName} removed {removed} {(removed == 1 ? "boulder" : "boulders")}. Fresh problems are on the way.");

    public static NotificationText NewBoulderInSector(string language, string gymName, string sectorName, string summary, int count) => It(language)
        ? new(count == 1 ? $"Nuovo blocco in {sectorName}" : $"{count} nuovi blocchi in {sectorName}",
            count == 1 ? $"{gymName} · {summary}" : $"{gymName} · ultimo: {summary}")
        : new(count == 1 ? $"New boulder in {sectorName}" : $"{count} new boulders in {sectorName}",
            count == 1 ? $"{gymName} · {summary}" : $"{gymName} · latest: {summary}");

    public static NotificationText NewBoulderAtGym(string language, string gymName, string sectorName, string summary, int count) => It(language)
        ? new(count == 1 ? $"Nuovo blocco a {gymName}" : $"{count} nuovi blocchi a {gymName}",
            count == 1 ? $"{sectorName} · {summary}" : $"Ultimo in {sectorName}: {summary}")
        : new(count == 1 ? $"New boulder at {gymName}" : $"{count} new boulders at {gymName}",
            count == 1 ? $"{sectorName} · {summary}" : $"Latest in {sectorName}: {summary}");

    /// <summary>What changed on a boulder, as codes so each reader gets their own language.</summary>
    public enum BoulderChange { Grade, Sector, HoldColor, Photo }

    public static NotificationText BoulderUpdated(string language, string gymName, string sectorName, IReadOnlyList<BoulderChange> changes)
    {
        var words = changes.Select(c => (It(language), c) switch
        {
            (true, BoulderChange.Grade) => "grado cambiato",
            (true, BoulderChange.Sector) => "spostato in un altro settore",
            (true, BoulderChange.HoldColor) => "colore delle prese corretto",
            (true, _) => "foto nuova",
            (false, BoulderChange.Grade) => "grade changed",
            (false, BoulderChange.Sector) => "moved to another sector",
            (false, BoulderChange.HoldColor) => "hold colour corrected",
            _ => "new photo",
        });
        var list = string.Join(", ", words);
        return It(language)
            ? new("Un blocco che segui è stato aggiornato", $"{sectorName} · {gymName} — {list}")
            : new("A boulder you follow was updated", $"{sectorName} · {gymName} — {list}");
    }

    public static NotificationText OfficialBeta(string language, string gymName, string sectorName) => It(language)
        ? new("Nuova beta ufficiale", $"{gymName} ha pubblicato la beta di un blocco in {sectorName}.")
        : new("New official beta", $"{gymName} posted beta for a boulder in {sectorName}.");

    public static NotificationText Comment(string language, string authorName, string excerpt, int count) => It(language)
        ? new(count == 1 ? $"{authorName} ha commentato un blocco che segui" : $"{count} nuovi commenti su un blocco che segui", excerpt)
        : new(count == 1 ? $"{authorName} commented on a boulder you follow" : $"{count} new comments on a boulder you follow", excerpt);

    public static NotificationText VideoApproved(string language) => It(language)
        ? new("Il tuo video è online", "La palestra ha approvato il tuo video. Ora possono vederlo tutti.")
        : new("Your video is live", "The gym approved your video. Everyone can watch it now.");

    public static NotificationText VideoRejected(string language, string? reason) => It(language)
        ? new("Il tuo video non è stato approvato", reason)
        : new("Your video wasn't approved", reason);

    public static NotificationText ReportReviewed(string language, bool resolved) => (It(language), resolved) switch
    {
        (true, true) => new("Grazie, la tua segnalazione è stata gestita", "La palestra è intervenuta sul contenuto che hai segnalato."),
        (true, false) => new("La tua segnalazione è stata esaminata", "La palestra l'ha esaminata e non ha riscontrato problemi."),
        (false, true) => new("Thanks — your report was handled", "The gym took action on the content you reported."),
        _ => new("Your report was reviewed", "The gym reviewed it and didn't find a problem."),
    };
}
