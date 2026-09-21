# Email templates (Italian)

Supabase → **Authentication → Emails → Templates**. For each template, paste the subject and the HTML file.
`{{ .ConfirmationURL }}`, `{{ .Email }}` and `{{ .NewEmail }}` are filled in by Supabase.

| Supabase template | Subject | File |
|---|---|---|
| Confirm signup | `Conferma il tuo account BoulderTime` | `confirm-signup.html` |
| Reset password | `Scegli una nuova password per BoulderTime` | `reset-password.html` |
| Change email address | `Conferma il nuovo indirizzo email` | `change-email.html` |
| Invite user | `Sei stato invitato su BoulderTime` | `invite.html` |

Written to stay out of spam folders: a real sentence before the button, the plain link repeated under it, no images,
no link shorteners, and a clear line for people who didn't ask for the email. Keep click and open tracking off in
Resend — rewritten links are a spam signal.
