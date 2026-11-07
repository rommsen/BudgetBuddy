// scripts/layout-invariants.mjs
//
// Layout-Invarianten als ein Sweep (infrastructure-nx14s). Geprüft wird, was für jedes Design gelten muss,
// nicht wie etwas aussieht. Die Zustände stehen in `layout-states.mjs`, der Test in
// `layout-invariants.test.mjs`. Feste Werte sind nur die Schwellen aus dem Styleguide
// (`standards/frontend/styleguide.md`): 48 px Trefferfläche am Handy (`TouchTargets.minSize`), 16 px Schrift
// in Eingabefeldern unterhalb `md` (iOS zoomt sonst beim Fokus, design-system-btwhg), `md` ab 768 px
// (`Breakpoints` in `src/Client/DesignSystem/Tokens.fs`).
//
// Die Invarianten (Nummern wie in der Task):
//   1 kein seitliches Scrollen von Seite und Sheet
//   2 Trefferflächen am Handy mindestens 48 × 48 px
//   3 Eingabefelder am Handy mindestens 16 px Schrift
//   4 kein sichtbarer Text abgeschnitten (Ellipse mit `title` ist erlaubt)
//   5 nichts verdeckt: ein offenes Sheet, Popover oder Dialog liegt ganz im Bild und in jedem Behälter, der
//     abschneidet; sein Primärknopf und die Knöpfe der Toasts sind das oberste Element
//   6 Sticky-Köpfe überlappen sich nach dem Scrollen nicht
//   7 der Primärknopf eines Sheets ist ohne Scrollen erreichbar, auch bei offener Tastatur (kleinerer Viewport)
//   8 Spaltenköpfe einer Tabelle überlappen nicht und tragen ihren Text ganz
//   9 kein Layout-Sprung beim Nachladen
//
// Messregeln gegen Flackern: gemessen wird erst, wenn alle endlichen Animationen fertig sind (unendliche wie
// Spinner zählen nicht), gescrollt wird bis zum Stillstand, verglichen wird mit einer Toleranz von einem Pixel.
// Eine Verletzung nennt Zustand, Breite, Invariante, Selektor und Messwert.

/// Styleguide-Schwellen.
export const TOUCH_MIN = 48;
export const INPUT_FONT_MIN = 16;
/// `md` aus `Breakpoints`: darunter gilt das Handy-Layout.
export const MOBILE_BELOW = 768;
/// Breiten für Seiten (Invariante 1).
export const PAGE_WIDTHS = [320, 390, 768, 1024, 1440, 1920];
export const HANDY_WIDTHS = [320, 390];
/// Breiten für die Spaltenköpfe des Kassenbuchs (Invariante 8).
export const TABLE_WIDTHS = [800, 1024, 1280, 1440];

/// Höhe des Viewports je Breite: Handy hochkant, sonst ein übliches Querformat.
export const heightFor = (width) => (width < MOBILE_BELOW ? 844 : width < 1024 ? 1024 : 900);

/// Wie eine offene Bildschirmtastatur: der sichtbare Bereich halbiert sich. `Viewport.fs` schreibt daraus
/// `--vvh`, die Sheets schrumpfen mit.
export const keyboardHeight = (height) => Math.round(height / 2);

/// Wartet, bis alle endlichen Animationen fertig sind und die Lage steht. Unendliche Animationen (Spinner,
/// Puls) haben kein Ende und werden übergangen (Vorbild `scrollIntoStickyState` in ledger-filter-bar.test.mjs).
export async function settle(page) {
  await page.evaluate(async () => {
    const finite = () =>
      document.getAnimations().filter((a) => {
        if (a.playState === "finished" || a.playState === "idle") return false;
        const end = a.effect?.getComputedTiming?.().endTime;
        return Number.isFinite(end);
      });
    for (let round = 0; round < 20; round++) {
      const running = finite();
      if (running.length === 0) break;
      await Promise.all(running.map((a) => a.finished.catch(() => {})));
    }
    // Dann steht die Lage: die Ebenen und die Seitenhöhe ändern sich fünf Frames lang nicht mehr (ein Popover wächst
    // etwa noch, wenn eine späte Antwort seine Liste füllt).
    const layout = () =>
      JSON.stringify([
        document.documentElement.scrollHeight,
        ...[...document.querySelectorAll(".bottom-sheet.active, [data-layer]")].map((el) => {
          const r = el.getBoundingClientRect();
          return [Math.round(r.top), Math.round(r.bottom), Math.round(r.left), Math.round(r.right)];
        }),
      ]);
    let last = null;
    let still = 0;
    for (let frame = 0; frame < 300 && still < 5; frame++) {
      await new Promise((done) => requestAnimationFrame(done));
      const now = layout();
      still = now === last ? still + 1 : 0;
      last = now;
    }
  });
}

