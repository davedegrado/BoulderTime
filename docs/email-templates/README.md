# Email templates (Italian)

Supabase → **Authentication → Emails → Templates**. For each template, paste the subject and the HTML file.
`{{ .SiteURL }}`, `{{ .TokenHash }}`, `{{ .Email }}` and `{{ .NewEmail }}` are filled in by Supabase.

**The links go to `/auth/confirm` with a token, not to `{{ .ConfirmationURL }}`.** The default link carries a code
that only works in the browser where sign-up started, so a person who signs up in the app and opens the email in the
browser (or on another device) would see a failed sign-in. The token is checked by the server and works anywhere.
It also points straight at bouldertime.com, which is what lets the phone open the link in the app.

| Supabase template | Subject | File |
|---|---|---|
| Confirm signup | `Conferma il tuo account BoulderTime` | `confirm-signup.html` |
| Reset password | `Scegli una nuova password per BoulderTime` | `reset-password.html` |
| Change email address | `Conferma il nuovo indirizzo email` | `change-email.html` |
| Invite user | `Sei stato invitato su BoulderTime` | `invite.html` |

Written to stay out of spam folders: a real sentence before the button, the plain link repeated under it, no images,
no link shorteners, and a clear line for people who didn't ask for the email. Keep click and open tracking off in
Resend — rewritten links are a spam signal.
