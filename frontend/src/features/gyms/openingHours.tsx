import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { CalendarOff, Clock, Plus, Save, Trash2, X } from "lucide-react";
import { api } from "@/lib/api";
import { gymKeys, type GymDetail } from "@/features/gyms/api";
import { Button } from "@/components/Button";
import { useToast } from "@/components/Toast";
import { ApiError, errorMessage } from "@/lib/apiError";
import { localeTag, t } from "@/i18n/i18n";

/** "HH:mm" in the gym's local time; "24:00" closes at midnight. */
export interface OpeningInterval { opens: string; closes: string }
/** 1 Monday … 7 Sunday. A day left out, or without intervals, is a closed day. */
export interface OpeningDay { day: number; intervals: OpeningInterval[] }
/** Both dates included, "YYYY-MM-DD". */
export interface Closure { from: string; to: string; note?: string | null }
export interface OpeningHours { week: OpeningDay[]; closures: Closure[] }

/** Every gym on BoulderTime is in Italy for now, so its hours are read on Italian time wherever the reader is. */
export const GYM_TIME_ZONE = "Europe/Rome";
export const DAYS = [1, 2, 3, 4, 5, 6, 7] as const;
export const MAX_INTERVALS = 3;

const minutes = (time: string) => Number(time.slice(0, 2)) * 60 + Number(time.slice(3, 5));

/** Monday's name for 1, Sunday's for 7, in the language in use. */
export function dayName(day: number, style: "long" | "short" = "long"): string {
  // 1 January 2024 was a Monday.
  return new Date(Date.UTC(2024, 0, day, 12)).toLocaleDateString(localeTag(), { weekday: style, timeZone: "UTC" });
}

/** "24:00" reads as midnight. */
export const timeLabel = (time: string) => (time === "24:00" ? "00:00" : time);

/** The date, weekday and minutes of the day at the gym right now. */
export function gymNow(now: Date = new Date()): { date: string; day: number; minutes: number } {
  const parts = Object.fromEntries(new Intl.DateTimeFormat("en-GB", {
    timeZone: GYM_TIME_ZONE, year: "numeric", month: "2-digit", day: "2-digit", hour: "2-digit", minute: "2-digit", hourCycle: "h23", weekday: "short",
  }).formatToParts(now).map((p) => [p.type, p.value]));
  const day = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].indexOf(parts.weekday ?? "") + 1;
  return { date: `${parts.year}-${parts.month}-${parts.day}`, day, minutes: Number(parts.hour) * 60 + Number(parts.minute) };
}

const addDays = (date: string, n: number) => {
  const d = new Date(`${date}T12:00:00Z`);
  d.setUTCDate(d.getUTCDate() + n);
  return d.toISOString().slice(0, 10);
};

export const closureOn = (hours: OpeningHours, date: string) => hours.closures.find((c) => c.from <= date && date <= c.to) ?? null;
const intervalsOn = (hours: OpeningHours, day: number) =>
  [...(hours.week.find((d) => d.day === day)?.intervals ?? [])].sort((a, b) => minutes(a.opens) - minutes(b.opens));

/** Closures not over yet, soonest first. */
export const upcomingClosures = (hours: OpeningHours, today: string) =>
  hours.closures.filter((c) => c.to >= today).sort((a, b) => a.from.localeCompare(b.from));

/**
 * One line on whether the gym is open: "Open now · closes at 23:00", "Closed · opens tomorrow at 10:00",
 * "Closed today: Christmas". Null when the gym has no opening times at all.
 */
export function openStatus(hours: OpeningHours, now: Date = new Date()): { open: boolean; text: string } | null {
  if (!hours.week.some((d) => d.intervals.length > 0)) return null;
  const here = gymNow(now);
  const closedToday = closureOn(hours, here.date);
  if (!closedToday) {
    const current = intervalsOn(hours, here.day).find((i) => minutes(i.opens) <= here.minutes && here.minutes < minutes(i.closes));
    if (current) return { open: true, text: t("Open now · closes at {time}", { time: timeLabel(current.closes) }) };
    const later = intervalsOn(hours, here.day).find((i) => minutes(i.opens) > here.minutes);
    if (later) return { open: false, text: t("Closed · opens at {time}", { time: later.opens }) };
  }
  for (let n = 1; n <= 14; n++) {
    const date = addDays(here.date, n);
    const day = ((here.day - 1 + n) % 7) + 1;
    const first = intervalsOn(hours, day)[0];
    if (!first || closureOn(hours, date)) continue;
    const when = n === 1 ? t("tomorrow") : dayName(day);
    const text = t("Closed · opens {day} at {time}", { day: when, time: first.opens });
    return { open: false, text: closedToday?.note ? `${t("Closed today: {reason}", { reason: closedToday.note })} · ${t("opens {day} at {time}", { day: when, time: first.opens })}` : text };
  }
  return { open: false, text: closedToday?.note ? t("Closed today: {reason}", { reason: closedToday.note }) : t("Closed") };
}

