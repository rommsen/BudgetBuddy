// scripts/test-count-reporter.mjs
//
// Reporter für `node --test` (infrastructure-t5m2b): je gelaufenem Test eine JSON-Zeile mit Datei,
// Verschachtelung und Art (Test oder Suite). run-guards.mjs zählt daraus die Browser-Tests.
export default async function* testCountReporter(source) {
  for await (const event of source) {
    if (event.type !== "test:pass" && event.type !== "test:fail") continue;
    const { file, nesting, details } = event.data;
    yield `${JSON.stringify({ file, nesting, kind: details?.type ?? "test" })}\n`;
  }
}
