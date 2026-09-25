/*
 * Push handling, merged into the generated service worker.
 * Kept tiny and dependency-free: this code runs with the app closed, so it must never fail.
 */
self.addEventListener("push", (event) => {
  let data = { title: "BoulderTime", body: "", url: "/" };
  try {
    if (event.data) data = { ...data, ...event.data.json() };
  } catch {
    // A payload we can't read still deserves a notification: the phone was told something happened.
  }
  event.waitUntil(
    self.registration.showNotification(data.title, {
      body: data.body,
      icon: "/assets/app-icon-192.png",
      badge: "/assets/app-icon-192.png",
      // Notifications about the same thing replace each other instead of piling up.
      tag: data.tag || undefined,
      data: { url: data.url || "/" },
    }),
  );
});

self.addEventListener("notificationclick", (event) => {
  event.notification.close();
  const target = new URL(event.notification.data?.url || "/", self.location.origin).href;
  event.waitUntil(
    self.clients.matchAll({ type: "window", includeUncontrolled: true }).then((clients) => {
      // Reuse the open app if there is one: opening a second window loses where the person was.
      for (const client of clients) {
        if (client.url.startsWith(self.location.origin) && "focus" in client) {
          client.navigate(target);
          return client.focus();
        }
      }
      return self.clients.openWindow(target);
    }),
  );
});
