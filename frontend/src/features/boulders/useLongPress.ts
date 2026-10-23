import { useEffect, useRef, type MouseEvent } from "react";

const LONG_PRESS_MS = 450;

/**
 * Press and hold a boulder (a card or a row) to start selecting, the way a phone's home screen arms its icons.
 * `pressHandlers` go on the link while not selecting, `toggleHandlers` on the selectable element that takes its place.
 * The hold lets go of any text the press began to select, the link isn't opened by the same press, and lifting the
 * finger over the selectable element now under it doesn't toggle it straight back off; a new press, or the keyboard,
 * toggles as always.
 */
export function useLongPress(onLongPress: (() => void) | undefined, onToggle: (() => void) | undefined) {
  const timer = useRef<number | null>(null);
  const fired = useRef(false);
  const releasing = useRef(false);

  const cancel = () => { if (timer.current !== null) { window.clearTimeout(timer.current); timer.current = null; } };
  useEffect(() => cancel, []);

  const start = () => {
    if (!onLongPress) return;
    fired.current = false;
    timer.current = window.setTimeout(() => {
      fired.current = true;
      // Whatever the press began to select (a name, a sector) is let go: the press selected the boulder, not text.
      window.getSelection()?.removeAllRanges();
      onLongPress();
    }, LONG_PRESS_MS);
  };

  const pressHandlers = onLongPress
    ? {
        onPointerDown: start,
        onPointerUp: cancel,
        onPointerLeave: cancel,
        onPointerCancel: cancel,
        // The hold has already selected the boulder, so letting the tap through would open it at the same time.
        onClick: (e: MouseEvent) => { if (fired.current) { e.preventDefault(); fired.current = false; } },
        onContextMenu: (e: MouseEvent) => { if (timer.current !== null || fired.current) e.preventDefault(); },
        draggable: false,
      }
    : {};

  const toggleHandlers = {
    onPointerDown: () => { releasing.current = false; fired.current = false; },
    onPointerUp: () => { if (fired.current) { fired.current = false; releasing.current = true; } },
    onClick: () => {
      if (releasing.current || fired.current) { releasing.current = false; fired.current = false; return; }
      onToggle?.();
    },
  };

  return { pressHandlers, toggleHandlers };
}
