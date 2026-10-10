import type { FaqGuide } from "@/features/help/faq";

/**
 * La guida in italiano. Le risposte usano lo stesso formato dei documenti legali: paragrafi separati da una riga
 * vuota, elenchi con "- " e **grassetto**. I nomi dei pulsanti sono scritti come compaiono nell'app: se ne cambia
 * uno, va cambiato anche qui.
 */
export const FAQ_IT: FaqGuide = {
  climbers: [
    {
      id: "inizio",
      title: "Palestre e settori",
      items: [
        {
          q: "Come trovo la mia palestra?",
          a: `Apri **Esplora** e cerca per nome o città, oppure passa a **Mappa** per vedere le palestre intorno a te. Se
permetti all'app di conoscere la tua posizione, la lista parte dalla più vicina.

La tua palestra non c'è? In fondo alla lista tocca **Segnalacela**: la contattiamo noi. Le tue segnalazioni e il loro
stato le trovi nella stessa pagina.`,
        },
        {
          q: "Perché conviene seguire una palestra?",
          a: `Tocca la **campanella** in alto nella pagina della palestra. Così ricevi le novità (blocchi nuovi, avvisi,
eventi) e la palestra compare nella Home. Con il **cuore** la metti tra le preferite e resta in cima.`,
        },
        {
          q: "Come vedo i blocchi di un solo settore?",
          a: `Nella pagina della palestra apri **Settori**. Se la palestra ha caricato la piantina, tocca il settore sulla
mappa e poi **Vedi i blocchi**; altrimenti scegli il settore dalla lista.

Puoi anche usare **Filtri** nella scheda Blocchi: settore, grado, colore delle prese, i tuoi progressi e valutazione.`,
        },
        {
          q: "Cosa vuol dire seguire un settore?",
          a: `Ti avvisiamo quando nel settore arrivano blocchi nuovi o quando viene ritracciato. Seguilo dalla scheda del
settore (**Segui**) o con la campanella accanto al suo nome nella lista.`,
        },
        {
          q: "Cosa sono i blocchi smontati?",
          a: `Sono i blocchi che la palestra ha tolto dal muro. Restano consultabili, così ritrovi quello che hai salito:
nella scheda Blocchi passa da **Montati** a **Smontati**.`,
        },
      ],
    },
    {
      id: "progressi",
      title: "Blocchi e progressi",
      items: [
        {
          q: "Come segno un blocco completato?",
          a: `Apri il blocco e, in **I tuoi progressi**, tocca **Segna come completato**. Se hai sbagliato, tocca
**Annulla**.

Con **Tentativi** conti quante volte l'hai provato. Si salva da solo: non c'è nessun pulsante da premere.`,
        },
        {
          q: "Cos'è un progetto?",
          a: `Un blocco che hai provato almeno una volta ma non hai ancora completato. Li ritrovi in **Attività** e con
il filtro **I tuoi progressi → Progetti**.`,
        },
        {
          q: "Come valuto un blocco con le stelle?",
          a: `In **La tua valutazione** scegli da 1 a 5 stelle. Serve almeno un tentativo registrato: le stelle le dà
solo chi il blocco l'ha provato.`,
        },
        {
          q: "Come propongo un grado diverso da quello ufficiale?",
          a: `Nella sezione **Grado della community** scegli il tuo grado. Puoi farlo dopo aver registrato almeno un
tentativo, e puoi votare in ogni scala: quelle della palestra e anche quelle che la palestra non usa (es. scala V).

- Un voto per scala: se lo cambi, sostituisce il precedente.
- **Nessuna proposta** ritira il voto.
- **Più votato** mostra il grado scelto dalla maggioranza; **Vedi i voti** mostra come si distribuiscono.

Il grado ufficiale lo decide la palestra e non cambia con i voti.`,
        },
        {
          q: "Dove trovo la beta di un blocco?",
          a: `Se la palestra l'ha pubblicata, nella pagina del blocco c'è **Beta ufficiale**. Dove la palestra lo
permette, sotto trovi anche i **Video della community**.`,
        },
        {
          q: "Come funziona la classifica?",
          a: `Nella scheda **Classifica** della palestra puoi ordinare per punti, completati o grado più alto, per
settimana, mese, anno o da sempre. I punti dipendono dal grado: nella scala principale della palestra il più facile
vale 10 e il più difficile 100. I tentativi non tolgono punti.

Non vuoi comparire? In **Profilo → Classifiche** attiva **Non mostrarmi nelle classifiche**.`,
        },
      ],
    },
    {
      id: "community",
      title: "Commenti, video e segnalazioni",
      items: [
        {
          q: "Come carico un mio video?",
          a: `Nella pagina del blocco, in **Video della community**, tocca **Scegli un video**, aggiungi se vuoi una
didascalia e tocca **Invia per la revisione**. Lo staff della palestra lo approva prima che sia visibile a tutti.

- Formati MP4, MOV o WebM, sotto i 100 MB.
- Al massimo 3 video per blocco.
- Se non viene approvato, vedi il motivo sotto il video.

La sezione c'è solo nelle palestre dove i video della community sono attivi.`,
        },
        {
          q: "Posso modificare o cancellare un commento?",
          a: `Sì, solo i tuoi: **Modifica** o **Elimina** sotto il commento. Un commento modificato mostra
"modificato".`,
        },
        {
          q: "Come segnalo un problema?",
          a: `Per un blocco (presa rotta, grado sbagliato…) tocca **Segnala un problema con questo blocco** in fondo alla
sua pagina. Per un commento o un video usa **Segnala** accanto. Le segnalazioni vanno alla palestra, e ti avvisiamo
quando le ha gestite.

Per segnalare una persona apri il suo profilo e usa **Segnala**: quella arriva al team di BoulderTime.`,
        },
        {
          q: "Come blocco qualcuno?",
          a: `Apri il suo profilo e tocca **Blocca**. Da quel momento non vedete più i commenti e i video l'uno
dell'altro. Non riceve nessun avviso. Le persone bloccate le trovi in **Profilo → Persone bloccate**.`,
        },
      ],
    },
    {
      id: "notifiche",
      title: "Notifiche",
      items: [
        {
          q: "Come attivo le notifiche sul telefono?",
          a: `Vai in **Profilo → Notifiche** e attiva **Notifiche su questo telefono**, poi accetta la richiesta del
telefono. Va fatto su ogni dispositivo che usi.

Su iPhone, se usi BoulderTime dal browser, prima aggiungilo alla schermata Home (**Condividi → Aggiungi a Home**) e
aprilo da lì.`,
        },
        {
          q: "Cosa fa suonare il telefono?",
          a: `Solo le novità di palestre e settori che segui: blocchi nuovi, ritracciature e avvisi della palestra. Il
resto (commenti, modifiche ai blocchi, esito dei tuoi video e delle segnalazioni) lo trovi in **Avvisi**.

Più novità dello stesso tipo si raccolgono in un solo avviso finché non lo leggi. Non ricevi mai avvisi per quello
che fai tu.`,
        },
        {
          q: "Non mi arrivano le notifiche. Cosa controllo?",
          a: `- In **Profilo → Notifiche**, l'interruttore **Notifiche su questo telefono** deve essere attivo.
- Tocca **Invia una notifica di prova**: ti dice subito se il telefono è raggiungibile.
- Nelle impostazioni del telefono, le notifiche di BoulderTime devono essere permesse, e nessuna modalità Full
immersion o Non disturbare deve bloccarle.
- La categoria giusta (es. **Novità dei settori**) deve essere attiva, e in **Cosa segui** l'elemento non deve essere
silenziato.`,
        },
        {
          q: "Come smetto di ricevere avvisi da una palestra senza smettere di seguirla?",
          a: `In **Profilo → Notifiche**, sotto **Cosa segui**, spegni l'interruttore accanto alla palestra, al settore o
al blocco. La X invece smette di seguirlo.`,
        },
      ],
    },
    {
      id: "account",
      title: "Account e privacy",
      items: [
        {
          q: "Come cambio nome, foto o lingua?",
          a: `**Profilo → Modifica profilo**. La lingua vale subito per l'app e anche per le notifiche che ricevi.`,
        },
        {
          q: "Chi vede il mio profilo?",
          a: `Il **profilo pubblico** mostra nome, foto, statistiche, storico e palestre che segui; lo vedi tu stesso da
**Profilo → Profilo pubblico**. Se non vuoi comparire nelle classifiche puoi nasconderti da **Profilo →
Classifiche**.`,
        },
        {
          q: "Come cancello il mio account?",
          a: `In fondo a **Profilo** c'è **Elimina il mio account**. Per 7 giorni puoi ancora ripensarci con **Mantieni il
mio account**; poi i dati vengono cancellati.`,
        },
      ],
    },
  ],
  staff: [
    {
      id: "accesso",
      title: "Accesso e ruoli",
      items: [
        {
          q: "Come entro nell'area staff?",
          a: `Serve un invito. Un amministratore della palestra ti invita da **Gestisci → Staff → Invita qualcuno** con
la tua email. Accedi a BoulderTime con quella email: in Home trovi **Hai ricevuto un invito**, tocca **Accetta**.
L'invito scade dopo 7 giorni.

Poi l'area staff la apri da **Profilo → Gestisci**, o dalla rotella nella pagina della palestra.`,
        },
        {
          q: "Che differenza c'è tra Staff, Admin e Proprietario?",
          a: `- **Staff**: blocchi, beta ufficiale, settori e mappa, novità, moderazione.
- **Admin**: tutto quello dello staff, più scale di grado, impostazioni della palestra (profilo, logo, copertina),
eliminazione definitiva dei blocchi e gestione del team.
- **Proprietario**: come l'admin, e in più può nominare altri proprietari. Una palestra ha sempre almeno un
proprietario.`,
        },
      ],
    },
    {
      id: "blocchi",
      title: "Blocchi",
      items: [
        {
          q: "Come aggiungo un blocco?",
          a: `In **Gestisci → Blocchi** tocca il **+**. Servono la foto, il settore e il colore delle prese; il grado
ufficiale si può lasciare vuoto. Poi **Aggiungi blocco**.

Prima di tutto la palestra deve avere almeno un settore e una scala di grado attivi.`,
        },
        {
          q: "Come scelgo cosa si vede nella card del blocco?",
          a: `Nell'editor del blocco, sotto la foto, tocca **Scegli la parte**: trascina e ingrandisci la foto finché
il riquadro mostra quello che vuoi, poi **Usa questa parte**. La foto intera resta nella pagina del blocco.`,
        },
        {
          q: "Come aggiungo la beta ufficiale?",
          a: `Dopo aver creato il blocco, aprilo in modifica: in **Beta ufficiale** carica un video oppure **Collega
invece un video** (YouTube, Instagram, Vimeo, TikTok, Facebook). I link non contano nel limite di video della
palestra. Chi segue il blocco o lo sta provando viene avvisato.`,
        },
        {
          q: "Ho ritracciato un settore: come tolgo i vecchi blocchi?",
          a: `In **Gestisci → Blocchi** tocca l'icona di selezione (o tieni premuto su una card), scegli i blocchi (anche
**Tutti**) e tocca **Rimuovi**. Con **Avvisa i follower** attivo, chi segue il settore riceve un solo avviso per
settore. Subito dopo puoi scrivere un aggiornamento per raccontare la ritracciatura.

Un blocco ritracciato va rimosso e creato di nuovo, non modificato: chi l'aveva salito tiene il suo completato.`,
        },
        {
          q: "Ho rimosso un blocco per sbaglio",
          a: `Passa a **Smontati** e tocca **Ripristina**. Gli admin possono anche **Eliminare definitivamente** un blocco
smontato: prima l'app mostra cosa si perde.`,
        },
      ],
    },
    {
      id: "settori",
      title: "Settori e mappa",
      items: [
        {
          q: "Come creo e ordino i settori?",
          a: `In **Gestisci → Settori** scrivi il nome in **Nuovo settore** e tocca **Aggiungi**. Le frecce cambiano
l'ordine, la matita il nome, l'occhio lo nasconde. I settori non si cancellano, così lo storico di chi arrampica resta
intatto.`,
        },
        {
          q: "Come disegno i settori sulla piantina?",
          a: `In **Gestisci → Settori** apri **Mappa dei settori** e carica la piantina (un'immagine fino a 5 MB). Poi,
per ogni settore:

- sceglilo in alto;
- tocca la piantina sugli angoli del settore e chiudi toccando di nuovo il primo angolo;
- trascina gli angoli o il nome per sistemarli;
- tocca **Salva il settore**: l'editor passa da solo al prossimo da disegnare.

Se i nomi si accavallano, togli la spunta a **Mostra il nome sulla mappa**: il settore resta toccabile e mostra il
nome quando lo si apre.`,
        },
      ],
    },
    {
      id: "gestione",
      title: "Gradi, moderazione e novità",
      items: [
        {
          q: "Come imposto le scale di grado?",
          a: `In **Gestisci → Gradi** (solo admin) aggiungi una scala: colori, Fontainebleau, scala V o personalizzata.
Con **Modifica i gradi** cambi nomi, ordine e colori. Un grado tolto resta sui blocchi che lo usano già.`,
        },
        {
          q: "Come approvo i video e gestisco le segnalazioni?",
          a: `In **Gestisci → Moderazione**:

- **Video**: **Approva**, oppure **Rifiuta** scrivendo il motivo, che vede chi l'ha caricato. Non puoi approvare un
tuo video.
- **Segnalazioni contenuti**: nascondi il commento o rifiuta il video se serve, poi segna come **Risolta** o
**Archivia**. Chi ha segnalato viene avvisato.

I commenti si possono nascondere anche direttamente dalla pagina del blocco.`,
        },
        {
          q: "Come pubblico un avviso o un evento?",
          a: `In **Gestisci → Novità** tocca **Pubblica una novità**, scegli il tipo (annuncio, evento, cambio orari,
manutenzione, gara…), scrivi titolo e messaggio e, se vuoi, aggiungi un'immagine o limita la novità a un settore.
Con **Avvisa i follower** arriva anche sul telefono di chi segue la palestra. Le modifiche successive non rimandano
l'avviso.`,
        },
        {
          q: "Come cambio logo, copertina e informazioni della palestra?",
          a: `In **Gestisci → Impostazioni** (solo admin): logo, foto di copertina, contatti, descrizione e posizione
sulla mappa (**Trova dall'indirizzo**, poi trascina il segnaposto).`,
        },
      ],
    },
  ],
};