/** "24–26 Dec" or "24 Dec" for one day. */
export function closureDates(c: Closure): string {
  const fmt = (d: string) => new Date(`${d}T12:00:00Z`).toLocaleDateString(localeTag(), { day: "numeric", month: "short", timeZone: "UTC" });
  return c.from === c.to ? fmt(c.from) : t("{from} – {to}", { from: fmt(c.from), to: fmt(c.to) });
}

/** The hours on the gym's Info tab: whether it is open now, the week (today picked out), the closures to come. */
export function OpeningHoursCard({ hours }: { hours: OpeningHours }) {
  const here = gymNow();
  const status = openStatus(hours);
  const closures = upcomingClosures(hours, here.date);
  return (
    <section className="card opening-hours" aria-labelledby="hours-title">
      <h2 id="hours-title" className="section__title"><Clock aria-hidden className="title-icon" /> {t("Opening hours")}</h2>
      {status && <p className={`opening-hours__status ${status.open ? "is-open" : ""}`}>{status.text}</p>}
      {hours.week.some((d) => d.intervals.length > 0) && (
        <dl className="opening-hours__week">
          {DAYS.map((day) => {
            const intervals = intervalsOn(hours, day);
            return (
              <div key={day} className={`opening-hours__day ${day === here.day ? "is-today" : ""}`}>
                <dt>{dayName(day)}</dt>
                <dd>{intervals.length === 0 ? t("Closed") : intervals.map((i) => `${i.opens}–${timeLabel(i.closes)}`).join(", ")}</dd>
              </div>
            );
          })}
        </dl>
      )}
      {closures.length > 0 && (
        <ul className="opening-hours__closures" aria-label={t("Closures")}>
          {closures.map((c) => (
            <li key={`${c.from}-${c.to}`}><CalendarOff aria-hidden /> <strong>{closureDates(c)}</strong>{c.note ? ` · ${c.note}` : ""}</li>
          ))}
        </ul>
      )}
    </section>
  );
}

export function useSetOpeningHours(gym: GymDetail) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (hours: OpeningHours) => api.put<OpeningHours | undefined>(`/api/gyms/${gym.id}/opening-hours`, hours),
    onSuccess: (saved) => {
      qc.setQueryData<GymDetail>(gymKeys.detail(gym.slug), (g) => (g ? { ...g, openingHours: saved ?? null } : g));
    },
  });
}

