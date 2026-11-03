// scripts/settings-secrets.test.mjs
//
// Gerenderter Beleg für `infrastructure-s1r95`: die Settings-Seite zeigt gesetzte Secrets als
// "gesetzt" und speichert die Comdirect-Felder ohne neue Eingabe. Echter Vite-Client
// gegen gemockte Settings-API (erfundene Werte). Dass der Server die leeren Felder als "alten
// Wert behalten" liest, belegt `src/Tests/SettingsSecretsTests.fs`.
//
// infrastructure-t5m2b: hier bleiben das Speichern als Happy Path (was Fable.Remoting wirklich
// schickt) und "Bank trennen" am Handy mit Lage der Knöpfe und ohne seitliches Scrollen. Sperre,
// verlorene Antwort, Rückfrage, TAN-Block und "nicht lesbar" prüfen `ClientCommandTests.fs` und
// `BankConnectionTests.fs` ("Einstellungen: Stand, Trennen und Rückfrage im Client").
//
// Ausführen: node --test scripts/settings-secrets.test.mjs
// (Teil von `npm run test:scripts`.)

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer } from "./screenshot-route.mjs";

const settings = {
  Comdirect: { HasClientId: true, HasClientSecret: true, HasUsername: true, HasPassword: true, AccountId: "konto-1" },
  Ynab: { HasToken: true, DefaultBudgetId: null, DefaultAccountId: null, QuickAddAccountId: null },
  Sync: { DaysToFetch: 30 },
};
const json = (body) => ({ contentType: "application/json", body: JSON.stringify(body) });

let frontend, port, browser;
before(async () => {
  port = await getFreePort();
  frontend = await startFrontend(port, undefined);
  browser = await chromium.launch();
});
after(async () => {
  await browser?.close();
  await stopServer(frontend);
});

test("[flow:zugangsdaten-speichern] Speichern schickt den neuen Benutzernamen und lässt die übrigen Zugangsdaten leer", async () => {
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
  page.setDefaultTimeout(30000);
  const saved = [];
  await page.route("**/api/**", (route) => route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked"}' }));
  await page.route("**/api/SettingsApi/getSettings", (r) => r.fulfill(json(settings)));
  await page.route("**/api/SettingsApi/getLenaFlagSettings", (r) => r.fulfill(json({ SavedColor: null, SuggestedColor: null })));
  await page.route("**/api/SyncApi/getBankConnectionStatus", (r) => r.fulfill(json("BankDisconnected")));
  await page.route("**/api/SettingsApi/saveComdirectCredentials", (r) => {
    saved.push(r.request().postDataJSON());
    r.fulfill(json({ Ok: null }));
  });

  await page.goto(`http://localhost:${port}/#/settings`);
  const card = page.getByRole("heading", { name: "Comdirect Verbindung" }).locator("xpath=ancestor::div[2]");
  await card.getByText("Client-Secret", { exact: true }).waitFor();
  assert.equal(await card.getByText("gesetzt", { exact: true }).count(), 4, "alle vier Zugangsdaten stehen als gesetzt da");

  await card.getByRole("button", { name: "Bearbeiten" }).click();
  await card.getByPlaceholder("Deine Zugangsnummer").waitFor();
  // Der Hinweis steht unter dem Feld, das leere Feld zeigt keinen gespeicherten Wert.
  assert.equal(await card.getByText("Gesetzt. Leer lassen, um den Wert zu behalten").count(), 4);
  assert.equal(await card.getByPlaceholder("Deine Zugangsnummer").inputValue(), "");
  assert.equal(await card.getByPlaceholder("Deine API Client-ID").inputValue(), "");
  await card.getByPlaceholder("Deine Zugangsnummer").fill("neuer-name");
  await card.getByRole("button", { name: "Zugangsdaten speichern" }).click();

  await card.getByRole("button", { name: "Bearbeiten" }).waitFor();
  // Fable.Remoting schickt die Argumente als Array.
  const [credentials] = saved[0];
  assert.equal(credentials.Username, "neuer-name");
  assert.equal(credentials.ClientId, "");
  assert.equal(credentials.ClientSecret, "");
  assert.equal(credentials.Password, "");
  assert.equal(await card.getByText("gesetzt", { exact: true }).count(), 4, "nach dem Speichern weiter gesetzt");
  await page.close();
});

// banking-import-bftq0: Das Badge zeigt den Stand, "Bank trennen" fragt einmal nach, ein Fehler bleibt stehen.
test("[flow:bank-trennen] Bank trennen am Handy: Abbrechen steht vorn, erst scheitert das Löschen sichtbar, der zweite Versuch trennt", async () => {
  const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
  page.setDefaultTimeout(30000);
  let status = "BankConnected";
  const failed = { Error: { DatabaseError: ["disconnect_bank", "Gespeicherte Bankverbindung nicht gelöscht (SqliteException). Bank trennen wiederholen."] } };
  let answers = [failed, { Ok: null }];
  let calls = 0;
  await page.route("**/api/**", (route) => route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked"}' }));
  await page.route("**/api/SettingsApi/getSettings", (r) => r.fulfill(json(settings)));
  await page.route("**/api/SettingsApi/getLenaFlagSettings", (r) => r.fulfill(json({ SavedColor: null, SuggestedColor: null })));
  await page.route("**/api/SyncApi/getBankConnectionStatus", (r) => r.fulfill(json(status)));
  await page.route("**/api/SyncApi/disconnectBank", (r) => {
    calls += 1;
    const answer = answers.shift();
    // Wie der Server: scheitert das Löschen, bleibt die Verbindung bestehen.
    if (answer.Ok !== undefined) status = "BankDisconnected";
    r.fulfill(json(answer));
  });

  await page.goto(`http://localhost:${port}/#/settings`);
  const card = page.getByRole("heading", { name: "Comdirect Verbindung" }).locator("xpath=ancestor::div[2]");
  await card.getByText("Verbunden", { exact: true }).waitFor();
  await card.getByText("Nächster Sync ohne TAN").waitFor();

  await card.getByRole("button", { name: "Bank trennen" }).click();
  assert.equal(calls, 0, "erst nach der Rückfrage");
  const cancelBox = await card.getByRole("button", { name: "Abbrechen" }).boundingBox();
  const confirmBox = await card.getByRole("button", { name: "Wirklich trennen" }).boundingBox();
  assert.ok(cancelBox.x < confirmBox.x, "der Doppeltipp trifft den harmlosen Knopf");
  await card.getByRole("button", { name: "Wirklich trennen" }).click();
  await card.getByText("Gespeicherte Bankverbindung nicht gelöscht", { exact: false }).first().waitFor();
  await card.getByText("Verbunden", { exact: true }).waitFor();

  await card.getByRole("button", { name: "Bank trennen" }).click();
  await card.getByRole("button", { name: "Wirklich trennen" }).click();
  await card.getByRole("alert").waitFor({ state: "detached" });
  await card.getByText("Nicht verbunden", { exact: true }).waitFor();
  assert.equal(await card.getByRole("button", { name: "Bank trennen" }).count(), 0, "nichts mehr zu trennen");
  assert.equal(calls, 2);
  assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), "kein seitliches Scrollen");
  await page.close();
});
