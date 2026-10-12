import { useState } from "react";
import { Link } from "react-router-dom";
import { CalendarCheck, CalendarPlus, ChevronDown, Users } from "lucide-react";
import { useAttendance, useAttendees, type Announcement } from "@/features/notifications/api";
import { useAuth } from "@/auth/AuthProvider";
import { Avatar } from "@/components/Avatar";
import { Button } from "@/components/Button";
import { useToast } from "@/components/Toast";
import { errorMessage } from "@/lib/apiError";
import { plural, t } from "@/i18n/i18n";

/** How long after its date an event still takes names: the whole of that day (the server holds the same rule). */
const GRACE_MS = 24 * 60 * 60 * 1000;

/**
 * "I'll be there" under an event: one tap to say it, one to take it back, how many are coming, and — for the gym's
 * followers and staff — who. Not a booking: no places, no waiting list.
 */
export function AttendanceBar({ a }: { a: Announcement }) {
  const { session } = useAuth();
  const toast = useToast();
  const attend = useAttendance(a);
  const [open, setOpen] = useState(false);
  const attendees = useAttendees(a.id, open && !!a.canSeeAttendees);
  if (!a.takesAttendance) return null;

  const count = a.attendees ?? 0;
  const going = !!a.viewerAttending;
  const over = a.eventDate ? new Date(a.eventDate).getTime() + GRACE_MS < Date.now() : false;
  const toggle = () => attend.mutate(!going, { onError: (e) => toast.error(errorMessage(e)) });

  return (
    <div className="attendance">
      <div className="attendance__row">
        {session && !(over && !going) && (
          <Button variant={going ? "secondary" : "primary"} icon={going ? <CalendarCheck aria-hidden /> : <CalendarPlus aria-hidden />}
            loading={attend.isPending} onClick={toggle} aria-pressed={going}>
            {going ? t("You're going · cancel") : t("I'll be there")}
          </Button>
        )}
        {!session && !over && <Link to="/sign-in" className="text-link">{t("Sign in to say you'll be there")}</Link>}
        <span className="attendance__count"><Users aria-hidden /> {count === 0 ? t("Nobody yet") : plural(count, "{count} person going", "{count} people going")}</span>
      </div>
      {a.canSeeAttendees && count > 0 && (
        <div className="attendance__who">
          <button type="button" className="text-btn" aria-expanded={open} onClick={() => setOpen((v) => !v)}>
            {t("See who is going")} <ChevronDown aria-hidden className={open ? "is-open" : ""} />
          </button>
          {open && (attendees.isPending ? <p className="list__sub">{t("Loading…")}</p>
            : attendees.isError ? <p className="list__sub">{errorMessage(attendees.error)}</p>
            : (
              <ul className="attendance__list">
                {attendees.data.map((p) => (
                  <li key={p.userId}>
                    <Link to={`/users/${p.userId}`} className="attendance__person">
                      <Avatar name={p.displayName} url={p.avatarUrl} size={28} /><span>{p.displayName}</span>
                    </Link>
                  </li>
                ))}
              </ul>
            ))}
        </div>
      )}
    </div>
  );
}
