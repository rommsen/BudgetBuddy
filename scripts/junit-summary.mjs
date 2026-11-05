// scripts/junit-summary.mjs
//
// Liest den JUnit-Bericht, den Expecto mit `--junit-summary` schreibt
// (infrastructure-d1dwb). Aufrufer ist scripts/verify-lib.sh, Tests stehen in
// scripts/verify-lib.test.mjs.
//
//   node scripts/junit-summary.mjs count <bericht> <Total|Passed|Failed|Skipped>
//   node scripts/junit-summary.mjs tests <bericht> <failed|skipped>
//   node scripts/junit-summary.mjs message <bericht> <n>
//
// Form des Berichts (Expecto 10.2.1): eine einzige Zeile XML mit BOM,
// <testsuites><testsuite name="Tests"> ohne Zähl-Attribute. Jeder Test ist ein
// <testcase name="[Liste; Untergruppe; Test]">, rot mit <failure message="…">
// (Expect schlägt fehl) oder <error message="…"> (Ausnahme), übersprungen mit
// <skipped message="…">. Rote Tests zählen beide Arten.
//
// Ein fehlender, leerer oder fremder Bericht ergibt keine Ausgabe und Exit 1:
// verify.sh wertet ein unlesbares Ergebnis als rot, nie als Nullwert.
import fs from "node:fs";

function decodeEntities(s) {
  return s.replace(/&(#x[0-9a-fA-F]+|#[0-9]+|amp|lt|gt|quot|apos);/g, (_, e) => {
    if (e[0] === "#") {
      const code = e[1] === "x" ? parseInt(e.slice(2), 16) : parseInt(e.slice(1), 10);
      return String.fromCodePoint(code);
    }
    return { amp: "&", lt: "<", gt: ">", quot: '"', apos: "'" }[e];
  });
}

// Anzeige wie früher in der trx-Ausgabe: ohne eckige Klammern, Ebenen mit "." getrennt.
function displayName(raw) {
  let name = decodeEntities(raw);
  if (name.startsWith("[") && name.endsWith("]")) name = name.slice(1, -1);
  return name.split("; ").join(".");
}

function attr(tag, name) {
  const m = tag.match(new RegExp(`\\s${name}="([^"]*)"`));
  return m ? m[1] : null;
}

function parseJUnit(xml) {
  if (!/<testsuites[\s>]/.test(xml)) return null;
  const cases = [];
  // Ein testcase ist entweder selbstschließend oder hat einen Rumpf bis </testcase>.
  const re = /<testcase\b([^>]*?)(\/>|>([\s\S]*?)<\/testcase>)/g;
  let m;
  while ((m = re.exec(xml)) !== null) {
    const body = m[3] ?? "";
    const red = body.match(/<(failure|error)\b[^>]*>/);
    const skipped = /<skipped\b/.test(body);
    const nameRaw = attr(m[1], "name") ?? "";
    cases.push({
      name: displayName(nameRaw),
      outcome: red ? "failed" : skipped ? "skipped" : "passed",
      message: red ? decodeEntities(attr(red[0], "message") ?? "") : "",
    });
  }
  return cases;
}

// Erste nichtleere Zeile einer Meldung, ohne ANSI-Farbcodes von Expecto.
function firstLine(message) {
  const plain = message.replace(/\x1B\[[0-9;]*m/g, "");
  return plain.split(/\r?\n/).map((l) => l.trim()).find((l) => l !== "") ?? "";
}

function main(argv) {
  const [mode, file, arg] = argv;
  let xml;
  try {
    xml = fs.readFileSync(file, "utf8");
  } catch {
    return 1;
  }
  const cases = parseJUnit(xml);
  if (cases === null) return 1;
  const failed = cases.filter((c) => c.outcome === "failed");
  const skipped = cases.filter((c) => c.outcome === "skipped");
  if (mode === "count") {
    const counts = {
      Total: cases.length,
      Passed: cases.length - failed.length - skipped.length,
      Failed: failed.length,
      Skipped: skipped.length,
    };
    if (!(arg in counts)) return 2;
    process.stdout.write(String(counts[arg]));
    return 0;
  }
  if (mode === "tests") {
    const list = arg === "failed" ? failed : arg === "skipped" ? skipped : null;
    if (list === null) return 2;
    for (const c of list) process.stdout.write(c.name + "\n");
    return 0;
  }
  if (mode === "message") {
    const c = failed[Number(arg) - 1];
    if (c) process.stdout.write(firstLine(c.message));
    return 0;
  }
  return 2;
}

process.exitCode = main(process.argv.slice(2));
