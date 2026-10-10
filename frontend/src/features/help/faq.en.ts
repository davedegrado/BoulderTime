import type { FaqGuide } from "@/features/help/faq";

/** The guide in English. Same format as the Italian one; button names as the English app shows them. */
export const FAQ_EN: FaqGuide = {
  climbers: [
    {
      id: "inizio",
      title: "Gyms and sectors",
      items: [
        {
          q: "How do I find my gym?",
          a: `Open **Explore** and search by name or city, or switch to **Map** to see the gyms around you. If you let the
app know where you are, the list starts from the nearest.

Your gym isn't there? At the bottom of the list, tap **Suggest it**: we get in touch with them. Your suggestions and their
status are on the same page.`,
        },
        {
          q: "Why follow a gym?",
          a: `Tap the **bell** at the top of the gym's page. You get its news (new boulders, announcements, events) and
the gym shows up on Home. The **heart** makes it a favourite, kept at the top.`,
        },
        {
          q: "How do I see the boulders of one sector?",
          a: `On the gym's page open **Sectors**. If the gym uploaded its floor plan, tap the sector on the map, then
**See the boulders**; otherwise pick the sector from the list.

**Filters** in the Boulders tab work too: sector, grade, hold colour, your progress and rating.`,
        },
        {
          q: "What does following a sector do?",
          a: `We tell you when new boulders go up there or when it is retraced. Follow it from the sector's sheet
(**Follow**) or with the bell next to its name in the list.`,
        },
        {
          q: "What are removed boulders?",
          a: `Boulders the gym took off the wall. They stay listed, so you can find what you climbed: in the Boulders tab
switch from **On the wall** to **Taken down**.`,
        },
      ],
    },
    {
      id: "progressi",
      title: "Boulders and progress",
      items: [
        {
          q: "How do I log a send?",
          a: `Open the boulder and, in **Your progress**, tap **Mark as completed**. Made a mistake? Tap **Undo**.

**Attempts** counts how many times you tried it. It saves by itself: there is no button to press.`,
        },
        {
          q: "What is a project?",
          a: `A boulder you have tried at least once but not completed yet. Find them in **Activity** and with the filter
**Your progress → Projects**.`,
        },
        {
          q: "How do I rate a boulder?",
          a: `In **Your rating** pick 1 to 5 stars. You need at least one logged attempt: only people who tried a
boulder rate it.`,
        },
        {
          q: "How do I suggest a different grade?",
          a: `In **Community grade** pick your grade. You can once you have logged at least one attempt, and you can vote
in every scale: the gym's own and also those the gym doesn't use (e.g. V-scale).

- One vote per scale: changing it replaces the old one.
- **No suggestion** withdraws your vote.
- **Most voted** shows the grade most people chose; **See the votes** shows how they spread.

The official grade is the gym's and votes never change it.`,
        },
        {
          q: "Where is the beta?",
          a: `If the gym published it, the boulder's page has **Official beta**. Where the gym allows it, **Community
videos** are below.`,
        },
        {
          q: "How does the leaderboard work?",
          a: `In the gym's **Leaderboard** tab you can rank by points, sends or hardest grade, by week, month, year or all
time. Points follow the grade: on the gym's main scale the easiest is worth 10 and the hardest 100. Attempts never cost
points.

Rather not appear? In **Profile → Leaderboards** turn on **Don't show me in leaderboards**.`,
        },
      ],
    },
    {
      id: "community",
      title: "Comments, videos and reports",
      items: [
        {
          q: "How do I upload a video?",
          a: `On the boulder's page, in **Community videos**, tap **Choose a video**, add a caption if you like and tap
**Send for review**. The gym's staff approve it before everyone can see it.

- MP4, MOV or WebM, under 100 MB.
- Up to 3 videos per boulder.
- If it isn't approved, the reason shows under the video.

The section is there only at gyms where community videos are on.`,
        },
        {
          q: "Can I edit or delete a comment?",
          a: `Yes, your own: **Edit** or **Delete** under the comment. An edited comment says "edited".`,
        },
        {
          q: "How do I report a problem?",
          a: `For a boulder (broken hold, wrong grade…) tap **Report a problem with this boulder** at the bottom of its
page. For a comment or a video use **Report** next to it. Reports go to the gym, and we tell you when it has handled
them.

To report a person, open their profile and use **Report**: that one goes to the BoulderTime team.`,
        },
        {
          q: "How do I block someone?",
          a: `Open their profile and tap **Block**. From then on neither of you sees the other's comments and videos.
They are not told. Blocked people are in **Profile → Blocked people**.`,
        },
      ],
    },
    {
      id: "notifiche",
      title: "Notifications",
      items: [
        {
          q: "How do I turn on phone notifications?",
          a: `Go to **Profile → Notifications** and turn on **Notifications on this phone**, then accept the phone's
request. Do it on every device you use.

On iPhone, if you use BoulderTime in the browser, first add it to the Home Screen (**Share → Add to Home Screen**)
and open it from there.`,
        },
        {
          q: "What makes my phone buzz?",
          a: `Only news from gyms and sectors you follow: new boulders, retraces and gym announcements. Everything else
(comments, boulder changes, the outcome of your videos and reports) is in **Notifications** inside the app.

Several updates of the same kind gather into one until you read it. You are never notified about what you did
yourself.`,
        },
        {
          q: "Notifications don't arrive. What do I check?",
          a: `- In **Profile → Notifications**, **Notifications on this phone** must be on.
- Tap **Send a test notification**: it tells you straight away whether the phone can be reached.
- In the phone's settings, BoulderTime's notifications must be allowed, and no Focus or Do Not Disturb mode must hold
them back.
- The right category (e.g. **Sector updates**) must be on, and under **What you follow** the item must not be muted.`,
        },
        {
          q: "How do I mute a gym without unfollowing it?",
          a: `In **Profile → Notifications**, under **What you follow**, switch off the gym, sector or boulder. The X
unfollows it instead.`,
        },
      ],
    },
    {
      id: "account",
      title: "Account and privacy",
      items: [
        {
          q: "How do I change my name, photo or language?",
          a: `**Profile → Edit profile**. The language applies at once to the app and to the notifications you get.`,
        },
        {
          q: "Who sees my profile?",
          a: `Your **public profile** shows your name, photo, stats, history and the gyms you follow; see it yourself from
**Profile → Public profile**. To stay out of leaderboards, use **Profile → Leaderboards**.`,
        },
        {
          q: "How do I delete my account?",
          a: `At the bottom of **Profile** there is **Delete my account**. For 7 days you can still change your mind with
**Keep my account**; then the data is deleted.`,
        },
      ],
    },
  ],
  staff: [
    {
      id: "accesso",
      title: "Access and roles",
      items: [
        {
          q: "How do I get into the staff area?",
          a: `You need an invitation. A gym admin invites you from **Manage → Staff → Invite someone** with your email.
Sign in to BoulderTime with that email: Home shows **You've been invited**, tap **Accept**. Invitations expire after
7 days.

Then open the staff area from **Profile → Manage**, or the gear on the gym's page.`,
        },
        {
          q: "What's the difference between Staff, Admin and Owner?",
          a: `- **Staff**: boulders, official beta, sectors and the map, news, moderation.
- **Admin**: everything staff do, plus grading scales, gym settings (profile, logo, cover), deleting boulders for good
and managing the team.
- **Owner**: like an admin, and can make others owners. A gym always keeps at least one owner.`,
        },
      ],
    },
    {
      id: "blocchi",
      title: "Boulders",
      items: [
        {
          q: "How do I add a boulder?",
          a: `In **Manage → Boulders** tap **+**. You need a photo, the sector and the hold colour; the official grade can
stay empty. Then **Add boulder**.

The gym first needs at least one active sector and one active grading scale.`,
        },
        {
          q: "How do I choose what the boulder's card shows?",
          a: `In the boulder editor, under the photo, tap **Choose the part**: drag and zoom until the frame shows what you
want, then **Use this part**. The whole photo stays on the boulder's page.`,
        },
        {
          q: "How do I add the official beta?",
          a: `Once the boulder exists, open it to edit: in **Official beta** upload a video or **Link a video instead**
(YouTube, Instagram, Vimeo, TikTok, Facebook). Links don't count towards the gym's video limit. People following or
projecting the boulder are told.`,
        },
        {
          q: "I retraced a sector: how do I take the old boulders off?",
          a: `In **Manage → Boulders** tap the select icon (or long-press a card), pick the boulders (or **All**) and tap
**Remove**. With **Notify followers** on, people following the sector get one notification per sector. Right after,
you can write an update about the retrace.

A retraced boulder is removed and created again, not edited: people who sent it keep their send.`,
        },
        {
          q: "I removed a boulder by mistake",
          a: `Switch to **Taken down** and tap **Restore**. Admins can also **Delete for good** a removed boulder: the app
first shows what would be lost.`,
        },
      ],
    },
    {
      id: "settori",
      title: "Sectors and the map",
      items: [
        {
          q: "How do I create and order sectors?",
          a: `In **Manage → Sectors** type the name in **New sector** and tap **Add**. The arrows change the order, the
pencil the name, the eye hides it. Sectors are never deleted, so climbers' history stays intact.`,
        },
        {
          q: "How do I draw the sectors on the floor plan?",
          a: `In **Manage → Sectors** open **Sector map** and upload the floor plan (an image up to 5 MB). Then, for each
sector:

- pick it at the top;
- tap the plan at the sector's corners and close it by tapping the first corner again;
- drag the corners or the name to adjust;
- tap **Save the sector**: the editor moves on to the next one to draw.

If names overlap, untick **Show the name on the map**: the sector stays tappable and shows its name when opened.`,
        },
      ],
    },
    {
      id: "gestione",
      title: "Grades, moderation and news",
      items: [
        {
          q: "How do I set up grading scales?",
          a: `In **Manage → Grades** (admins only) add a scale: colours, Fontainebleau, V-scale or custom. **Edit grades** changes names, order and colours. A retired grade stays on the boulders that already use it.`,
        },
        {
          q: "How do I approve videos and handle reports?",
          a: `In **Manage → Moderation**:

- **Videos**: **Approve**, or **Reject** with a reason the uploader sees. You can't approve your own video.
- **Reports**: hide the comment or reject the video if needed, then mark it **Resolved** or **Archive** it.
The person who reported is told.

Comments can also be hidden straight from the boulder's page.`,
        },
        {
          q: "How do I post an announcement or an event?",
          a: `In **Manage → Updates** tap **New update**, pick the kind (announcement, event, opening hours,
maintenance, competition…), write a title and message and, if you like, add an image or limit it to one sector.
With **Notify followers** it also reaches the phones of people following the gym. Later edits don't notify again.`,
        },
        {
          q: "How do I change the gym's logo, cover and details?",
          a: `In **Manage → Settings** (admins only): logo, cover photo, contacts, description and position on the map
(**Find from address**, then drag the pin).`,
        },
      ],
    },
  ],
};