/// Scrollt das Fenster ans Ende und wartet, bis die Lage fünf Frames lang steht.
export async function scrollToEndAndRest(page) {
  await page.evaluate(async () => {
    window.scrollTo({ top: document.documentElement.scrollHeight, behavior: "instant" });
    let last = -1;
    let still = 0;
    for (let frame = 0; frame < 300 && still < 5; frame++) {
      await new Promise((done) => requestAnimationFrame(done));
      const y = window.scrollY;
      still = y === last ? still + 1 : 0;
      last = y;
    }
  });
  await settle(page);
}

/// Markiert die Elemente eines Locators, damit die Messung im Browser sie findet.
export async function mark(locator, attribute) {
  const count = await locator.count();
  for (let i = 0; i < count; i++) await locator.nth(i).evaluate((el, name) => el.setAttribute(name, ""), attribute);
  return count;
}

/// Die Messung im Browser für die Invarianten 1 bis 5, 7 (ohne Tastatur) und 8. Liefert Verletzungen als
/// `{ inv, selector, value }`.
function measureInPage({ mobile, touchMin, inputFontMin, checks, reachSelector }) {
  const out = [];
  const vw = document.documentElement.clientWidth;
  const vh = window.innerHeight;
  const tol = 1;
  const want = (n) => checks.includes(n);

  const describe = (el) => {
    const parts = [];
    const own = el.getAttribute("data-testid");
    const holder = own ? null : el.closest("[data-testid]");
    if (holder) parts.push(`[data-testid="${holder.getAttribute("data-testid")}"]`);
    let tag = el.tagName.toLowerCase();
    if (own) tag += `[data-testid="${own}"]`;
    const role = el.getAttribute("role");
    if (role) tag += `[role="${role}"]`;
    parts.push(tag);
    const name = (el.getAttribute("aria-label") || el.textContent || el.getAttribute("placeholder") || "").replace(/\s+/g, " ").trim();
    if (name) parts.push(`"${name.length > 40 ? name.slice(0, 40) + "…" : name}"`);
    return parts.join(" ");
  };
  const report = (inv, el, value) => out.push({ inv, selector: typeof el === "string" ? el : describe(el), value });
  const round = (n) => Math.round(n * 10) / 10;

  const inertOrHidden = (el) => !!el.closest('[inert], [aria-hidden="true"]');
  const shown = (el) => {
    if (inertOrHidden(el)) return false;
    if (!el.checkVisibility({ opacityProperty: true, visibilityProperty: true })) return false;
    const r = el.getBoundingClientRect();
    return r.width > 0 && r.height > 0;
  };
  // Ein Element in einer festen Ebene, die ganz außerhalb des Bildes liegt (ein zugefahrenes Sheet), ist
  // nicht erreichbar und zählt nicht.
  const parked = (el) => {
    for (let node = el; node && node !== document.body; node = node.parentElement) {
      if (getComputedStyle(node).position === "fixed") {
        const r = node.getBoundingClientRect();
        return r.bottom <= 0 || r.top >= vh || r.right <= 0 || r.left >= vw;
      }
    }
    return false;
  };
  const clips = (style) => style.overflowX !== "visible" || style.overflowY !== "visible";
  const scrolls = (style) => ["auto", "scroll"].includes(style.overflowX) || ["auto", "scroll"].includes(style.overflowY);
  // Der Bereich, in dem ein Behälter seinen Inhalt zeigt (ohne Rahmen).
  const clientRect = (el) => {
    const r = el.getBoundingClientRect();
    const s = getComputedStyle(el);
    const left = r.left + parseFloat(s.borderLeftWidth);
    const top = r.top + parseFloat(s.borderTopWidth);
    return { left, top, right: left + el.clientWidth, bottom: top + el.clientHeight };
  };
  const inside = (a, b) => a.left >= b.left - tol && a.right <= b.right + tol && a.top >= b.top - tol && a.bottom <= b.bottom + tol;
  const fmt = (r) => `${round(r.left)},${round(r.top)} bis ${round(r.right)},${round(r.bottom)}`;
  const topmost = (el) => {
    const r = el.getBoundingClientRect();
    const x = Math.min(Math.max(r.left + r.width / 2, 0), vw - 1);
    const y = Math.min(Math.max(r.top + r.height / 2, 0), vh - 1);
    const hit = document.elementFromPoint(x, y);
    return { ok: !!hit && (hit === el || el.contains(hit)), hit };
  };

  const overlays = [...document.querySelectorAll('.bottom-sheet.active, [data-layer]')].filter(shown);

  // 1 Kein seitliches Scrollen: Seite, Sheet und sein Körper.
  if (want(1)) {
    const root = document.documentElement;
    if (root.scrollWidth > root.clientWidth + tol) report(1, "html", `scrollWidth ${root.scrollWidth} > clientWidth ${root.clientWidth}`);
    if (document.body.scrollWidth > document.body.clientWidth + tol)
      report(1, "body", `scrollWidth ${document.body.scrollWidth} > clientWidth ${document.body.clientWidth}`);
    for (const layer of overlays) {
      for (const el of [layer, ...layer.querySelectorAll(":scope > .sheet-body, :scope > .sheet-pinned")]) {
        if (el.scrollWidth > el.clientWidth + tol) report(1, el, `scrollWidth ${el.scrollWidth} > clientWidth ${el.clientWidth}`);
      }
    }
  }

  // 2 Trefferflächen am Handy.
  if (want(2) && mobile) {
    for (const el of document.querySelectorAll('button, a[href], [role="tab"], [role="option"]')) {
      if (!shown(el) || parked(el)) continue;
      const r = el.getBoundingClientRect();
      if (r.width < touchMin - 0.5 || r.height < touchMin - 0.5) report(2, el, `${round(r.width)} × ${round(r.height)} px`);
    }
  }

  // 3 Schrift in Eingabefeldern am Handy.
  if (want(3) && mobile) {
    const fields = 'input:not([type="checkbox"]):not([type="radio"]):not([type="hidden"]):not([type="range"]):not([type="file"]), textarea, select';
    for (const el of document.querySelectorAll(fields)) {
      if (!shown(el) || parked(el)) continue;
      const size = parseFloat(getComputedStyle(el).fontSize);
      if (size < inputFontMin) report(3, el, `font-size ${size}px`);
    }
  }

  // 4 Kein sichtbarer Text abgeschnitten.
  if (want(4)) {
    const textRect = (el) => {
      let box = null;
      for (const node of el.childNodes) {
        if (node.nodeType !== Node.TEXT_NODE || !node.textContent.trim()) continue;
        const range = document.createRange();
        range.selectNodeContents(node);
        for (const r of range.getClientRects()) {
          if (r.width === 0 || r.height === 0) continue;
          box = box
            ? { left: Math.min(box.left, r.left), top: Math.min(box.top, r.top), right: Math.max(box.right, r.right), bottom: Math.max(box.bottom, r.bottom) }
            : { left: r.left, top: r.top, right: r.right, bottom: r.bottom };
        }
      }
      return box;
    };
    const ellipsisWithTitle = (el, until) => {
      let ellipsis = false;
      let title = false;
      for (let node = el; node; node = node.parentElement) {
        const s = getComputedStyle(node);
        if (s.textOverflow === "ellipsis" || s.webkitLineClamp !== "none") ellipsis = true;
        if (node.hasAttribute("title") && node.getAttribute("title").trim()) title = true;
        if (node === until) break;
      }
      return ellipsis && title;
    };
    for (const el of document.body.querySelectorAll("*")) {
      if (["SCRIPT", "STYLE", "OPTION", "TEXTAREA", "SVG"].includes(el.tagName)) continue;
      if (!shown(el) || parked(el)) continue;
      const text = textRect(el);
      if (!text) continue;
      // Am Bildrand: Text, der seitlich aus dem Bild läuft, ist weg, solange kein Behälter dazwischen scrollt.
      let clipped = null;
      for (let node = el; node && node !== document.documentElement; node = node.parentElement) {
        const s = getComputedStyle(node);
        if (!clips(s)) continue;
        if (scrolls(s)) {
          // Ein Scrollbereich zeigt den Rest beim Scrollen, er schneidet nichts ab.
          clipped = "scroll";
          break;
        }
        const r = node.getBoundingClientRect();
        // Nur für Screenreader (sr-only): ein Pixel groß, der Text ist absichtlich nicht zu sehen.
        if (r.width <= 1 || r.height <= 1) {
          clipped = "sr-only";
          break;
        }
        const area = clientRect(node);
        if (!inside(text, area)) {
          if (!ellipsisWithTitle(el, node)) report(4, el, `Text ${fmt(text)} ragt aus ${describe(node)} (${fmt(area)})`);
          clipped = "cut";
          break;
        }
      }
      if (clipped === null && getComputedStyle(el).position !== "fixed") {
        const page = { left: 0, top: -Infinity, right: vw, bottom: Infinity };
        if (!inside(text, page)) report(4, el, `Text ${fmt(text)} ragt seitlich aus dem Bild (Breite ${vw})`);
      }
    }
    // Platzhalter eines leeren Felds passt ganz hinein (budgeting-g2ty9).
    const canvas = document.createElement("canvas").getContext("2d");
    for (const el of document.querySelectorAll("input[placeholder]")) {
      if (!shown(el) || parked(el) || el.value) continue;
      const s = getComputedStyle(el);
      canvas.font = s.font;
      const needed = canvas.measureText(el.placeholder).width;
      const room = el.clientWidth - parseFloat(s.paddingLeft) - parseFloat(s.paddingRight);
      if (needed > room + tol) report(4, el, `Platzhalter ${round(needed)} px in ${round(room)} px`);
    }
  }

  // 5 Nichts verdeckt: Ebenen ganz im Bild und in jedem abschneidenden Behälter, oben liegen sie selbst.
  if (want(5)) {
    const view = { left: 0, top: 0, right: vw, bottom: vh };
    for (const layer of overlays) {
      const r = layer.getBoundingClientRect();
      if (!inside(r, view)) report(5, layer, `${fmt(r)} liegt nicht ganz im Bild (${vw} × ${vh})`);
      for (let node = layer.parentElement; node && node !== document.body; node = node.parentElement) {
        if (!clips(getComputedStyle(node))) continue;
        const area = clientRect(node);
        if (!inside(r, area)) report(5, layer, `${fmt(r)} wird von ${describe(node)} (${fmt(area)}) abgeschnitten`);
      }
      // Über einer Ebene darf nur eine andere Ebene liegen (das zweite Sheet über dem Detail) oder ein Toast.
      const hit = topmost(layer);
      const above = hit.hit && (hit.hit.closest(".toast-layer") || overlays.some((o) => o !== layer && o.contains(hit.hit)));
      if (!hit.ok && !above) report(5, layer, `verdeckt von ${hit.hit ? describe(hit.hit) : "nichts"}`);
    }
    for (const el of document.querySelectorAll("[data-sweep-primary], .toast-layer button")) {
      if (!shown(el) || parked(el)) continue;
      const hit = topmost(el);
      if (!hit.ok) report(5, el, `verdeckt von ${hit.hit ? describe(hit.hit) : "nichts"}`);
    }
  }

  // 7 Primärknopf ohne Scrollen erreichbar: im Bild und in jedem Behälter, der abschneidet oder scrollt.
  if (want(7)) {
    const view = { left: 0, top: 0, right: vw, bottom: vh };
    for (const el of document.querySelectorAll(reachSelector)) {
      if (!shown(el)) {
        report(7, el, "nicht sichtbar");
        continue;
      }
      const r = el.getBoundingClientRect();
      if (!inside(r, view)) {
        report(7, el, `${fmt(r)} liegt nicht im Bild (${vw} × ${vh})`);
        continue;
      }
      for (let node = el.parentElement; node && node !== document.body; node = node.parentElement) {
        if (!clips(getComputedStyle(node))) continue;
        const area = clientRect(node);
        if (!inside(r, area)) {
          report(7, el, `${fmt(r)} nur nach Scrollen in ${describe(node)} (${fmt(area)})`);
          break;
        }
      }
    }
  }

  // 8 Spaltenköpfe: jeder trägt seinen Text ganz, benachbarte Texte überlappen nicht.
  if (want(8)) {
    for (const row of document.querySelectorAll("thead tr")) {
      const cells = [...row.children].filter(shown);
      let previous = null;
      for (const th of cells) {
        if (th.scrollWidth > th.clientWidth + tol) report(8, th, `scrollWidth ${th.scrollWidth} > clientWidth ${th.clientWidth}`);
        const range = document.createRange();
        range.selectNodeContents(th);
        const text = range.getBoundingClientRect();
        if (text.width > 0) {
          if (previous && text.left < previous.right - tol) report(8, th, `Text beginnt bei ${round(text.left)}, der Kopf davor endet bei ${round(previous.right)}`);
          previous = text;
        }
      }
    }
  }

  return out;
}

