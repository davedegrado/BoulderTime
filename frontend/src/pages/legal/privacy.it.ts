/**
 * Privacy notice, written against what the app actually does rather than from a template: the services named here
 * are the ones in use, and the retention periods are ones the app really enforces.
 *
 * A draft to be reviewed by a professional before opening to the public.
 */
export const PRIVACY_UPDATED = "2026-10-10";

export const PRIVACY_IT = `
# Informativa sulla privacy

Ultimo aggiornamento: 10 ottobre 2026

## Chi tratta i tuoi dati

Il titolare del trattamento è **Davide Luisi**, Modena (Italia).
Per qualsiasi richiesta: **support@bouldertime.com**. L'indirizzo completo è disponibile su richiesta.

## Quali dati raccogliamo e perché

**Per avere un account** ci servono email e nome visualizzato. Senza, non puoi accedere. La password non la vediamo
mai: è gestita in forma cifrata dal nostro fornitore di autenticazione.

**Quando usi l'app** registriamo quello che fai tu: tentativi e blocchi completati, valutazioni, proposte di grado,
commenti, palestre e settori che segui. Servono a mostrarti il tuo storico e a costruire le classifiche delle
palestre. Puoi uscire dalle classifiche quando vuoi dal tuo profilo.

**Se carichi una foto profilo o un video** conserviamo il file e chi l'ha caricato. I video sono visibili solo dopo
l'approvazione dello staff della palestra.

**Se attivi le notifiche sul telefono** conserviamo l'indirizzo tecnico del tuo dispositivo (un codice generato per
le notifiche, non l'identificativo del telefono), che serve solo a recapitarle. Puoi disattivarle quando vuoi.

**Se cerchi le palestre vicine a te** l'app usa la posizione del telefono, solo dopo che lo hai permesso. Al server
arriva una posizione approssimata (circa un chilometro), usata per la ricerca e non salvata nel tuo account.

**Dati tecnici**: indirizzo IP e informazioni sulla richiesta finiscono nei registri dei nostri fornitori, per
sicurezza e per capire i malfunzionamenti.

Non usiamo cookie di profilazione, non facciamo pubblicità e non vendiamo i tuoi dati a nessuno.

## Su quale base

- **Esecuzione del servizio** per account, attività e contenuti: senza questi dati l'app non funziona.
- **Consenso** per i video che carichi e per le notifiche sul telefono: puoi ritirarlo in qualsiasi momento.
- **Legittimo interesse** per la sicurezza del servizio, la moderazione dei contenuti e delle persone segnalate e la
  sospensione degli account che violano le regole.

## I video e le altre persone

Un video di arrampicata riprende spesso anche chi assicura, chi passa dietro, chi è seduto a bordo parete.
**Chi carica un video dichiara di avere il consenso delle persone riconoscibili.** Se compari in un video e non vuoi,
scrivi a support@bouldertime.com o usa la segnalazione nell'app: lo rimuoviamo in tempi brevi, senza chiederti perché.

## Minori

BoulderTime non è destinato a chi ha meno di 14 anni, che è l'età minima prevista in Italia per iscriversi da soli a
un servizio online. Quando crei l'account, o accetti i termini aggiornati, ti chiediamo di dichiarare di avere almeno
14 anni: conserviamo solo il fatto che l'hai dichiarato e quando, non la tua data di nascita. Se veniamo a sapere che un
account appartiene a un minore di 14 anni, lo cancelliamo.

## Chi altro tratta i dati per noi

Sono fornitori che agiscono su nostra istruzione, scelti perché tengono i dati in Europa:

- **Supabase** (Francoforte, Germania): database, autenticazione, archiviazione di foto e video.
- **Railway** (Amsterdam, Paesi Bassi): il server che fa funzionare l'app.
- **Cloudflare**: distribuzione del sito e protezione, dominio ed email di contatto.
- **Resend** (Irlanda): invio delle email di servizio, come la conferma dell'account.
- **MapTiler / OpenStreetMap**: le mappe della sezione Esplora. Quando apri la mappa, il tuo indirizzo IP è visibile
  al fornitore delle mappe.
- **Google Firebase Cloud Messaging** e, su iPhone, **Apple Push Notification service**: recapitano le notifiche sul
  telefono. Ricevono l'indirizzo tecnico del dispositivo e il testo della notifica. Nel browser, le notifiche passano
  dal servizio del produttore del browser (per esempio Google per Chrome, Apple per Safari, Mozilla per Firefox).

I tuoi dati restano nello Spazio economico europeo, con un'eccezione: i servizi di notifica di Google e Apple possono
trattare i dati che servono a recapitarle anche negli Stati Uniti, con le garanzie previste dal GDPR (EU-US Data
Privacy Framework e clausole contrattuali standard). Se non attivi le notifiche, questo non avviene.

## Per quanto tempo

- **Finché hai l'account.** Quando lo cancelli, hai **7 giorni** per ripensarci; poi i dati personali sono eliminati.
- **Video rifiutati** dalla moderazione: eliminati entro 30 giorni.
- **Registri tecnici**: circa 90 giorni.
- **Account sospesi.** Se un account sospeso per violazione delle regole viene cancellato, conserviamo per **2 anni**
  soltanto un'impronta crittografica (hash) del suo indirizzo email, da cui l'indirizzo non si può ricavare. Serve
  unicamente a riconoscere un nuovo account creato con lo stesso indirizzo, che parte sospeso finché non lo
  valutiamo. Le segnalazioni fatte da te o su di te sono invece eliminate con l'account.
- Restano, senza il tuo nome, i contenuti che appartengono alla palestra: i blocchi che hai tracciato e le beta
  ufficiali, se facevi parte dello staff.

## Cosa puoi fare

Puoi accedere ai tuoi dati, correggerli, cancellarli, limitarne l'uso, opporti al trattamento e chiederne una copia.

La cancellazione la fai da solo: **Profilo → Cancellazione dell'account** (istruzioni anche su
bouldertime.com/delete-account). Per tutto il resto scrivi a
support@bouldertime.com: rispondiamo entro 30 giorni.

Se ritieni che i tuoi dati siano trattati in modo scorretto puoi rivolgerti al **Garante per la protezione dei dati
personali** (www.garanteprivacy.it).

## Modifiche

Se cambiamo questa informativa in modo sostanziale te lo diciamo nell'app. La data in alto indica l'ultimo
aggiornamento.
`.trim();
