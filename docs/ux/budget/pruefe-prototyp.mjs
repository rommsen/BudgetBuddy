// Prüft die Logik des Gesamt-Prototyps ohne Browser: Skriptblock laden, Abläufe durchspielen.
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const html = readFileSync(process.argv[2], 'utf8');
const src = html.match(/data-dc-script[^>]*>([\s\S]*?)<\/script>/)[1];
const ctx = {};
vm.createContext(ctx);
vm.runInContext(`
class DCLogic {
  constructor() { this.props = {}; }
  setState(p, cb) { this.state = { ...this.state, ...p }; if (cb) cb(); }
  forceUpdate() {}
}
${src}
globalThis.Component = Component; globalThis.calc = calc;
`, ctx);

const fails = [];
const eq = (name, got, want) => { if (got !== want) fails.push(`${name}: ${got} statt ${want}`); else console.log('ok  ', name); };
const c = ctx.calc;
eq('+20', c('+20', 900), 920);
eq('-20', c('-20', 900), 880);
eq('*2', c('*2', 45), 90);
eq('900+40', c('900+40', 1), 940);
eq('Punkt vor Strich', c('2+3*4', 0), 14);
eq('deutscher Betrag', c('1.240,50', 0), 1240.5);
eq('Division durch 0', Number.isNaN(c('/0', 5)), true);
eq('Unsinn', Number.isNaN(c('abc', 5)), true);

const app = () => new ctx.Component();
const v = (a) => a.renderVals();
const row = (a, name) => v(a).groups.flatMap((g) => g.rows).find((r) => r.n === name);

// Monatserster: Reihenfolge, Vorschau, Abwählen
let a = app();
eq('Leiste: erster Schritt', v(a).chips[0].lab, 'Targets prüfen');
eq('Leiste: zweiter Schritt', v(a).chips[1].lab, 'Targets anwenden');
eq('Targets Summe', v(a).chips[1].val, '1.560,00');
v(a).chips[1].pick();
eq('Vorschau offen', v(a).preview, true);
v(a).pRows[0].toggle();
eq('Abwählen senkt Summe', v(a).pSum, '660,00');
v(a).applyTargets();
eq('Zu verteilen nach Anwenden', a.state.tb, 990);
eq('Lebensmittel bleibt offen', row(a, 'Lebensmittel').miss, 'fehlt 900,00');

// Prüfen vor Anwenden: Übernehmen erhöht den Betrag
a = app();
v(a).chips[0].pick();
row(a, 'Gebäudeversicherung') || null;
row(a, 'Lebensmittel').pick();
eq('Prüfen öffnet Tab Target', v(a).isTarget, true);
v(a).applyCheck();
eq('Target übernommen', a.state.items[0].ta, 940);
eq('Summe steigt', v(a).chips[1].val, '1.600,00');

// Monatsmitte: Rechnen, Vorschau, Speichern beim Verlassen
a = ((x) => { v(x).toMitte(); return x; })(app());
eq('Leiste aus am 17.', v(a).showBar, false);
eq('Prüfung auch am 17.', a.state.items[0].check, 940);
row(a, 'Drogerie').pick();
eq('fehlt am 17.', v(a).assignLabel, '12,30 zuweisen');
v(a).typeDraft({ target: { value: '+12,30' } });
eq('Vorschau bei Rechnung', v(a).after, 'Verfügbar danach 0,00');
v(a).commit();
eq('Drogerie ausgeglichen', a.state.items[2].a, 104.6);
eq('Zu verteilen sinkt', a.state.tb, 60.2);

// Handy: Schrittknöpfe speichern nicht sofort, erst beim Verlassen
a = app();
v(a).toMitte();
v(a).toHandy();
row(a, 'Urlaub').pick();
const logBefore = a.state.log.length;
v(a).steps[4].pick();
v(a).steps[4].pick();
eq('Schritte noch nicht gespeichert', a.state.log.length, logBefore);
eq('Vorschau bei Schritten', v(a).showHint, true);
v(a).nextCat();
eq('Weiter speichert', a.state.items[7].a, 240);
eq('Ein Eintrag für zwei Schritte', a.state.log.length, logBefore + 1);

// Holen und Abgeben
a = app();
v(a).toMitte();
row(a, 'Drogerie').pick();
eq('Rot startet auf Holen', v(a).isIn, true);
eq('Betrag vorbelegt', a.state.amount, '12,30');
v(a).doMove();
eq('Holen gleicht aus', row(a, 'Drogerie').v, '0,00');
eq('Quelle sinkt', row(a, 'Kleidung').v, '130,70');
row(a, 'Urlaub').pick();
v(a).typeAmount({ target: { value: '100' } });
v(a).pickTarget({ target: { value: '-1' } });
v(a).doMove();
eq('Abgeben an Zu verteilen', a.state.tb, 172.5);

// Pausieren
a = app();
row(a, 'Strom').pick();
const vorher = v(a).chips[1].val;
v(a).togglePause();
eq('Pause nimmt Target aus der Summe', v(a).chips[1].val, '1.465,00');
eq('Pause zeigt Status', row(a, 'Strom').t, '95,00 / Monat · pausiert');
eq('Vorher war Summe', vorher, '1.560,00');
v(a).togglePause();
eq('Wieder aktiv', v(a).chips[1].val, '1.560,00');

// Target mit Datum, Historie
a = app();
row(a, 'Kleidung').pick();
v(a).pickType({ target: { value: 'datum' } });
v(a).typeT({ target: { value: '743,00' } });
v(a).typeDate({ target: { value: 'kaputt' } });
v(a).saveT();
eq('Ungültiges Datum speichert nicht', a.state.items[3].tt, '');
v(a).typeDate({ target: { value: '01.05.2027' } });
v(a).saveT();
eq('Datumstarget Bedarf', a.state.items[3].need, 100);
eq('Filter Targets zeigt Änderung', (() => { v(a).hFilters[3].pick(); return v(a).hRows.length; })(), 1);

// Leiste verschwindet, wenn alles erledigt ist
a = app();
for (const n of ['Lebensmittel', 'Gebäudeversicherung']) { row(a, n).pick(); v(a).dropCheck(); }
v(a).chips[1].pick();
v(a).applyTargets();
eq('Rot löst sich durch Targets', a.state.items.filter((i) => i.carry + i.a + i.c < 0).length, 0);
eq('Leiste fertig', v(a).showBar, false);
eq('Leere Ansicht gemeldet', (() => { v(a).filters[2].pick(); return v(a).noRows; })(), true);

// Freie Monate
a = app();
eq('Start im aktuellen Monat', v(a).monthLabel, 'November 2026');
for (let n = 0; n < 11; n += 1) v(a).prevMonth();
eq('Rückwärts über den Jahreswechsel', v(a).monthLabel, 'Dezember 2025');
eq('Leiste aus im alten Monat', v(a).showBar, false);
v(a).toToday();
eq('Zurück zu heute', v(a).monthLabel, 'November 2026');
eq('Leiste wieder da', v(a).showBar, true);
for (let n = 0; n < 3; n += 1) v(a).nextMonth();
eq('Vorwärts ins nächste Jahr', v(a).monthLabel, 'Februar 2027');

console.log(fails.length ? `\nFEHLER:\n${fails.join('\n')}` : '\nALLES OK');
process.exit(fails.length ? 1 : 0);