/// Invarianten 1 bis 5, 7 und 8 im jetzigen Zustand.
export async function measure(page, { width, checks = [1, 2, 3, 4, 5, 7, 8], reachSelector = "[data-sweep-primary], [data-sweep-reach]" }) {
  return page.evaluate(measureInPage, { mobile: width < MOBILE_BELOW, touchMin: TOUCH_MIN, inputFontMin: INPUT_FONT_MIN, checks, reachSelector });
}

/// Steht ein Sheet, Popover oder Dialog offen?
export const hasOpenLayer = (page) =>
  page.evaluate(() => [...document.querySelectorAll(".bottom-sheet.active, [data-layer]")].some((el) => el.checkVisibility()));

/// Invariante 6: nach dem Scrollen überlappt kein Sticky- oder fester Kopf einen anderen.
export async function measureSticky(page) {
  await scrollToEndAndRest(page);
  return page.evaluate(() => {
    const out = [];
    const tol = 1;
    const vw = document.documentElement.clientWidth;
    const vh = window.innerHeight;
    const describe = (el) => {
      const own = el.getAttribute("data-testid");
      const holder = own ? el : el.closest("[data-testid]");
      const tag = el.tagName.toLowerCase();
      return holder ? `[data-testid="${holder.getAttribute("data-testid")}"]${holder === el ? "" : " " + tag}` : tag;
    };
    const heads = [];
    for (const el of document.body.querySelectorAll("*")) {
      const s = getComputedStyle(el);
      if (s.position !== "sticky" && s.position !== "fixed") continue;
      if (el.closest('.bottom-sheet, .overlay, .toast-layer, [data-layer], [inert], [aria-hidden="true"]')) continue;
      if (!el.checkVisibility({ opacityProperty: true, visibilityProperty: true })) continue;
      const r = el.getBoundingClientRect();
      if (r.width < 1 || r.height < 1) continue;
      if (r.bottom <= 0 || r.top >= vh || r.right <= 0 || r.left >= vw) continue;
      // Ein Rahmen über das ganze Bild ist kein Kopf.
      if (r.width >= vw - tol && r.height >= vh - tol) continue;
      heads.push({ el, r });
    }
    // Ein Kopf in einem anderen Kopf ist dessen Teil.
    const outer = heads.filter((h) => !heads.some((o) => o !== h && o.el.contains(h.el)));
    for (let i = 0; i < outer.length; i++) {
      for (let j = i + 1; j < outer.length; j++) {
        const a = outer[i].r;
        const b = outer[j].r;
        const w = Math.min(a.right, b.right) - Math.max(a.left, b.left);
        const h = Math.min(a.bottom, b.bottom) - Math.max(a.top, b.top);
        if (w > tol && h > tol)
          out.push({ inv: 6, selector: `${describe(outer[i].el)} und ${describe(outer[j].el)}`, value: `überlappen ${Math.round(w)} × ${Math.round(h)} px` });
      }
    }
    return out;
  });
}

