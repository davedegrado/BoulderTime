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
        ("This contains words that aren't allowed here. Please rephrase it.", "Il testo contiene parole che qui non sono ammesse. Riformulalo."),

        // ---- Access ----
        ("Sign in to continue.", "Accedi per continuare."),
        ("You don't have permission to do that.", "Non hai i permessi per farlo."),
        ("Only BoulderTime administrators can do this.", "Solo gli amministratori di BoulderTime possono farlo."),
        ("Only this gym's staff can do this.", "Solo lo staff di questa palestra può farlo."),
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

        // ---- Video allowance ----
        ("Climber videos aren't available at this gym yet.", "I video dei climber non sono ancora disponibili in questa palestra."),
        ("This gym can keep {0} official beta videos. Remove one before adding another, link a video published on {1}, or ask BoulderTime for more.",
            "Questa palestra può tenere {0} beta ufficiali. Rimuovine una prima di aggiungerne un'altra, collega un video pubblicato su {1}, oppure chiedi a BoulderTime di alzare il limite."),
        ("Paste the address of a video on {0}.", "Incolla l'indirizzo di un video su {0}."),

        // ---- Boulders ----
        ("Add a photo of the boulder.", "Aggiungi una foto del blocco."),
        ("Choose the hold colour.", "Scegli il colore delle prese."),
        ("Unknown hold colour.", "Colore delle prese sconosciuto."),
        ("This boulder is no longer on the wall: it can't become a project.", "Questo blocco non è più sulla parete: non può diventare un progetto."),
        ("Choose a known intensity.", "Scegli un'intensità valida."),
        ("Use this gym's tags.", "Usa i tag di questa palestra."),
        ("Pick at most {0} tags.", "Scegli al massimo {0} tag."),

        // ---- Tags ----
        ("Give the tag a name.", "Dai un nome al tag."),
        ("Keep tag names under {0} characters.", "Il nome del tag deve avere meno di {0} caratteri."),
        ("A gym can have at most {0} tags.", "Una palestra può avere al massimo {0} tag."),
        ("There is already a tag called “{0}”.", "Esiste già un tag chiamato “{0}”."),
        ("Give the boulder at least one official grade.", "Assegna al blocco almeno un grado ufficiale."),
        ("Select at least one boulder.", "Seleziona almeno un blocco."),
        ("Remove at most {0} boulders at once.", "Rimuovi al massimo {0} blocchi alla volta."),
        ("Some selected boulders don't belong to this gym.", "Alcuni blocchi selezionati non appartengono a questa palestra."),
        ("Its sector is hidden. Show the sector again before restoring this boulder.", "Il suo settore è nascosto. Rendi di nuovo visibile il settore prima di ripristinare il blocco."),
        ("Take the boulder off the wall first, then delete it.", "Smonta prima il blocco dalla parete, poi eliminalo."),
        ("Upload a JPEG, PNG or WebP photo.", "Carica una foto JPEG, PNG o WebP."),
        ("Upload a JPEG, PNG or WebP image.", "Carica un'immagine JPEG, PNG o WebP."),
        ("Photos must be under {0} MB.", "Le foto devono pesare meno di {0} MB."),
        ("Images must be under {0} MB.", "Le immagini devono pesare meno di {0} MB."),
        ("Thumbnails must be under {0} KB.", "Le miniature devono pesare meno di {0} KB."),
        ("Thumbnails must be under {0} MB.", "Le miniature devono pesare meno di {0} MB."),
        ("Thumbnails must be JPEG or WebP.", "Le miniature devono essere JPEG o WebP."),
        ("The photo upload didn't complete. Upload it again.", "Il caricamento della foto non è andato a buon fine. Riprova."),
        ("Type the gym's slug to confirm.", "Scrivi lo slug della palestra per confermare."),
        ("Say which picture.", "Indica quale immagine."),
        ("Say whether the profile is private.", "Indica se il profilo è privato."),
        ("Unknown profile section.", "Sezione del profilo sconosciuta."),
        ("Each section can appear only once.", "Ogni sezione può comparire una sola volta."),
        ("Try the boulder before suggesting its intensity.", "Prova il blocco prima di proporne l'intensità."),
        ("Choose low, medium or high intensity.", "Scegli poco intenso, intensità media o molto intenso."),
        ("You already suggested an intensity for this boulder.", "Hai già proposto un'intensità per questo blocco."),
        ("Write the version as numbers and dots, for example 1.2.0.", "Scrivi la versione con numeri e punti, per esempio 1.2.0."),
        ("The recommended version can't be older than the minimum.", "La versione consigliata non può essere più vecchia della minima."),
        ("Use the app's https address on the App Store or Google Play.", "Usa l'indirizzo https dell'app sull'App Store o su Google Play."),
        ("The picture is too large.", "L'immagine è troppo grande."),
        ("This picture has changed meanwhile. Load the list again.", "Nel frattempo questa immagine è cambiata. Ricarica l'elenco."),
        ("The lighter copy is not lighter: the picture stays as it is.", "La copia alleggerita non pesa meno: l'immagine resta com'è."),
        ("The highlight mask must be a PNG.", "La maschera delle prese deve essere un PNG."),
        ("The highlight mask must be under {0} MB.", "La maschera delle prese deve pesare meno di {0} MB."),
        ("The highlighted holds couldn't be saved. Highlight them again.", "Non è stato possibile salvare le prese evidenziate. Evidenziale di nuovo."),
        ("That sector photo has been replaced. Choose the photo again.", "La foto di quel settore è stata sostituita. Scegli di nuovo la foto."),
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
        ("No account uses this email yet. Sign up in the app with it first.", "Nessun account usa ancora questa email. Prima registrati nell'app con questo indirizzo."),
        ("Uploading videos isn't available yet. Link a video published online instead.", "Il caricamento dei video non è ancora disponibile. Collega invece un video pubblicato online."),
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
        ("Draw the sector with 3 to 64 points.", "Disegna il settore con un numero di punti tra 3 e 64."),
        ("Keep every point on the floor plan.", "Tieni tutti i punti dentro la piantina."),
        ("Keep the label on the floor plan.", "Tieni l'etichetta dentro la piantina."),
        ("The floor plan's size is missing. Upload it again.", "Manca la dimensione della piantina. Caricala di nuovo."),
        ("Settings changed on another device. Refresh and try again.", "Le impostazioni sono cambiate su un altro dispositivo. Ricarica e riprova."),

        // ---- Reporting people & suspensions ----
        ("This account has been suspended.", "Questo account è stato sospeso."),
        ("Confirm that you're at least 14.", "Conferma di avere almeno 14 anni."),
        ("Reload the app and read the current terms.", "Ricarica l'app e leggi i termini aggiornati."),
        ("You can't report yourself.", "Non puoi segnalare te stesso."),
        ("Say whether to suspend the account.", "Indica se sospendere l'account."),
        ("You can't suspend yourself.", "Non puoi sospendere te stesso."),
        ("A BoulderTime administrator can't be suspended. Revoke the role first.", "Un amministratore di BoulderTime non può essere sospeso. Togli prima il ruolo."),

        // ---- Boulder names, points and two-colour grades ----
        ("Keep the name to {0} characters or fewer.", "Il nome può avere al massimo {0} caratteri."),
        ("Points go from 1 to {0}.", "I punti vanno da 1 a {0}."),
        ("Only colour grades can have two colours.", "Solo i gradi a colori possono avere due colori."),
        ("The two colours of a grade must be different.", "I due colori di un grado devono essere diversi."),
        ("A value doesn't belong to this grading system.", "Un grado non appartiene a questa scala."),

        // ---- Opening hours ----
        ("Days go from 1 (Monday) to 7 (Sunday).", "I giorni vanno da 1 (lunedì) a 7 (domenica)."),
        ("Give each day once.", "Indica ogni giorno una volta sola."),
        ("Use at most {0} opening times a day.", "Al massimo {0} fasce orarie al giorno."),
        ("Add at most {0} closures.", "Al massimo {0} chiusure."),
        ("A closure can't end before it starts.", "Una chiusura non può finire prima di cominciare."),
        ("Keep each note to {0} characters or fewer.", "Ogni nota può avere al massimo {0} caratteri."),
        ("Write times as HH:mm, for example 09:30.", "Scrivi gli orari come HH:mm, per esempio 09:30."),
        ("Each opening time has to close after it opens.", "Ogni fascia oraria deve chiudere dopo l'apertura."),
        ("Opening times on the same day can't overlap.", "Le fasce orarie dello stesso giorno non possono sovrapporsi."),

        // ---- Events ----
        ("Only events take names.", "Solo gli eventi raccolgono i partecipanti."),
        ("This event is over.", "Questo evento è già passato."),
        ("Follow the gym to see who is coming.", "Segui la palestra per vedere chi partecipa."),

        // ---- Messages that had no Italian yet ----
        ("Too many requests", "Troppe richieste"),
        ("Slow down for a moment and try again.", "Aspetta un momento e riprova."),
        ("A gym was already created from this suggestion.", "Da questa segnalazione è già stata creata una palestra."),
        ("Say whether this gym is the founding gym.", "Indica se questa è la palestra fondatrice."),
        ("Say whether climber videos are allowed.", "Indica se i video degli arrampicatori sono ammessi."),
        ("Give a limit of zero or more, or choose no limit.", "Indica un limite di zero o più, oppure nessun limite."),
        ("{0} is already the founding gym. Remove that first — there can only be one.", "{0} è già la palestra fondatrice. Togli prima quella: ce ne può essere una sola."),
        ("Another gym is already the founding gym.", "Un'altra palestra è già la palestra fondatrice."),
        ("Say whether to hide yourself.", "Indica se vuoi nasconderti."),
        ("Choose the climber to report.", "Scegli la persona da segnalare."),
        ("Say what looks wrong.", "Spiega cosa non ti torna."),
        ("Say whether to exclude the climber.", "Indica se escludere la persona."),
        ("Unknown kind of device.", "Tipo di dispositivo sconosciuto."),
        ("The device didn't provide a complete subscription.", "Il dispositivo non ha fornito un'iscrizione completa."),
        ("That isn't a valid push endpoint.", "Non è un indirizzo valido per le notifiche."),
        ("The app didn't provide a valid notification token.", "L'app non ha fornito un codice valido per le notifiche."),
        ("There's already an open invitation for this email.", "C'è già un invito in attesa per questa email."),
        ("Type your email address exactly to confirm.", "Per confermare, scrivi esattamente il tuo indirizzo email."),
        ("You are the only owner of a gym. Make someone else an owner first, or write to support@bouldertime.com.", "Sei l'unico proprietario di una palestra. Nomina prima un altro proprietario, oppure scrivi a support@bouldertime.com."),
        ("You can't block yourself.", "Non puoi bloccare te stesso."),
        ("You can't add yourself as a friend.", "Non puoi aggiungere te stesso agli amici."),
        ("Unblock this person to add them as a friend.", "Sblocca questa persona per aggiungerla agli amici."),
        ("You've sent a lot of friend requests today. Try again tomorrow.", "Oggi hai inviato molte richieste di amicizia. Riprova domani."),
        ("Friend request was not found.", "Richiesta di amicizia non trovata."),
        ("Keep the note under {0} characters.", "La nota deve restare sotto i {0} caratteri."),
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
