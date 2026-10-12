import { isNativeApp } from "@/lib/native";
import { t } from "@/i18n/i18n";

/**
 * What to call the device notifications arrive on, and where to fix them. In the store app it is the phone, and the
 * permission lives in the phone's settings; on the website it is this browser, and the permission belongs to the site
 * (a computer, as often as a phone). Telling someone at a desk to look in their phone's settings sends them to the
 * wrong place.
 */
export function pushWording() {
  if (isNativeApp()) {
    return {
      title: t("On your phone"),
      toggle: t("Notifications on this phone"),
      on: t("Phone notifications on"),
      off: t("Phone notifications off"),
      denied: t("Your phone refused notifications. Turn them on for BoulderTime in the phone's settings."),
      delivered: t("Sent. It should appear within a few seconds, even with the app open. If it doesn't, the phone isn't letting it through: check BoulderTime's notifications in the phone's settings and any Focus mode."),
    };
  }
  return {
    title: t("In this browser"),
    toggle: t("Notifications in this browser"),
    on: t("Browser notifications on"),
    off: t("Browser notifications off"),
    denied: t("This browser is blocking notifications from BoulderTime. Allow them in the site's settings (the icon beside the address), then try again."),
    delivered: t("Sent. It should appear within a few seconds. If it doesn't, the browser or the computer isn't letting it through: check the site's notification permission, the system's notification settings and Do Not Disturb."),
  };
}
