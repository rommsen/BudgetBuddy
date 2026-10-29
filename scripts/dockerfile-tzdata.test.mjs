// scripts/dockerfile-tzdata.test.mjs
//
// Wächter für infrastructure-b85yv: Das Runtime-Image (aspnet-alpine) bringt
// keine Zeitzonendatenbank mit. Ohne tzdata kennt .NET Europe/Berlin nicht, und
// die Bob-Tagesgrenze (ADR 0068) liefe auf einem ersatzweise gerechneten Offset.
// Der Server bricht dann beim Start ab; dieser Wächter fängt es schon im Dockerfile.
//
//   node --test scripts/dockerfile-tzdata.test.mjs
//
// Braucht keinen Browser und keinen Vite-Build.

import { test } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));

/// Der Text der Stage `AS runtime`: ab ihrer FROM-Zeile bis zum nächsten FROM
/// oder zum Dateiende. null, wenn es keine solche Stage gibt.
export function runtimeStage(dockerfile) {
  const lines = dockerfile.split(/\r?\n/);
  const start = lines.findIndex((l) => /^\s*FROM\s.+\sAS\s+runtime\s*$/i.test(l));
  if (start < 0) return null;
  const rest = lines.slice(start + 1);
  const next = rest.findIndex((l) => /^\s*FROM\s/i.test(l));
  return (next < 0 ? rest : rest.slice(0, next)).join("\n");
}

/// Installiert die Runtime-Stage tzdata per `apk add`? Fortsetzungszeilen (\)
/// werden zusammengezogen, Kommentarzeilen zählen nicht.
export function runtimeInstallsTzdata(dockerfile) {
  const stage = runtimeStage(dockerfile);
  if (stage === null) return false;
  return stage
    .replace(/\\\r?\n/g, " ")
    .split(/\r?\n/)
    .filter((l) => !/^\s*#/.test(l))
    .some((l) => /\bapk\s+add\b/.test(l) && /\btzdata\b/.test(l));
}

test("das echte Dockerfile installiert tzdata in der Runtime-Stage", () => {
  const dockerfile = fs.readFileSync(path.join(here, "..", "Dockerfile"), "utf8");
  assert.ok(runtimeInstallsTzdata(dockerfile), "RUN apk add ... tzdata fehlt in der Stage AS runtime");
});

test("ein Dockerfile ohne tzdata in der Runtime-Stage wird erkannt", () => {
  const ohne = [
    "FROM node:22-alpine AS build",
    "RUN apk add --no-cache tzdata",
    "FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime",
    "RUN apk add --no-cache curl",
  ].join("\n");
  assert.equal(runtimeInstallsTzdata(ohne), false);
});

test("tzdata in einer früheren Stage zählt nicht, ein Kommentar auch nicht", () => {
  const kommentar = [
    "FROM x AS runtime",
    "# apk add tzdata",
    "RUN apk add --no-cache curl",
  ].join("\n");
  assert.equal(runtimeInstallsTzdata(kommentar), false);
});

test("tzdata in der Runtime-Stage wird erkannt, auch mit Fortsetzungszeile", () => {
  const mit = ["FROM x AS runtime", "RUN apk add --no-cache \\", "    curl tzdata"].join("\n");
  assert.equal(runtimeInstallsTzdata(mit), true);
});
