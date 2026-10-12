import "@testing-library/jest-dom/vitest";

// jsdom doesn't scroll; the app scrolls to the top on navigation. A no-op keeps the test output free of its error.
Object.defineProperty(window, "scrollTo", { value: () => {}, writable: true });
