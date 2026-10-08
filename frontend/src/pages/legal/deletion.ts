/**
 * How to delete an account, readable without signing in. Google Play asks for a web page like this one, reachable
 * outside the app, and it has to match what AccountEraser really does (ADR-024, ADR-038). In Italian and English, as
 * the app is: someone who used the app in English must be able to read how to leave it.
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

export const DELETION_EN = `
# Delete your BoulderTime account

BoulderTime is the bouldering gym app developed by **Davide Luisi**. You can delete your account, and your data with
it, whenever you like: from the Android app, the iOS app or the website, which all work the same way.

## How

- Sign in to BoulderTime with your account.
- Open **Profile → Account deletion**.
- Type your email address to confirm, and confirm the deletion.

If you are the only owner of a gym, first make someone else an owner from the staff settings: a gym can't be left
without anyone to run it.

## If you can't sign in

Write to **support@bouldertime.com** from the account's email address and ask for it to be deleted. We do it within
30 days and confirm it to you.

## What happens next

- You leave the leaderboards straight away. For **7 days** you can still change your mind from the same place in
  your profile.
- After the 7 days we delete: your name, email, profile photo, attempts and sends, ratings, grade suggestions,
  comments and likes, uploaded videos, the gyms, sectors and boulders you follow, notifications, registered devices,
  reports and blocks between users, and the sign-in itself.

## What stays

- Without your name, what belongs to the gym: the boulders you set and the official beta, if you were on its staff.
- If the account was suspended for breaking the rules, for **2 years** a cryptographic fingerprint (hash) of the
  email address, from which the address can't be recovered: it only stops a suspended account from starting over.
- Our providers' technical logs, for about 90 days.

The full details are in the privacy notice (in Italian).
`.trim();

