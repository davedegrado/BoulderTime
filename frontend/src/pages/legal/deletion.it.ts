/**
 * How to delete an account, readable without signing in. Google Play asks for a web page like this one, reachable
 * outside the app, and it has to match what AccountEraser really does (ADR-024, ADR-038).
 */
export const DELETION_IT = `
# Cancellare l'account BoulderTime

BoulderTime è l'app delle palestre di boulder sviluppata da **Davide Luisi**. Puoi cancellare il tuo account, e con
lui i tuoi dati, quando vuoi: dall'app Android, dall'app iOS o dal sito, che funzionano allo stesso modo.

## Come si fa

- Accedi a BoulderTime con il tuo account.
- Apri **Profilo → Cancellazione dell'account**.
- Scrivi il tuo indirizzo email per confermare e conferma la cancellazione.

Se sei l'unico proprietario di una palestra, prima nomina un altro proprietario dalla gestione dello staff: la
palestra non può restare senza nessuno che la gestisca.

## Se non riesci ad accedere

Scrivi a **support@bouldertime.com** dall'indirizzo email dell'account, chiedendo la cancellazione. La facciamo
entro 30 giorni e te lo confermiamo.

## Cosa succede dopo

- Esci subito dalle classifiche. Per **7 giorni** puoi ancora cambiare idea dallo stesso punto del profilo.
- Passati i 7 giorni eliminiamo: nome, email, foto profilo, tentativi e blocchi completati, valutazioni, proposte di
  grado, commenti e like, video caricati, palestre, settori e blocchi seguiti, notifiche, dispositivi registrati,
  segnalazioni e blocchi tra utenti, e l'accesso stesso.

## Cosa resta

- Senza il tuo nome, quello che appartiene alla palestra: i blocchi che hai tracciato e le beta ufficiali, se facevi
  parte dello staff.
- Se l'account era sospeso per violazione delle regole, per **2 anni** un'impronta crittografica (hash) dell'email,
  da cui l'indirizzo non si può ricavare: serve solo a non far ripartire da zero un account sospeso.
- I registri tecnici dei fornitori, per circa 90 giorni.

Tutti i dettagli sono nell'informativa sulla privacy.
`.trim();
