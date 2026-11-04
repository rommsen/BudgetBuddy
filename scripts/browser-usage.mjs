// scripts/browser-usage.mjs
//
// Laufzeitzählung der Browser-Tests (infrastructure-t5m2b, Iteration 2). Eine statische Zählung über
// den Quelltext ließ sich umgehen (`it(`, `describe`, Schleifen, `forEach`, `t.test(`, umbenannter
// Import). Gezählt wird deshalb, was wirklich lief:
//   - `browser-usage-hook.mjs` wird über NODE_OPTIONS in jeden Testprozess und dessen Kindprozesse
//     geladen. Startet ein Prozess einen Browser (über `child_process`, gleich aus welchem Paket) oder
//     ruft `startFrontend` auf, legt er eine Marke für die Testdatei ab, die ihn gestartet hat.
//   - `test-count-reporter.mjs` schreibt je gelaufenem Test eine Zeile mit Datei und Verschachtelung.
//   - `run-guards.mjs` prüft die Kennung jedes Blatt-Tests der markierten Dateien und meldet die Zahl
//     je Kennung (infrastructure-b3t9k). Eine feste Obergrenze gibt es nicht mehr.

import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";

export const USAGE_DIR_ENV = "BB_BROWSER_USAGE_DIR";
export const OWNER_ENV = "BB_BROWSER_USAGE_OWNER";

/// Ein Dateipfad in seiner echten Form: Reporter und `argv` nennen dieselbe Datei sonst verschieden
/// (macOS: /var ist ein Verweis auf /private/var).
export function canonical(file) {
  const resolved = path.resolve(file);
  try {
    return fs.realpathSync(resolved);
  } catch {
    return resolved;
  }
}

/// Die Testdatei, der dieser Prozess zugerechnet wird: geerbt vom Testprozess oder die eigene Datei.
export function usageOwner(env = process.env, argv = process.argv) {
  if (env[OWNER_ENV]) return env[OWNER_ENV];
  if (env.NODE_TEST_CONTEXT && /\.test\.mjs$/.test(argv[1] ?? "")) return canonical(argv[1]);
  return null;
}

/// Startet dieser Befehl einen Browser? Playwright startet Chromium (auch `headless_shell`), Firefox
/// und WebKit (`pw_run.sh`) aus seinem Cache; erkannt wird am Namen der ausführbaren Datei, bei einer
/// Befehlszeile am ersten Wort.
export function isBrowserCommand(command) {
  if (typeof command !== "string") return false;
  // `exec` bekommt eine ganze Befehlszeile: die Datei ist dann das erste Wort. Ein Pfad mit Leerzeichen,
  // den es so gibt, bleibt ganz.
  const executable = fs.existsSync(command) ? command : command.trim().split(/\s+/)[0];
  const name = path.basename(executable);
  return /chrom(e|ium)|headless_shell|firefox|webkit|pw_run|msedge/i.test(name) || /ms-playwright/.test(executable);
}

/// Legt eine Marke ab: die Testdatei hat einen Browser oder ein Frontend gestartet.
export function markBrowserUsage(kind, env = process.env, argv = process.argv) {
  const dir = env[USAGE_DIR_ENV];
  const owner = usageOwner(env, argv);
  if (!dir || !owner) return;
  const id = crypto.createHash("sha1").update(owner).digest("hex");
  fs.writeFileSync(path.join(dir, `${id}-${kind}`), owner);
}

/// Die Testdateien mit Marke.
export function markedFiles(dir) {
  return new Set(fs.readdirSync(dir).map((f) => canonical(fs.readFileSync(path.join(dir, f), "utf8"))));
}

