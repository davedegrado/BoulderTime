# Privacy answers for the stores

Both stores ask what the app collects. The answers below match what the code does, the privacy notice
(`frontend/src/pages/legal/privacy.it.ts`) and the iOS privacy manifest (`frontend/ios/App/App/PrivacyInfo.xcprivacy`).
If one of them changes, change all of them.

The general answers are the same on both stores:

- **Nothing is used for tracking, advertising or analytics.** There is no analytics or crash-reporting SDK.
- **Nothing is sold or shared.** Supabase, Railway, Cloudflare, Resend, MapTiler, Firebase and Apple act on our
  behalf as processors, which neither store counts as "sharing".
- **Everything travels encrypted** (HTTPS only).
- **People can delete their account** in the app (Profile → Account deletion) and read how on
  <https://bouldertime.com/delete-account>, which works without signing in.

## What is collected

| Data | Why | Required? | Notes |
|---|---|---|---|
| Email address | Account | Yes | Sign-in and service emails |
| Name (display name) | App functionality | Yes | Shown on comments and leaderboards |
| User ID | App functionality | Yes | The account's id |
| Photos and videos | App functionality | No | Profile photo, beta and climber videos |
| Other user content | App functionality | No | Comments, grade suggestions, ratings, reports |
| App interactions | App functionality | Yes | Attempts, sends, follows: the history and the leaderboards |
| Device ID (push token) | App functionality | No | Only if notifications are switched on |
| Approximate location | App functionality | No | Only for "gyms near me" and the map: coarsened on the phone before sending (see below), not stored on the account |

Not collected: precise location, contacts, health, financial data, browsing history, search history, diagnostics.

**Why "approximate" is true.** The phone's position never leaves it as such: `frontend/src/features/map/coarse.ts`
snaps it to a 0.03° grid (about 3.3 × 2.4 km in Italy, ≈ 8 km²; two decimals at most) before the gym search sends it,
and widens the map's visible box to the same grid, so the box's centre doesn't give the position away either. Google
Play calls "approximate" anything of 3 km² or more; Apple calls "precise" three decimals or more. On iOS the app also
asks for reduced accuracy by default (`NSLocationDefaultAccuracyReduced`). In the store apps the position comes from
the system (`@capacitor/geolocation`, low accuracy): Android 12+ is asked for approximate location only; Android 11
and earlier, which have no "approximate" choice, get `ACCESS_FINE_LOCATION`, still declared for them. What is
collected is decided by what is sent, and that is coarse.

**Third parties the app talks to directly.** Supabase (sign-in, files), the API on Railway, the map tile provider
(`VITE_MAP_TILE_URL`, MapTiler in production: the builds warn when it is missing), and Firebase/APNs only once
notifications are switched on (Firebase's automatic token creation is off until then). Fonts ship with the app; no
Google Fonts request.

## Google Play: Data safety

Play Console → App content → Data safety.

1. **Does your app collect or share any of the required user data types?** Yes.
2. **Is all of the user data collected by your app encrypted in transit?** Yes.
3. **Do you provide a way for users to request that their data is deleted?** Yes, with the URL
   <https://bouldertime.com/delete-account>.
4. Data types, each **Collected: yes, Shared: no, Processed ephemerally: no**, purpose **App functionality** (email also
   **Account management**):
   - Personal info → **Name**, **Email address**, **User IDs** (required)
   - Photos and videos → **Photos**, **Videos** (optional)
   - App activity → **App interactions** (required), **Other user-generated content** (optional)
   - Device or other IDs → **Device or other IDs** (optional: the notification token)
   - Location → **Approximate location** (optional)
5. App content → **Account deletion**: the same URL.
6. App content → **Privacy policy**: <https://bouldertime.com/privacy>.

## App Store Connect: App Privacy

App Store Connect → the app → App Privacy → Get Started. **Do you or your third-party partners collect data from this
app?** Yes. For every type below, choose **App Functionality** only, **Linked to the user: yes**, **Used for tracking:
no**:

- Contact Info → **Name**, **Email Address**
- User Content → **Photos or Videos**, **Other User Content**
- Identifiers → **User ID**, **Device ID**
- Usage Data → **Product Interaction**
- Location → **Coarse Location**

Privacy Policy URL: <https://bouldertime.com/privacy>. Because there is no tracking, the app never shows Apple's
"Allow tracking?" prompt and needs no `NSUserTrackingUsageDescription`.

The privacy manifest in the app declares the same list. Capacitor and Firebase bring their own manifests for the system
APIs they use; the app's own code uses none of Apple's "required reason" APIs.
