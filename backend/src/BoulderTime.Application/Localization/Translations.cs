using System.Text.RegularExpressions;

namespace BoulderTime.Application.Localization;

/// <summary>
/// Italian versions of the messages the API returns (validation, permissions, conflicts).
/// Messages are written in English at the point they occur and translated once, here, using the reader's language.
/// Entries are matched on the English text, with <c>{0}</c> standing for values interpolated at runtime
/// (numbers, names, limits). Anything without an entry is returned unchanged, so a missing translation degrades to
/// English instead of breaking.
/// </summary>
public static partial class Translations
{
    public static string Translate(string? message, string language)
    {
        if (string.IsNullOrEmpty(message) || Language.Normalize(language) != Language.Italian) return message ?? string.Empty;
        if (Exact.TryGetValue(message, out var exact)) return exact;
        foreach (var (pattern, italian) in Patterns)
        {
            var match = pattern.Match(message);
            if (!match.Success) continue;
            var args = match.Groups.Cast<Group>().Skip(1).Select(g => (object)g.Value).ToArray();
            return string.Format(italian, args);
        }
        return message;
    }

    /// <summary>English → Italian. Use {0}, {1} for runtime values; the English side is a template, not a regex.</summary>
    private static readonly (string English, string Italian)[] Catalog =
    [
        // ---- Problem titles ----
        ("Invalid request", "Richiesta non valida"),
        ("Unauthorized", "Accesso non effettuato"),
        ("Forbidden", "Operazione non consentita"),
        ("Not found", "Non trovato"),
        ("Conflict", "Conflitto"),
        ("Server error", "Errore del server"),
        ("Something went wrong on our side.", "C'è stato un problema da parte nostra."),
        ("This already exists.", "Esiste già."),
        ("One or more fields are invalid.", "Uno o più campi non sono validi."),

        // ---- Access ----
        ("Sign in to continue.", "Accedi per continuare."),
        ("You don't have permission to do that.", "Non hai i permessi per farlo."),
        ("Only BoulderTime administrators can do this.", "Solo gli amministratori di BoulderTime possono farlo."),
        ("Only this gym's staff can do this.", "Solo lo staff di questa palestra può farlo."),
        ("Only staff can browse removed boulders.", "Solo lo staff può vedere i blocchi rimossi."),
        ("This needs the {0} role at this gym.", "Serve il ruolo {0} in questa palestra."),
        ("This sign-in method didn't provide an email address.", "Questo metodo di accesso non ha fornito un indirizzo email."),

        // ---- Profile ----
        ("Use at least {0} characters.", "Usa almeno {0} caratteri."),
        ("Use at least 2 characters.", "Usa almeno 2 caratteri."),
        ("Keep it to {0} characters or fewer.", "Non superare {0} caratteri."),
        ("Enter a name.", "Inserisci un nome."),
        ("Choose a language BoulderTime speaks.", "Scegli una lingua supportata da BoulderTime."),

        // ---- Gyms ----
        ("Enter the gym's name.", "Inserisci il nome della palestra."),
        ("Enter the city.", "Inserisci la città."),
        ("Enter a valid email address.", "Inserisci un indirizzo email valido."),
        ("Enter a full web address starting with https://", "Inserisci un indirizzo web completo che inizi con https://"),
        ("Enter a shorter phone number.", "Inserisci un numero di telefono più corto."),
        ("Set both latitude and longitude.", "Imposta sia latitudine che longitudine."),
        ("Latitude must be between -90 and 90.", "La latitudine deve essere tra -90 e 90."),
        ("Longitude must be between -180 and 180.", "La longitudine deve essere tra -180 e 180."),
        ("Invalid map bounds.", "Area della mappa non valida."),
        ("Add the gym's address first.", "Inserisci prima l'indirizzo della palestra."),
        ("This gym already has a sector with that name.", "Questa palestra ha già un settore con quel nome."),
        ("Enter a sector name.", "Inserisci il nome del settore."),
        ("Choose a sector.", "Scegli un settore."),
        ("Choose a sector of this gym.", "Scegli un settore di questa palestra."),
        ("Choose an active sector of this gym.", "Scegli un settore attivo di questa palestra."),
        ("List every sector of this gym exactly once.", "Elenca ogni settore di questa palestra una sola volta."),
        ("Choose a status.", "Scegli uno stato."),

        // ---- Staff & invitations ----
        ("Choose a role.", "Scegli un ruolo."),
        ("This person is already on the staff.", "Questa persona fa già parte dello staff."),
        ("You can't invite someone with that role.", "Non puoi invitare qualcuno con quel ruolo."),
        ("You can't change this person's role.", "Non puoi cambiare il ruolo di questa persona."),
        ("You can't remove this person.", "Non puoi rimuovere questa persona."),
        ("You can't revoke this invitation.", "Non puoi annullare questo invito."),
        ("This invitation is no longer open.", "Questo invito non è più valido."),
        ("This invitation has expired or was already answered.", "Questo invito è scaduto o ha già avuto risposta."),
        ("A gym needs at least one owner. Make someone else an owner first.", "Una palestra deve avere almeno un proprietario. Nominane un altro prima."),
        ("The setter must be on this gym's staff.", "Il tracciatore deve far parte dello staff di questa palestra."),

        // ---- Gym suggestions ----
        ("You already have {0} open suggestions. We'll get to them soon.", "Hai già {0} segnalazioni aperte. Le esamineremo presto."),

        // ---- Grading ----
        ("Choose a grading type.", "Scegli un tipo di scala."),
        ("This gym already has a grading system with that name.", "Questa palestra ha già una scala con quel nome."),
        ("List every grading system of this gym exactly once.", "Elenca ogni scala di questa palestra una sola volta."),
        ("Each grade needs a label of 1–{0} characters.", "Ogni grado deve avere un'etichetta da 1 a {0} caratteri."),
        ("Grade labels must be unique.", "Le etichette dei gradi devono essere diverse tra loro."),
        ("Every colour grade needs a colour like #F5C400.", "Ogni grado a colori richiede un colore, ad esempio #F5C400."),
        ("Use at most {0} grades.", "Usa al massimo {0} gradi."),
        ("Add at least one grade.", "Aggiungi almeno un grado."),
        ("Pick one grade per grading system.", "Scegli un grado per ogni scala."),
        ("Use active grades from this gym's grading systems.", "Usa gradi attivi delle scale di questa palestra."),
        ("Pick a grade from this gym's grading systems.", "Scegli un grado tra le scale di questa palestra."),

        // ---- Boulders ----
        ("Add a photo of the boulder.", "Aggiungi una foto del blocco."),
        ("Choose the hold colour.", "Scegli il colore delle prese."),
        ("Give the boulder at least one official grade.", "Assegna al blocco almeno un grado ufficiale."),
        ("Select at least one boulder.", "Seleziona almeno un blocco."),
        ("Remove at most {0} boulders at once.", "Rimuovi al massimo {0} blocchi alla volta."),
        ("Some selected boulders don't belong to this gym.", "Alcuni blocchi selezionati non appartengono a questa palestra."),
        ("Its sector is hidden. Show the sector again before restoring this boulder.", "Il suo settore è nascosto. Rendi di nuovo visibile il settore prima di ripristinare il blocco."),
        ("Upload a JPEG, PNG or WebP photo.", "Carica una foto JPEG, PNG o WebP."),
        ("Upload a JPEG, PNG or WebP image.", "Carica un'immagine JPEG, PNG o WebP."),
        ("Photos must be under {0} MB.", "Le foto devono pesare meno di {0} MB."),
        ("Images must be under {0} MB.", "Le immagini devono pesare meno di {0} MB."),
        ("Thumbnails must be under {0} KB.", "Le miniature devono pesare meno di {0} KB."),
        ("Thumbnails must be under {0} MB.", "Le miniature devono pesare meno di {0} MB."),
        ("Thumbnails must be JPEG or WebP.", "Le miniature devono essere JPEG o WebP."),
        ("The photo upload didn't complete. Upload it again.", "Il caricamento della foto non è andato a buon fine. Riprova."),
        ("The image upload didn't complete. Upload it again.", "Il caricamento dell'immagine non è andato a buon fine. Riprova."),
        ("The video upload didn't complete. Upload it again.", "Il caricamento del video non è andato a buon fine. Riprova."),
        ("The thumbnail upload didn't complete. Upload the video again.", "Il caricamento della miniatura non è andato a buon fine. Ricarica il video."),

        // ---- Climbing ----
        ("Attempts must be between 0 and {0}.", "I tentativi devono essere tra 0 e {0}."),
        ("Say whether you completed it.", "Indica se l'hai completato."),
        ("Rate from 1 to 5 stars.", "Vota da 1 a 5 stelle."),
        ("Try the boulder before rating it.", "Prova il blocco prima di votarlo."),
        ("Try the boulder before suggesting a grade.", "Prova il blocco prima di proporre un grado."),
        ("You already suggested a grade in this system.", "Hai già proposto un grado per questa scala."),
        ("Your progress was updated from another device. Refresh and try again.", "I tuoi progressi sono stati aggiornati da un altro dispositivo. Ricarica e riprova."),
        ("Unknown leaderboard.", "Classifica sconosciuta."),

        // ---- Community ----
        ("Write something first.", "Scrivi prima qualcosa."),
        ("You can only edit your own comments.", "Puoi modificare solo i tuoi commenti."),
        ("You can only delete your own comments. Staff can hide comments instead.", "Puoi eliminare solo i tuoi commenti. Lo staff può invece nasconderli."),
        ("Upload an MP4, MOV or WebM video.", "Carica un video MP4, MOV o WebM."),
        ("Videos must be under {0} MB.", "I video devono pesare meno di {0} MB."),
        ("Unknown upload kind.", "Tipo di caricamento sconosciuto."),
        ("Keep the caption to {0} characters or fewer.", "Non superare {0} caratteri nella didascalia."),
        ("You can post up to {0} videos per boulder. Delete one to add another.", "Puoi pubblicare fino a {0} video per blocco. Eliminane uno per aggiungerne un altro."),
        ("You can't review your own video. Another staff member has to.", "Non puoi revisionare un tuo video: deve farlo un altro membro dello staff."),
        ("You can't moderate your own video.", "Non puoi moderare un tuo video."),
        ("Tell the climber why, so they can fix it.", "Spiega il motivo, così chi l'ha caricato può rimediare."),
        ("Removed after a report.", "Rimosso in seguito a una segnalazione."),
        ("Choose what you're reporting.", "Scegli cosa stai segnalando."),
        ("Choose a reason.", "Scegli un motivo."),
        ("Tell us a bit more about the problem.", "Spiega un po' meglio il problema."),
        ("You've already reported this. The gym will review it.", "L'hai già segnalato. La palestra lo esaminerà."),
        ("This report was already handled.", "Questa segnalazione è già stata gestita."),
        ("Keep the note to {0} characters or fewer.", "Non superare {0} caratteri nella nota."),
        ("Boulders can't be removed from a report. Edit or remove the boulder in the staff area.", "I blocchi non si rimuovono da una segnalazione. Modificalo o rimuovilo dall'area staff."),

        // ---- Announcements & notifications settings ----
        ("Choose a type.", "Scegli un tipo."),
        ("Write a title.", "Scrivi un titolo."),
        ("Write the announcement.", "Scrivi il testo dell'annuncio."),
        ("Keep the title to {0} characters or fewer.", "Non superare {0} caratteri nel titolo."),
        ("Events and competitions need a date.", "Eventi e gare richiedono una data."),
        ("Choose logo or cover.", "Scegli logo o copertina."),
        ("Settings changed on another device. Refresh and try again.", "Le impostazioni sono cambiate su un altro dispositivo. Ricarica e riprova."),
    ];

    private static readonly Dictionary<string, string> Exact =
        Catalog.Where(e => !e.English.Contains("{0}")).ToDictionary(e => e.English, e => e.Italian);

    private static readonly (Regex Pattern, string Italian)[] Patterns = Catalog
        .Where(e => e.English.Contains("{0}"))
        .Select(e => (BuildPattern(e.English), e.Italian))
        .ToArray();

    /// <summary>Turns "Keep it to {0} characters or fewer." into a regex that captures the runtime value.</summary>
    private static Regex BuildPattern(string template)
    {
        var escaped = Regex.Escape(template);
        for (var i = 0; i < 3; i++) escaped = escaped.Replace(Regex.Escape($"{{{i}}}"), "(.+?)");
        return new Regex($"^{escaped}$", RegexOptions.Compiled | RegexOptions.Singleline);
    }
}
