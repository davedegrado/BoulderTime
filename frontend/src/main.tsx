import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import "@/styles/tokens.css";
import "@/styles/global.css";

const root = createRoot(document.getElementById("root")!);

// Import lazily so a missing env var renders a readable message instead of a blank page.
import("@/app/App")
  .then(({ App }) => root.render(<StrictMode><App /></StrictMode>))
  .catch((err: Error) => {
    root.render(
      <div className="state state--error" role="alert">
        <h1 className="state__title">BoulderTime couldn't start</h1>
        <p className="state__text">{err.message}</p>
      </div>,
    );
  });