/// Invariante 9: die Lage der sichtbaren Bedienelemente und Überschriften vor einer verzögerten Antwort.
export async function snapshotPositions(page) {
  await page.evaluate(() => {
    let n = 0;
    for (const el of document.querySelectorAll("button, a[href], input, select, textarea, h1, h2, h3, [role=tab]")) {
      if (!el.checkVisibility({ opacityProperty: true, visibilityProperty: true })) continue;
      const r = el.getBoundingClientRect();
      if (r.width === 0 || r.height === 0) continue;
      el.setAttribute("data-sweep-y", String(r.top + window.scrollY));
      el.setAttribute("data-sweep-x", String(r.left + window.scrollX));
      n++;
    }
    return n;
  });
}

/// Invariante 9: was sich nach der Antwort verschoben hat.
export async function measureJump(page) {
  return page.evaluate(() => {
    const out = [];
    for (const el of document.querySelectorAll("[data-sweep-y]")) {
      if (!el.isConnected || !el.checkVisibility({ opacityProperty: true, visibilityProperty: true })) continue;
      const r = el.getBoundingClientRect();
      const dy = r.top + window.scrollY - Number(el.getAttribute("data-sweep-y"));
      const dx = r.left + window.scrollX - Number(el.getAttribute("data-sweep-x"));
      if (Math.abs(dy) > 1 || Math.abs(dx) > 1) {
        const holder = el.closest("[data-testid]");
        const name = (el.getAttribute("aria-label") || el.textContent || "").replace(/\s+/g, " ").trim().slice(0, 40);
        out.push({
          inv: 9,
          selector: `${holder ? `[data-testid="${holder.getAttribute("data-testid")}"] ` : ""}${el.tagName.toLowerCase()} "${name}"`,
          value: `verschoben um ${Math.round(dx)} / ${Math.round(dy)} px`,
        });
      }
    }
    return out;
  });
}