/// Die Blatt-Tests aus den Zeilen des Reporters, je Test Datei und Name. Ein Test mit Untertests ist
/// selbst kein Blatt, seine Untertests sind es. Suiten (`describe`) sind keine Blätter, ihre Tests schon.
export function leafTests(lines) {
  const leaves = [];
  const lastNesting = new Map();
  for (const line of lines) {
    if (!line.trim()) continue;
    const event = JSON.parse(line);
    const file = canonical(event.file ?? "");
    const parent = lastNesting.get(file) === event.nesting + 1;
    lastNesting.set(file, event.nesting);
    if (event.kind !== "test" || parent) continue;
    leaves.push({ file, name: event.name ?? "" });
  }
  return leaves;
}

/// Die Kennungen eines Browser-Tests (Doktrin Punkt 5): was er prüft, das nur der Browser kann.
export const TAGS = {
  layout: "Maße, Breiten, Abschneiden, sticky, Viewport, Trefferflächen, berechnete Farben",
  fokus: "Fokus im echten DOM",
  tastatur: "Tastatur-Ereignisse im echten DOM",
  scroll: "Scrollen und Scrollposition",
  fable: "gilt nur im Fable-Kompilat (Dezimalrechnung, Datumsformate, Netzfehler des Remoting-Proxys)",
  flow: "der eine Happy Path eines Nutzerflusses aus FLOWS",
};

/// Die Nutzerflüsse mit je einem Happy Path im Browser (`[flow:<name>]`). Ein neuer Flow ist eine neue
/// Zeile hier; je Flow läuft höchstens ein Test.
export const FLOWS = {
  "budget-oeffnen": "Die Budget-Seite ist am Handy über die Tab-Leiste erreichbar.",
  "geld-zuweisen": "Am Handy eine Kategorie wählen, einen Betrag zuweisen und zur nächsten springen.",
  "geld-verschieben": "Am Handy Geld von einer Kategorie an eine andere abgeben.",
  "verschiebung-erneut-senden": "Eine gescheiterte Verschiebung über den Toast erneut senden.",
  "targets-anwenden": "Die Targets eines Monats als Vorschau prüfen und übernehmen.",
  "target-aendern": "Am Handy im Tab Target den Betrag eines Targets ändern und speichern.",
  "budget-zur-buchung": "Aus einer Kategorie im Budget in eine Buchung im Kassenbuch springen und zurück.",
  "kategorien-neu-laden": "Nach einem Ladefehler im Kategorie-Picker die Kategorien erneut laden.",
  "buchung-oeffnen": "Im Kassenbuch eine Buchung im Detail öffnen.",
  "konto-wechseln": "Im Kassenbuch am Handy das Konto wechseln.",
  "kassenbuch-filtern": "Im Kassenbuch nach Kategorien filtern und den Filter zurücknehmen.",
  "kassenbuch-sortieren": "Im Kassenbuch nach Betrag sortieren, umkehren und zurück nach Datum.",
  "zuletzt-verwendet-merken": "Der Kategorie-Filter behält Zuletzt verwendet über ein Neuladen.",
  "kategorie-aendern": "Die Kategorie einer Buchung ändern und rückgängig machen.",
  "memo-setzen": "Einer Buchung ein Memo geben.",
  "aufteilen": "Eine Buchung aufteilen, mit Übertrag, und rückgängig machen.",
  "barabhebung-aufteilen": "Einen Teil einer Buchung als Barabhebung aufs Bar-Konto übertragen.",
  "buchung-pruefen": "Eine Buchung an der Zeile als geprüft markieren.",
  "pruefliste-abarbeiten": "Die letzte Buchung unter Zu prüfen prüfen, danach steht Alles geprüft.",
  "lena-fragen": "Für eine Buchung Lena fragen.",
  "lena-fragen-mehrere": "Für mehrere ausgewählte Buchungen Lena fragen.",
  "mehrfachauswahl": "Am Handy Buchungen auswählen, alle wählen und die Auswahl aufheben.",
  "bestell-link-oeffnen": "Den Bestell-Link einer Buchung aus dem Detail öffnen.",
  "sync-starten": "Den Abgleich mit der Bank über den TAN-Schritt bis zur Prüfung führen.",
  "import-lena-fragen": "An einer Importzeile Lena fragen und den Import abschließen.",
  "import-uebertrag": "Eine Importzeile als Übertrag aufs Bar-Konto aufteilen und speichern.",
  "regel-aus-import": "Aus einer Importzeile eine Regel anlegen, das Muster wird geprüft.",
  "regel-anlegen": "Auf der Regelseite eine neue Regel mit Kategorie anlegen.",
  "regeln-importieren": "Am Handy Regeln aus einer Datei importieren.",
  "zugangsdaten-speichern": "In den Einstellungen Zugangsdaten speichern.",
  "bank-trennen": "In den Einstellungen die Bankverbindung trennen.",
  "design-system-export": "Das Design-System als Bundle exportieren.",
};

