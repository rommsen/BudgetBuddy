// scripts/settings-secrets.test.mjs
//
// Gerenderter Beleg für `infrastructure-s1r95`: die Settings-Seite zeigt gesetzte Secrets als
// "gesetzt" und speichert die Comdirect-Felder ohne neue Secret-Eingabe. Echter Vite-Client
// gegen gemockte Settings-API (erfundene Werte). Dass der Server die leeren Felder als "alten
// Wert behalten" liest, belegt `src/Tests/SettingsSecretsTests.fs`.
//
// Ausführen: node --test scripts/settings-secrets.test.mjs
// (Teil von `npm run test:scripts`.)

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer } from "./screenshot-route.mjs";

const settings = {
  Comdirect: { ClientId: "client-id-1234", HasClientSecret: true, Username: "alter-name", HasPassword: true, AccountId: "konto-1" },
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

test("Speichern ohne Secret-Eingabe schickt geänderte Felder und leere Secrets", async () => {
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
  page.setDefaultTimeout(30000);
  const saved = [];
  await page.route("**/api/**", (route) => route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked"}' }));
  await page.route("**/api/SettingsApi/getSettings", (r) => r.fulfill(json(settings)));
  await page.route("**/api/SettingsApi/getLenaFlagSettings", (r) => r.fulfill(json({ SavedColor: null, SuggestedColor: null })));
  await page.route("**/api/SettingsApi/saveComdirectCredentials", (r) => {
    saved.push(r.request().postDataJSON());
    r.fulfill(json({ Ok: null }));
  });

  await page.goto(`http://localhost:${port}/#/settings`);
  const card = page.getByRole("heading", { name: "Comdirect Verbindung" }).locator("xpath=ancestor::div[2]");
  await card.getByText("Client-Secret", { exact: true }).waitFor();
  assert.equal(await card.getByText("gesetzt", { exact: true }).count(), 2, "Client-Secret und PIN stehen als gesetzt da");

  await card.getByRole("button", { name: "Bearbeiten" }).click();
  await card.getByPlaceholder("Deine Zugangsnummer").fill("neuer-name");
  await card.getByRole("button", { name: "Zugangsdaten speichern" }).click();

  await card.getByRole("button", { name: "Bearbeiten" }).waitFor();
  // Fable.Remoting schickt die Argumente als Array.
  const [credentials] = saved[0];
  assert.equal(credentials.Username, "neuer-name");
  assert.equal(credentials.ClientSecret, "");
  assert.equal(credentials.Password, "");
  assert.equal(await card.getByText("gesetzt", { exact: true }).count(), 2, "nach dem Speichern weiter gesetzt");
  await page.close();
});