/// Ein Zustand bei einer Breite: neue Seite, Mocks, Zustand herstellen, alle Invarianten messen. Liefert die
/// Verletzungen mit Zustand und Breite. `inject` (nur für Gegenbeweise) setzt zusätzliches CSS, bevor
/// gemessen wird.
export async function runState(browser, baseUrl, state, width, { inject = null, checks = null } = {}) {
  const height = state.height ?? heightFor(width);
  const mobile = width < MOBILE_BELOW;
  const context = await browser.newContext({ viewport: { width, height }, hasTouch: mobile, isMobile: mobile });
  const page = await context.newPage();
  page.setDefaultTimeout(20000);
  const found = [];
  const unmocked = new Set();
  const want = (n) => (checks ?? state.checks ?? [1, 2, 3, 4, 5, 6, 7, 8, 9]).includes(n);
  try {
    // Was nicht gemockt ist, antwortet mit 500 und wird gemeldet: ein Fehler-Toast verfälschte die Messung.
    await page.route("**/api/**", (route) => {
      unmocked.add(new URL(route.request().url()).pathname);
      return route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"im Sweep nicht gemockt"}' });
    });
    await state.mock(page);
    let release = null;
    if (state.deferred && want(9)) {
      const gate = Promise.withResolvers();
      release = gate.resolve;
      await page.route(state.deferred.api, async (route) => {
        await gate.promise;
        await route.fulfill({ contentType: "application/json", body: JSON.stringify(state.deferred.body) });
      });
    }
    await page.goto(`${baseUrl}/#${state.route}`, { waitUntil: "load" });
    await state.open(page, { width, mobile });
    if (inject) await page.addStyleTag({ content: inject });
    await settle(page);

    if (release) {
      await snapshotPositions(page);
      release();
      await state.deferred.loaded(page).waitFor();
      await settle(page);
      found.push(...(await measureJump(page)));
    }

    // Ein Zustand, dessen Primärknopf fehlt, prüfte die Invarianten 5 und 7 sonst stillschweigend nicht.
    // `primary` und `reach` dürfen je Breite `null` liefern (am PC ein Popover mit Fuß, am Handy ein Sheet ohne).
    const primary = state.primary?.(page, { width, mobile });
    if (primary && (await mark(primary, "data-sweep-primary")) === 0) throw new Error("Primärknopf nicht gefunden");
    const reach = state.reach?.(page, { width, mobile });
    if (reach && (await mark(reach, "data-sweep-reach")) === 0) throw new Error("reach nicht gefunden");
    found.push(...(await measure(page, { width, checks: [1, 2, 3, 4, 5, 7, 8].filter(want) })));

    // Mit offener Tastatur: das Feld hat den Fokus, der Viewport halbiert sich, was `keyboard.reach` nennt,
    // bleibt ohne Scrollen sichtbar.
    if (want(7) && state.keyboard && mobile) {
      const field = state.keyboard.focus(page);
      await field.focus();
      if ((await mark(state.keyboard.reach(page), "data-sweep-keyboard")) === 0) throw new Error("keyboard.reach nicht gefunden");
      await page.setViewportSize({ width, height: keyboardHeight(height) });
      // Wie das Handy beim Öffnen der Tastatur: das fokussierte Feld rückt in seinem Scrollbereich ins Bild.
      await settle(page);
      await field.evaluate((el) => el.scrollIntoView({ block: "nearest" }));
      await settle(page);
      for (const v of await measure(page, { width, checks: [7], reachSelector: "[data-sweep-keyboard]" })) found.push({ ...v, value: `bei offener Tastatur: ${v.value}` });
      await page.setViewportSize({ width, height });
      await settle(page);
    }

    // Sticky-Köpfe der Seite zählen nur ohne offene Ebene: mit Sheet ist die Seite dahinter gesperrt.
    if (want(6) && !(await hasOpenLayer(page))) found.push(...(await measureSticky(page)));
  } finally {
    await context.close();
  }
  if (unmocked.size > 0) throw new Error(`nicht gemockt: ${[...unmocked].sort().join(", ")}`);
  // Eine benannte Ausnahme eines Zustands gilt nur für ihre Breite, ihre Invariante und ihren Selektor.
  const excepted = (v) =>
    (state.exceptions ?? []).some((e) => e.width === width && e.inv === v.inv && v.selector.startsWith(e.selector));
  return found.filter((v) => !excepted(v)).map((v) => ({ state: state.name, width, ...v }));
}

/// Alle Zustände bei allen ihren Breiten, mit mehreren Seiten zugleich.
export async function sweep(browser, baseUrl, states, { concurrency = 4 } = {}) {
  const jobs = states.flatMap((state) => state.widths.map((width) => ({ state, width })));
  const violations = [];
  const errors = [];
  let next = 0;
  const worker = async () => {
    while (next < jobs.length) {
      const { state, width } = jobs[next++];
      try {
        violations.push(...(await runState(browser, baseUrl, state, width)));
      } catch (error) {
        errors.push(`${state.name} @ ${width}: ${error.message.split("\n")[0]}`);
      }
    }
  };
  await Promise.all(Array.from({ length: concurrency }, worker));
  return { violations, errors, runs: jobs.length };
}

/// Eine Zeile je Verletzung: Zustand, Breite, Invariante, Selektor, Messwert.
export const formatViolation = (v) => `${v.state} @ ${v.width} px, Invariante ${v.inv}: ${v.selector}: ${v.value}`;
