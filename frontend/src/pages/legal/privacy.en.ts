/**
 * The privacy notice in English: a courtesy translation of PRIVACY_IT, which prevails. Keep the two in step: same
 * sections, same facts, same date. A draft to be reviewed by a professional, as the Italian one.
 */
export const PRIVACY_EN = `
# Privacy notice

Last updated: 11 October 2026

**This is a courtesy translation. If it differs from the Italian version, the Italian version prevails.**

## Who handles your data

The data controller is **Davide Luisi**, Modena (Italy).
For any request: **support@bouldertime.com**. The full address is available on request.

## What data we collect and why

**To have an account** we need your email and a display name. Without them you can't sign in. We never see your
password: it is kept encrypted by our authentication provider.

**When you use the app** we record what you do: attempts and completed boulders, ratings, grade suggestions,
comments, the gyms and sectors you follow. They are used to show you your history and, if you choose to appear in
them, to build the gyms' leaderboards: a new account doesn't appear in the leaderboards until you turn them on from
your profile, and you can leave them whenever you like.

**If you upload a profile photo or a video** we keep the file and who uploaded it. Videos are visible only after the
gym's staff have approved them.

**If you turn on notifications on your phone** we keep your device's technical address (a code generated for
notifications, not the phone's identifier), used only to deliver them. You can turn them off whenever you like.

**If you look for gyms near you** the app uses your phone's location, only after you have allowed it. The server only
receives an approximate position (accurate to a few kilometres), used for the search and not saved in your account.

**Technical data**: IP address and information about the request end up in our providers' logs, for security and to
understand faults.

We don't use profiling cookies, we don't show advertising and we don't sell your data to anyone.

## On what basis

- **Providing the service** for the account, activity and content: without this data the app doesn't work.
- **Consent** for the videos you upload and for notifications on your phone: you can withdraw it at any time.
- **Legitimate interest** for the security of the service, the moderation of content and of reported people, and the
  suspension of accounts that break the rules.

## Videos and other people

A climbing video often also shows the person belaying, someone walking by, someone sitting at the foot of the wall.
**Whoever uploads a video declares that they have the consent of the people who can be recognised.** If you appear in
a video and don't want to, write to support@bouldertime.com or use the report option in the app: we remove it quickly,
without asking you why.

## Minors

BoulderTime is not meant for anyone under 14, the minimum age in Italy to sign up for an online service on one's own.
When you create the account, or accept updated terms, we ask you to declare that you are at least 14: we only keep the
fact that you declared it and when, not your date of birth. If we learn that an account belongs to someone under 14,
we delete it.

## Who else handles the data for us

These are providers acting on our instructions, chosen because they keep data in Europe:

- **Supabase** (Frankfurt, Germany): database, authentication, storage of photos and videos.
- **Railway** (Amsterdam, Netherlands): the server that runs the app.
- **Cloudflare**: delivery and protection of the website, domain and contact email.
- **Resend** (Ireland): sending service emails, such as account confirmation and the alerts we receive when someone
  makes a report.
- **MapTiler / OpenStreetMap**: the maps in the Explore section. When you open the map, your IP address is visible to
  the map provider.
- **Google Firebase Cloud Messaging** and, on iPhone, **Apple Push Notification service**: they deliver notifications
  to your phone. They receive the device's technical address and the text of the notification. In the browser,
  notifications go through the service of the browser's maker (for example Google for Chrome, Apple for Safari,
  Mozilla for Firefox).

Your data stays in the European Economic Area, with one exception: Google's and Apple's notification services may
also process the data needed to deliver them in the United States, with the safeguards provided by the GDPR (EU-US
Data Privacy Framework and standard contractual clauses). If you don't turn on notifications, this doesn't happen.

## For how long

- **As long as you have the account.** When you delete it, you have **7 days** to change your mind; then your
  personal data is erased.
- **Technical logs**: for the time set by the terms of the providers that keep them.
- **Suspended accounts.** If an account suspended for breaking the rules is deleted, we keep for **2 years** only a
  cryptographic fingerprint (hash) of its email address, which doesn't contain the address and can't be read back.
  Its only use is to recognise a new account created with the same address, which starts suspended until we review
  it. Reports made by you or about you are deleted with the account.
- Content that belongs to the gym stays, without your name: the boulders you set and the official betas, if you were
  on the staff.

## What you can do

You can access your data, correct it, delete it, restrict its use, object to its processing and ask for a copy.

You delete the account yourself: **Profile → Account deletion** (instructions also at
bouldertime.com/delete-account). For everything else write to
support@bouldertime.com: we reply within 30 days.

If you believe your data is handled improperly you can contact the Italian data protection authority, the **Garante
per la protezione dei dati personali** (www.garanteprivacy.it).

## Changes

If we change this notice substantially we tell you in the app. The date at the top shows the latest update.
`.trim();