const TAG_PREFIX = /^\[([^\]]*)\] (?=\S)/;
const FLOW_NAME = /^flow:([a-z0-9]+(?:-[a-z0-9]+)*)$/;

/// Die Kennung am Anfang eines Testnamens: `{ tag }` oder `{ tag: "flow", flow }`, sonst null. Ungültig
/// sind ein Name ohne Kennung, eine unbekannte Kennung, eine Kennung nicht am Anfang, `[flow:]` ohne
/// Namen und eine zweite Kennung direkt hinter der ersten.
export function testTag(name) {
  const match = TAG_PREFIX.exec(typeof name === "string" ? name : "");
  if (!match) return null;
  if (name.slice(match[0].length).startsWith("[")) return null;
  const tag = match[1];
  const flow = FLOW_NAME.exec(tag);
  if (flow) return { tag: "flow", flow: flow[1] };
  return tag !== "flow" && Object.hasOwn(TAGS, tag) ? { tag } : null;
}

/// Die Blatt-Tests der markierten Dateien.
export function browserLeaves(leaves, marked) {
  return leaves.filter((leaf) => marked.has(leaf.file));
}

/// Verstöße gegen die Kennungsregel: fehlende oder unbekannte Kennung, Flow außerhalb der Liste, zwei
/// Tests für denselben Flow (auch über Dateien hinweg). Leer, wenn alles stimmt.
export function tagViolations(leaves, flows = FLOWS) {
  const where = (leaf) => `${path.basename(leaf.file)}: "${leaf.name}"`;
  const violations = [];
  const byFlow = new Map();
  for (const leaf of leaves) {
    const parsed = testTag(leaf.name);
    if (!parsed) {
      violations.push(`${where(leaf)} hat keine gültige Kennung (${Object.keys(TAGS).map((t) => (t === "flow" ? "[flow:<name>]" : `[${t}]`)).join(", ")})`);
    } else if (parsed.tag === "flow") {
      if (!Object.hasOwn(flows, parsed.flow)) violations.push(`${where(leaf)}: Flow "${parsed.flow}" steht nicht in FLOWS (scripts/browser-usage.mjs)`);
      byFlow.set(parsed.flow, [...(byFlow.get(parsed.flow) ?? []), leaf]);
    }
  }
  for (const [flow, tests] of byFlow) {
    if (tests.length > 1) violations.push(`Flow "${flow}" hat ${tests.length} Tests, erlaubt ist einer: ${tests.map(where).join("; ")}`);
  }
  return violations;
}

/// Zahl der Browser-Tests je Kennung und Summe. Alle Flows zählen unter `flow`, ungültige Namen unter
/// `ohne`.
export function tagSummary(leaves) {
  const counts = {};
  for (const leaf of leaves) {
    const tag = testTag(leaf.name)?.tag ?? "ohne";
    counts[tag] = (counts[tag] ?? 0) + 1;
  }
  return { counts, total: leaves.length };
}

/// Die Zeile im Gate: "Browser-Tests je Kennung: layout 2, flow 1, Summe 3".
export function summaryLine({ counts, total }) {
  const order = [...Object.keys(TAGS), "ohne"].filter((t) => counts[t]);
  return `Browser-Tests je Kennung: ${order.map((t) => `${t} ${counts[t]}`).join(", ")}${order.length ? ", " : ""}Summe ${total}`;
}
