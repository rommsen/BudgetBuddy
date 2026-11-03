// scripts/test-count-reporter.mjs
//
// Reporter für `node --test` (infrastructure-t5m2b, infrastructure-b3t9k): je gelaufenem Test eine
// JSON-Zeile mit Datei, Verschachtelung, Art (Test oder Suite) und Name. run-guards.mjs prüft daraus
// die Kennungen der Browser-Tests und zählt sie.
export default async function* testCountReporter(source) {
  for await (const event of source) {
    if (event.type !== "test:pass" && event.type !== "test:fail") continue;
    const { file, nesting, name, details } = event.data;
    yield `${JSON.stringify({ file, nesting, name, kind: details?.type ?? "test" })}\n`;
  }
}
