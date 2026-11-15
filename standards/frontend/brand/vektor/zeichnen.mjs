import fs from 'node:fs';
const C = { bg:'#08081a', body:'#1e5d6b', cyan:'#28d6df', gold:'#cdb27a', goldDark:'#9c8550', visor:'#0b1424' };

// Kopf, eigenes Koordinatensystem 64 x 64
const head = (detail = true) => `
  <rect x="3" y="23" width="9" height="18" rx="4" fill="${C.gold}"/>
  <rect x="52" y="23" width="9" height="18" rx="4" fill="${C.gold}"/>
  <rect x="9" y="9" width="46" height="45" rx="19" fill="${C.body}" stroke="${C.cyan}" stroke-width="${detail ? 2.5 : 3.5}"/>
  <rect x="15" y="18" width="34" height="27" rx="12" fill="${C.visor}"/>
  <ellipse cx="25" cy="30" rx="3.6" ry="${detail ? 4.4 : 5}" fill="${C.cyan}"/>
  <ellipse cx="39" cy="30" rx="3.6" ry="${detail ? 4.4 : 5}" fill="${C.cyan}"/>
  <path d="M26.5 37.5 Q32 42 37.5 37.5" fill="none" stroke="${C.cyan}" stroke-width="${detail ? 2.6 : 3.4}" stroke-linecap="round"/>
  ${detail ? `<path d="M41 14.5 Q47 16 49.5 21" fill="none" stroke="${C.cyan}" stroke-width="1.6" stroke-linecap="round" opacity="0.55"/>` : ''}`;

const headSvg = (detail = true) =>
  `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64">${head(detail)}</svg>`;

// Ganze Figur, 512 x 512; nozzle: true = goldene Düse
const figure = (nozzle) => `
  <ellipse cx="256" cy="452" rx="70" ry="9" fill="${C.cyan}" opacity="0.12"/>
  ${nozzle ? `
  <path d="M256 394 C266 408 267 424 256 438 C245 424 246 408 256 394 Z" fill="${C.gold}" opacity="0.9"/>
  <path d="M256 404 C261 412 261 420 256 428 C251 420 251 412 256 404 Z" fill="#f3e6c4"/>
  <path d="M234 366 H278 L270 390 H242 Z" fill="${C.gold}"/>` : ''}
  <path d="M206 262 Q168 292 170 338" fill="none" stroke="${C.cyan}" stroke-width="30" stroke-linecap="round"/>
  <path d="M206 262 Q168 292 170 338" fill="none" stroke="${C.body}" stroke-width="20" stroke-linecap="round"/>
  <path d="M306 262 Q352 252 362 206" fill="none" stroke="${C.cyan}" stroke-width="30" stroke-linecap="round"/>
  <path d="M306 262 Q352 252 362 206" fill="none" stroke="${C.body}" stroke-width="20" stroke-linecap="round"/>
  <rect x="194" y="234" width="124" height="138" rx="50" fill="${C.body}" stroke="${C.cyan}" stroke-width="6"/>
  <path d="M236 264 V342 M236 264 H262 A19 19 0 0 1 262 302 H236 M236 302 H266 A20 20 0 0 1 266 342 H236"
        fill="none" stroke="${C.cyan}" stroke-width="9" stroke-linecap="round" stroke-linejoin="round"/>
  <circle cx="170" cy="344" r="15" fill="${C.body}" stroke="${C.cyan}" stroke-width="5"/>
  <g transform="translate(168 62) scale(2.75)">${head(true)}</g>
  <circle cx="362" cy="202" r="15" fill="${C.body}" stroke="${C.cyan}" stroke-width="5"/>
  <circle cx="400" cy="152" r="40" fill="${C.gold}" stroke="${C.goldDark}" stroke-width="5"/>
  <path d="M415 135 A22 22 0 1 0 415 169" fill="none" stroke="${C.bg}" stroke-width="8" stroke-linecap="round"/>
  <path d="M377 146 H404 M377 159 H404" stroke="${C.bg}" stroke-width="7" stroke-linecap="round"/>`;

const appIcon = (nozzle) =>
  `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512"><rect width="512" height="512" rx="112" fill="${C.bg}"/>${figure(nozzle)}</svg>`;
const maskable = (nozzle) =>
  `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512"><rect width="512" height="512" fill="${C.bg}"/><g transform="translate(61.44 61.44) scale(0.76)">${figure(nozzle)}</g></svg>`;

const files = {
  'kopf.svg': headSvg(true), 'kopf-klein.svg': headSvg(false),
  'figur-mit-duese.svg': appIcon(true), 'figur-ohne-duese.svg': appIcon(false),
  'maskable-mit-duese.svg': maskable(true), 'maskable-ohne-duese.svg': maskable(false),
};
for (const [n, s] of Object.entries(files)) fs.writeFileSync(n, s);
console.log(Object.keys(files).join(' '));
