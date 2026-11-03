// scripts/browser-usage-hook.mjs
//
// Wird von run-guards.mjs über NODE_OPTIONS (--import) in jeden Testprozess und dessen
// Node-Kindprozesse geladen (infrastructure-t5m2b). Wer einen Browser startet, hinterlässt eine Marke
// für seine Testdatei (browser-usage.mjs).
//
// Umhüllt wird `child_process`, nicht Playwright: im Baum liegen mehrere Kopien von `playwright-core`
// (über `playwright`, `@playwright/test` und andere Pakete), jede mit eigenem `chromium`-Objekt. Alle
// starten den Browser über `child_process.spawn`, das ist die Stelle, die sie teilen. Umhüllt wird
// immer; ohne BB_BROWSER_USAGE_DIR setzt die Hülle nur keine Marke. Die synchronen Formen
// (`spawnSync`, `execSync`, `execFileSync`) bleiben unumhüllt, ein Browser startet nicht synchron.
import childProcess from "node:child_process";
import { syncBuiltinESMExports } from "node:module";
import { markBrowserUsage, usageOwner, isBrowserCommand, OWNER_ENV } from "./browser-usage.mjs";

const owner = usageOwner();
// Kindprozesse (etwa ein gestartetes Werkzeug) erben die Zuordnung zur Testdatei.
if (owner) process.env[OWNER_ENV] = owner;

for (const method of ["spawn", "execFile", "exec", "fork"]) {
  const original = childProcess[method];
  const wrapped = function (command, ...rest) {
    if (isBrowserCommand(command)) markBrowserUsage("browser");
    return original.call(this, command, ...rest);
  };
  // Eigenschaften des Originals mitnehmen, vor allem `util.promisify.custom` von `exec` und
  // `execFile`: ohne sie liefert `promisify(exec)` den String statt `{ stdout, stderr }`.
  Object.defineProperties(wrapped, Object.getOwnPropertyDescriptors(original));
  childProcess[method] = wrapped;
}
// Auch `import { spawn } from "node:child_process"` sieht die Hülle.
syncBuiltinESMExports();