/** The editor in the gym's settings: each day open or closed with up to three opening times, and the closures. */
export function OpeningHoursEditor({ gym }: { gym: GymDetail }) {
  const save = useSetOpeningHours(gym);
  const toast = useToast();
  const today = gymNow().date;
  const [week, setWeek] = useState<OpeningDay[]>(() => DAYS.map((day) => ({ day, intervals: gym.openingHours?.week.find((d) => d.day === day)?.intervals ?? [] })));
  const [closures, setClosures] = useState<Closure[]>(() => upcomingClosures(gym.openingHours ?? { week: [], closures: [] }, today));

  const setDay = (day: number, intervals: OpeningInterval[]) => setWeek((w) => w.map((d) => (d.day === day ? { ...d, intervals } : d)));
  const setTimes = (day: number, k: number, part: Partial<OpeningInterval>) =>
    setDay(day, week.find((d) => d.day === day)!.intervals.map((x, i) => (i === k ? { ...x, ...part } : x)));
  const copyMonday = () => {
    const monday = week.find((d) => d.day === 1)!.intervals;
    setWeek((w) => w.map((d) => (d.day >= 2 && d.day <= 5 ? { ...d, intervals: monday.map((i) => ({ ...i })) } : d)));
  };

  function submit() {
    save.mutate({ week: week.filter((d) => d.intervals.length > 0), closures: closures.map((c) => ({ ...c, to: c.to || c.from })) }, {
      onSuccess: () => toast.success(t("Opening hours saved")),
      onError: (e) => toast.error(e instanceof ApiError && e.isValidation ? (Object.values(e.fieldErrors)[0]?.[0] ?? errorMessage(e)) : errorMessage(e)),
    });
  }

  return (
    <section className="card stack form--wide opening-editor" aria-labelledby="hours-editor-title">
      <h2 id="hours-editor-title" className="section__title">{t("Opening hours")}</h2>
      <p className="field__hint">{t("Shown on your gym's Info tab, with whether you are open right now. Times are Italian time.")}</p>
      <ul className="opening-editor__week">
        {week.map((d) => (
          <li key={d.day} className="opening-editor__day">
            <span className="opening-editor__name">{dayName(d.day)}</span>
            <div className="opening-editor__times">
              {d.intervals.length === 0 && <span className="list__sub">{t("Closed")}</span>}
              {d.intervals.map((i, k) => (
                <span key={k} className="opening-editor__interval">
                  <input type="time" className="field__input" value={i.opens} aria-label={t("{day}: opens", { day: dayName(d.day) })}
                    onChange={(e) => setTimes(d.day, k, { opens: e.target.value })} />
                  <span aria-hidden>–</span>
                  <input type="time" className="field__input" value={timeLabel(i.closes)} aria-label={t("{day}: closes", { day: dayName(d.day) })}
                    onChange={(e) => setTimes(d.day, k, { closes: e.target.value === "00:00" ? "24:00" : e.target.value })} />
                  <button type="button" className="icon-btn" aria-label={t("Remove these times")} onClick={() => setDay(d.day, d.intervals.filter((_, x) => x !== k))}><X aria-hidden /></button>
                </span>
              ))}
              {d.intervals.length < MAX_INTERVALS && (
                <button type="button" className="text-btn" onClick={() => setDay(d.day, [...d.intervals, d.intervals.length === 0 ? { opens: "10:00", closes: "23:00" } : { opens: "18:00", closes: "23:00" }])}>
                  <Plus aria-hidden /> {d.intervals.length === 0 ? t("Open this day") : t("Add times")}
                </button>
              )}
            </div>
          </li>
        ))}
      </ul>
      <div className="form__actions">
        <Button variant="ghost" onClick={copyMonday}>{t("Copy Monday to Tuesday–Friday")}</Button>
      </div>

      <h3 className="field__label">{t("Closures")}</h3>
      <p className="field__hint">{t("Holidays, works, events that close the walls. Past ones disappear by themselves.")}</p>
      <ul className="opening-editor__closures">
        {closures.map((c, k) => (
          <li key={k} className="opening-editor__closure">
            <label className="field"><span className="field__label">{t("From")}</span>
              <input type="date" className="field__input" value={c.from} min={today} onChange={(e) => setClosures((cs) => cs.map((x, i) => (i === k ? { ...x, from: e.target.value, to: x.to < e.target.value ? e.target.value : x.to } : x)))} /></label>
            <label className="field"><span className="field__label">{t("To")}</span>
              <input type="date" className="field__input" value={c.to} min={c.from || today} onChange={(e) => setClosures((cs) => cs.map((x, i) => (i === k ? { ...x, to: e.target.value } : x)))} /></label>
            <label className="field opening-editor__note"><span className="field__label">{t("Note")}</span>
              <input className="field__input" value={c.note ?? ""} maxLength={100} placeholder={t("e.g. Christmas")} onChange={(e) => setClosures((cs) => cs.map((x, i) => (i === k ? { ...x, note: e.target.value } : x)))} /></label>
            <button type="button" className="icon-btn" aria-label={t("Remove this closure")} onClick={() => setClosures((cs) => cs.filter((_, i) => i !== k))}><Trash2 aria-hidden /></button>
          </li>
        ))}
      </ul>
      <div className="form__actions">
        <Button variant="secondary" icon={<Plus aria-hidden />} onClick={() => setClosures((cs) => [...cs, { from: today, to: today, note: "" }])}>{t("Add a closure")}</Button>
        <Button icon={<Save aria-hidden />} loading={save.isPending} onClick={submit}>{t("Save opening hours")}</Button>
      </div>
    </section>
  );
}
