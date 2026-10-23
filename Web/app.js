'use strict';
// =====================================================================
//  КИБЕР АРЕНА — веб-версия (порт с Unity). Данные и картинки встроены.
// =====================================================================
const REG = DATA.regions;
const REG_SHORT = ['LCS', 'LEC', 'КЛ', 'LPL', 'LCK'];
const RCOL = ['#FF6B6B', '#FFB84D', '#4CD97B', '#4DB8FF', '#B57BFF'];
const COL = { p1: '#FF5468', p2: '#4D8DFF', gold: '#FFC940', accent: '#2EE6D6', dim: '#8A93AD', bad: '#FF5468', good: '#4CD97B', panel2: '#252E4A', text: '#EEF1FA' };

const clamp = (v, a, b) => Math.max(a, Math.min(b, v));
const esc = s => String(s ?? '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const rnd = n => Math.floor(Math.random() * n);
function shuffle(a) { a = a.slice(); for (let i = a.length - 1; i > 0; i--) { const j = rnd(i + 1); [a[i], a[j]] = [a[j], a[i]]; } return a; }

const store = {
  get(k, d) { try { const v = localStorage.getItem(k); return v ? JSON.parse(v) : d; } catch (e) { return d; } },
  set(k, v) { try { localStorage.setItem(k, JSON.stringify(v)); } catch (e) { } }
};

const CLOUD = { db: null, on: false, state: 'local', busy: {}, dirty: {} };

// ================= РОСТЕР И НАСТРОЙКИ =================
const PL = new Map(DATA.players.map(p => [p.id, p]));
const TM = new Map(DATA.teams.map(t => [t.id, t]));
const BASE = { power: {}, bonus: {}, skill: {} };
DATA.players.forEach(p => { BASE.power[p.id] = p.power; BASE.skill[p.id] = p.skill || ''; });
DATA.teams.forEach(t => { BASE.bonus[t.id] = t.bonus; });
const OKEY = 'ca_overrides_v1';
let OV = store.get(OKEY, null) || { power: {}, bonus: {}, skill: {} };
function applyOV() {
  for (const p of DATA.players) { p.power = clamp(OV.power[p.id] ?? BASE.power[p.id], 50, 75); p.skill = OV.skill[p.id] ?? BASE.skill[p.id]; }
  for (const t of DATA.teams) t.bonus = clamp(OV.bonus[t.id] ?? BASE.bonus[t.id], 1, 10);
}
function setOV(kind, id, v) { if (v === BASE[kind][id]) delete OV[kind][id]; else OV[kind][id] = v; store.set(OKEY, OV); applyOV(); cloudSave('settings'); }
function resetOV() { OV = { power: {}, bonus: {}, skill: {} }; store.set(OKEY, OV); applyOV(); cloudSave('settings'); }
applyOV();

const inRegion = r => DATA.players.filter(p => p.region === r);
const teamsIn = r => DATA.teams.filter(t => t.region === r);
const power = (p, t) => p.power + (t ? t.bonus : 0);

// ================= БОЙ =================
const MAXHP = 100, BLOCK = 0.4, CRIT = 0.10, CRITM = 1.7, TICK = 1;
const K = p => (p - 50) / 25;
const atkC = p => 0.50 + 0.12 * K(p), retC = p => 0.14 + 0.06 * K(p), defC = p => 0.30 + 0.08 * K(p);
const baseDmg = p => 8 + p * 0.12;
const roll = pw => ({ attack: Math.random() < atkC(pw), retreat: Math.random() < retC(pw), defend: Math.random() < defC(pw) });
function resolve(ap, def) {
  const crit = Math.random() < CRIT;
  const dmg = baseDmg(ap) * (0.7 + 0.6 * Math.random()) * (crit ? CRITM : 1);
  if (def.retreat) return { res: 'dodge', dmg: 0 };
  if (def.defend) return { res: 'block', dmg: dmg * BLOCK, crit };
  return { res: 'hit', dmg, crit };
}
const fighter = (p, t) => ({ p, t, pw: power(p, t), hp: MAXHP });
function tick(f1, f2) {
  const a1 = roll(f1.pw), a2 = roll(f2.pw);
  const on2 = a1.attack ? resolve(f1.pw, a2) : null, on1 = a2.attack ? resolve(f2.pw, a1) : null;
  if (on2) f2.hp -= on2.dmg; if (on1) f1.hp -= on1.dmg;
  return { a1, a2, on1, on2 };
}
function roundWinner(f1, f2) {
  if (f1.hp > 0 && f2.hp <= 0) return 1; if (f2.hp > 0 && f1.hp <= 0) return 2;
  if (Math.abs(f1.hp - f2.hp) < 0.001) return rnd(2) + 1;
  return f1.hp > f2.hp ? 1 : 2;
}
function finishRound(f1, f2) { let g = 0; while (f1.hp > 0 && f2.hp > 0 && g++ < 10000) tick(f1, f2); return roundWinner(f1, f2); }
function simMatch(a, ta, b, tb) { let s1 = 0, s2 = 0; while (s1 < 2 && s2 < 2) { if (finishRound(fighter(a, ta), fighter(b, tb)) === 1) s1++; else s2++; } return [s1, s2]; }

// ================= СЕЗОН =================
const ST = { League: 0, PlayIn: 1, Semi: 2, Final: 3, WC: 4, WG: 5, WQ: 6, WS: 7, WF: 8 };
const RS = { NotStarted: 0, Draft: 1, League: 2, PlayIn: 3, Semi: 4, Final: 5, Done: 6 };
const PH = { Regions: 0, WC: 1, WG: 2, WQ: 3, WS: 4, WF: 5, Finished: 6 };
const SKEY = 'ca_season_v1';
let D;
const fresh = (n, ht, hp) => ({ number: n, phase: 0, regionState: [0, 0, 0, 0, 0], matches: [], draftPlayers: [], draftTeams: [], qualified: Array(15).fill(-1), wcQueue: [], wcNext: 0, wcWinner: -1, groups: [], champion: -1, championPlayer: '', historyTeams: ht || [], historyPlayers: hp || [] });
function loadSeason() {
  D = store.get(SKEY, null);
  const ok = D && Array.isArray(D.matches) && Array.isArray(D.regionState) && D.regionState.length === 5 && Array.isArray(D.draftPlayers)
    && Array.isArray(D.draftTeams) && D.draftPlayers.length === D.draftTeams.length && Array.isArray(D.qualified) && D.qualified.length === 15
    && D.matches.every(m => TM.has(m.t1) && TM.has(m.t2)) && D.draftPlayers.every(id => PL.has(id)) && D.draftTeams.every(id => TM.has(id));
  if (!ok) { D = fresh(1); saveSeason(); }
}
const saveSeason = () => { store.set(SKEY, D); cloudSave('season'); };
const S = {
  phase: () => D.phase,
  stateOf: r => D.regionState[r],
  allDone: () => D.regionState.every(s => s === RS.Done),
  letter: g => 'ABCD'[clamp(g, 0, 3)],
  newSeason() { D = fresh(D.number + 1, D.historyTeams, D.historyPlayers); saveSeason(); },
  resetAll() { D = fresh(1); saveSeason(); },
  playerOf(tid) { const i = D.draftTeams.indexOf(tid); return i >= 0 ? PL.get(D.draftPlayers[i]) : null; },
  teamIdOf(pid) { const i = D.draftPlayers.indexOf(pid); return i >= 0 ? D.draftTeams[i] : -1; },
  startRegion(r) { if (D.phase !== PH.Regions || D.regionState[r] !== RS.NotStarted) return; D.regionState[r] = RS.Draft; saveSeason(); },
  draftOrder: r => teamsIn(r),
  currentPicker(r) { return teamsIn(r).find(t => !S.playerOf(t.id)) || null; },
  available(r) { return inRegion(r).filter(p => S.teamIdOf(p.id) < 0).sort((a, b) => b.power - a.power); },
  pickNumber(r) { return teamsIn(r).filter(t => S.playerOf(t.id)).length + 1; },
  pick(r, pid) {
    if (D.regionState[r] !== RS.Draft) return false;
    const team = S.currentPicker(r), p = PL.get(pid);
    if (!team || !p || p.region !== r || S.teamIdOf(pid) >= 0) return false;
    D.draftPlayers.push(pid); D.draftTeams.push(team.id);
    if (!S.currentPicker(r)) { D.regionState[r] = RS.League; D.matches.push(...roundRobin(teamsIn(r).map(t => t.id), ST.League, r, false)); }
    saveSeason(); return true;
  },
  autoPick(r) {
    const av = S.available(r); if (!av.length) return false;
    const best = av.map(p => [p, p.power + Math.random() * 8]).sort((a, b) => b[1] - a[1])[0][0];
    return S.pick(r, best.id);
  },
  autoDraft(r) { let g = 0; while (D.regionState[r] === RS.Draft && g++ < 100 && S.autoPick(r)) { } },
  matches: (st, g) => D.matches.filter(m => m.stage === st && (g === undefined || m.group === g)),
  nextInRegion: r => D.matches.find(m => !m.played && m.group === r && m.stage <= ST.Final) || null,
  nextWC: () => D.matches.find(m => !m.played && m.stage === ST.WC) || null,
  nextWorlds: () => D.matches.find(m => !m.played && m.stage >= ST.WG) || null,
  qualified: (r, place) => D.qualified[r * 3 + place],
  groupTeams(st, g) { const ids = []; for (const m of S.matches(st, g)) { if (!ids.includes(m.t1)) ids.push(m.t1); if (!ids.includes(m.t2)) ids.push(m.t2); } return ids; },
  standings(st, g) {
    const ms = S.matches(st, g);
    const tab = new Map(S.groupTeams(st, g).map(id => [id, { team: id, played: 0, wins: 0, losses: 0, mw: 0, ml: 0 }]));
    for (const m of ms) if (m.played) {
      const a = tab.get(m.t1), b = tab.get(m.t2);
      a.played++; b.played++; a.mw += m.s1; a.ml += m.s2; b.mw += m.s2; b.ml += m.s1;
      if (m.s1 > m.s2) { a.wins++; b.losses++; } else { b.wins++; a.losses++; }
    }
    return [...tab.values()].sort((x, y) => {
      if (x.team === y.team) return 0;
      if (x.wins !== y.wins) return y.wins - x.wins;
      const dx = x.mw - x.ml, dy = y.mw - y.ml; if (dx !== dy) return dy - dx;
      if (x.mw !== y.mw) return y.mw - x.mw;
      let h = 0;
      for (const m of ms) if (m.played && ((m.t1 === x.team && m.t2 === y.team) || (m.t1 === y.team && m.t2 === x.team))) h += win(m) === x.team ? -1 : 1;
      if (h) return h;
      return x.team - y.team;
    });
  },
  regionStateName(r) {
    switch (D.regionState[r]) {
      case RS.NotStarted: return 'не начат';
      case RS.Draft: return `драфт · пик ${S.pickNumber(r)} из ${teamsIn(r).length}`;
      case RS.League: { const ms = S.matches(ST.League, r); return `лига · ${ms.filter(m => m.played).length} из ${ms.length}`; }
      case RS.PlayIn: return 'плей-ин';
      case RS.Semi: return 'полуфиналы';
      case RS.Final: return 'финал';
      default: return 'сыгран';
    }
  },
  phaseName() {
    switch (D.phase) {
      case PH.Regions: return `регионы · сыграно ${D.regionState.filter(s => s === RS.Done).length} из 5`;
      case PH.WC: return 'Wild Card';
      case PH.WG: return 'ЧМ · групповой этап';
      case PH.WQ: return 'ЧМ · четвертьфиналы';
      case PH.WS: return 'ЧМ · полуфиналы';
      case PH.WF: return 'ЧМ · финал';
      default: return 'сезон завершён';
    }
  },
  matchName(m) {
    const reg = m.group >= 0 && m.group < 5 ? REG[m.group] : '';
    switch (m.stage) {
      case ST.League: return `${reg} · лига`;
      case ST.PlayIn: return `${reg} · плей-ин`;
      case ST.Semi: return `${reg} · полуфинал`;
      case ST.Final: return m.kind === 1 ? `${reg} · матч за 3-е место` : `${reg} · финал`;
      case ST.WC: return m.kind === 1 ? 'Wild Card · финал' : 'Wild Card';
      case ST.WG: return `ЧМ · группа ${S.letter(m.group)}`;
      case ST.WQ: return 'ЧМ · четвертьфинал';
      case ST.WS: return 'ЧМ · полуфинал';
      default: return 'ЧМ · финал';
    }
  },
  report(m, s1, s2) {
    if (m.played) return;
    m.s1 = s1; m.s2 = s2; m.played = true;
    if (m.stage <= ST.Final) advanceRegion(m.group);
    else if (m.stage === ST.WC) advanceWC(m);
    else advanceWorlds();
    saveSeason();
  },
  quickSim(m) {
    const a = S.playerOf(m.t1), b = S.playerOf(m.t2);
    if (!a || !b || m.played) return;
    const [x, y] = simMatch(a, TM.get(m.t1), b, TM.get(m.t2));
    S.report(m, x, y);
  },
  simRegionStage(r) { const st = D.regionState[r]; if (st < RS.League || st === RS.Done) return; let m, g = 0; while (D.regionState[r] === st && (m = S.nextInRegion(r)) && g++ < 500) S.quickSim(m); },
  simWC() { let m, g = 0; while (D.phase === PH.WC && (m = S.nextWC()) && g++ < 500) S.quickSim(m); },
  simWorldsStage() { const ph = D.phase; let m, g = 0; while (D.phase === ph && (m = S.nextWorlds()) && g++ < 500) S.quickSim(m); },
  wcWaiting: () => Math.max(0, D.wcQueue.length - D.wcNext),
};
const win = m => m.s1 > m.s2 ? m.t1 : m.t2;
const lose = m => m.s1 > m.s2 ? m.t2 : m.t1;
const addM = (stage, group, t1, t2, kind = 0) => D.matches.push({ stage, group, t1, t2, s1: 0, s2: 0, played: false, kind });

function roundRobin(ids, stage, group, swap) {
  const res = [], list = ids.slice();
  if (list.length % 2) list.push(-1);
  const n = list.length;
  for (let round = 0; round < n - 1; round++) {
    for (let i = 0; i < n / 2; i++) {
      const a = list[i], b = list[n - 1 - i];
      if (a < 0 || b < 0) continue;
      const flip = (round % 2 === 1) !== swap;
      res.push({ stage, group, t1: flip ? b : a, t2: flip ? a : b, s1: 0, s2: 0, played: false, kind: 0 });
    }
    list.splice(1, 0, list.pop());
  }
  return res;
}

function advanceRegion(r) {
  if (S.nextInRegion(r)) return;
  switch (D.regionState[r]) {
    case RS.League: {
      const t = S.standings(ST.League, r);
      D.regionState[r] = RS.PlayIn;
      addM(ST.PlayIn, r, t[2].team, t[5].team); addM(ST.PlayIn, r, t[3].team, t[4].team);
      break;
    }
    case RS.PlayIn: {
      const seeds = S.standings(ST.League, r).map(s => s.team);
      const w = S.matches(ST.PlayIn, r).map(win).sort((a, b) => seeds.indexOf(a) - seeds.indexOf(b));
      D.regionState[r] = RS.Semi;
      addM(ST.Semi, r, seeds[0], w[1]); addM(ST.Semi, r, seeds[1], w[0]);
      break;
    }
    case RS.Semi: {
      const s = S.matches(ST.Semi, r);
      D.regionState[r] = RS.Final;
      addM(ST.Final, r, lose(s[0]), lose(s[1]), 1); addM(ST.Final, r, win(s[0]), win(s[1]), 0);
      break;
    }
    case RS.Final: {
      const fin = S.matches(ST.Final, r), f = fin.find(m => m.kind === 0), th = fin.find(m => m.kind === 1);
      D.qualified[r * 3] = win(f); D.qualified[r * 3 + 1] = lose(f); D.qualified[r * 3 + 2] = win(th);
      D.regionState[r] = RS.Done;
      if (S.allDone()) startWC();
      break;
    }
  }
}
function startWC() {
  D.phase = PH.WC;
  D.wcQueue = shuffle(DATA.teams.map(t => t.id).filter(id => !D.qualified.includes(id)));
  D.wcNext = 2;
  addM(ST.WC, 0, D.wcQueue[0], D.wcQueue[1], D.wcQueue.length === 2 ? 1 : 0);
}
function advanceWC(m) {
  if (D.wcNext < D.wcQueue.length) {
    const ch = D.wcQueue[D.wcNext++];
    addM(ST.WC, 0, win(m), ch, D.wcNext >= D.wcQueue.length ? 1 : 0);
    return;
  }
  D.wcWinner = win(m);
  drawWorlds();
}
function drawWorlds() {
  const pot = place => shuffle([0, 1, 2, 3, 4].map(r => S.qualified(r, place)));
  const f = pot(0), s = pot(1), t = pot(2);
  const p4 = shuffle([f[4], s[4], t[4], D.wcWinner]);
  D.groups = [];
  for (let g = 0; g < 4; g++) D.groups.push(f[g], s[g], t[g], p4[g]);
  D.phase = PH.WG;
  const per = [];
  for (let g = 0; g < 4; g++) {
    const ids = D.groups.slice(g * 4, g * 4 + 4);
    per.push([...roundRobin(ids, ST.WG, g, false), ...roundRobin(ids, ST.WG, g, true)]);
  }
  for (let k = 0; k < per[0].length; k += 2) for (let g = 0; g < 4; g++) D.matches.push(...per[g].slice(k, k + 2));
}
function advanceWorlds() {
  if (S.nextWorlds()) return;
  switch (D.phase) {
    case PH.WG: {
      const top = [0, 1, 2, 3].map(g => { const t = S.standings(ST.WG, g); return [t[0].team, t[1].team]; });
      D.phase = PH.WQ;
      addM(ST.WQ, 0, top[0][0], top[1][1]); addM(ST.WQ, 0, top[1][0], top[0][1]);
      addM(ST.WQ, 0, top[2][0], top[3][1]); addM(ST.WQ, 0, top[3][0], top[2][1]);
      break;
    }
    case PH.WQ: { const q = S.matches(ST.WQ, 0); D.phase = PH.WS; addM(ST.WS, 0, win(q[0]), win(q[2])); addM(ST.WS, 0, win(q[1]), win(q[3])); break; }
    case PH.WS: { const s = S.matches(ST.WS, 0); D.phase = PH.WF; addM(ST.WF, 0, win(s[0]), win(s[1])); break; }
    case PH.WF: {
      const fm = S.matches(ST.WF, 0);
      D.champion = win(fm[fm.length - 1]);
      const p = S.playerOf(D.champion);
      D.championPlayer = p ? p.nickname : '';
      D.phase = PH.Finished;
      D.historyTeams.push(D.champion); D.historyPlayers.push(D.championPlayer);
      break;
    }
  }
}
loadSeason();

// =====================================================================
//  ИНТЕРФЕЙС
// =====================================================================
const app = document.getElementById('app');
let H = {}, hid = 0, M = null;
const on = fn => { const k = 'h' + (++hid); H[k] = fn; return `data-h="${k}" tabindex="0"`; };
app.addEventListener('click', e => { const el = e.target.closest('[data-h]'); if (el && H[el.dataset.h]) H[el.dataset.h](e, el); });
app.addEventListener('keydown', e => { if ((e.key === 'Enter' || e.key === ' ') && e.target.dataset && e.target.dataset.h && e.target.tagName !== 'BUTTON' && e.target.tagName !== 'TEXTAREA') { e.preventDefault(); e.target.click(); } });

function screen(html) {
  if (M) { M.alive = false; M = null; }
  app.innerHTML = html;
  // оставляем только обработчики, которые есть на новом экране
  const live = {};
  app.querySelectorAll('[data-h]').forEach(el => { if (H[el.dataset.h]) live[el.dataset.h] = H[el.dataset.h]; });
  H = live;
  return app;
}
const topbar = (title, back, right = '') =>
  `<div class="top">${back ? `<button class="back" ${on(back)} aria-label="Назад">‹</button>` : '<span class="back ghost"></span>'}<h1>${esc(title)}</h1>${right || '<span class="back ghost"></span>'}</div>`;

const img = k => IMG[k] || '';
function logo(t, cls = '') {
  if (!t) return `<div class="logo ${cls}">?</div>`;
  const s = img(t.logo);
  return `<div class="logo ${cls}">${s ? `<img src="${s}" alt="${esc(t.name)}">` : esc(t.name[0])}</div>`;
}
const regionLogo = (r, cls = '') => img('r' + (r + 1)) ? `<div class="logo ${cls}"><img src="${img('r' + (r + 1))}" alt="${esc(REG[r])}"></div>` : '';
function flagImg(p) { const s = p && p.country ? img('f_' + p.country.toLowerCase()) : ''; return s ? `<img src="${s}" alt="${esc(p.country)}">` : ''; }
function portrait(p, t, frame, cls = '') {
  if (!p) return `<div class="pt ${cls}" style="--fr:${frame || COL.panel2}"></div>`;
  const f = flagImg(p);
  return `<div class="pt ${cls}" style="--fr:${frame || COL.panel2}"><img src="${img(p.portrait)}" alt="${esc(p.nickname)}">` +
    (f ? `<div class="flag">${f}</div>` : '') + (t ? `<div class="badge">${img(t.logo) ? `<img src="${img(t.logo)}" alt="">` : `<b>${esc(t.name[0])}</b>`}</div>` : '') + `<div class="flash"></div></div>`;
}
const teamTag = t => t ? `<span class="tt">${logo(t)}<span>${esc(t.name)}</span></span>` : `<span class="tt dim"><span>без команды</span></span>`;
const flagIcon = p => { const f = flagImg(p); return f ? `<span class="flagi">${f}</span>` : ''; };

// ================= ГЛАВНОЕ МЕНЮ =================
function showMenu() {
  const ht = D.historyTeams;
  let last = '';
  if (ht.length) {
    const t = TM.get(ht[ht.length - 1]); const who = D.historyPlayers[ht.length - 1] || '';
    if (t) last = `<div class="last">Последний чемпион мира: <b class="gold">${esc(t.name)}</b>${who ? ` (${esc(who)})` : ''}</div>`;
  }
  screen(`<div class="menu">
    <div class="brand"><h2>КИБЕР<br>АРЕНА</h2><p>симулятор киберспортивных турниров</p></div>
    <button class="btn b1" ${on(showSelect)}>СВОБОДНЫЙ БОЙ</button>
    <button class="btn b2" ${on(showSeason)}>СЕЗОН ${D.number}<small>${esc(S.phaseName())}</small></button>
    <button class="btn" ${on(() => showSettings(0))}>НАСТРОЙКИ</button>
    ${last}
    <div class="sync" id="sync">${syncText()}</div>
  </div>`);
}

// ================= СВОБОДНЫЙ БОЙ =================
const SEL = { a: -1, b: -1, ta: -1, tb: -1 };
function showSelect() {
  const prev = app.querySelector('.scroll.sel');
  const keep = prev ? prev.scrollTop : 0;
  let html = topbar('Свободный бой', showMenu) + '<div class="scroll sel">';
  for (let r = 0; r < 5; r++) {
    html += `<div class="reg-h"><span class="bar" style="background:${RCOL[r]}"></span>${regionLogo(r)}<b style="color:${RCOL[r]}">${esc(REG[r].toUpperCase())}</b></div><div class="pgrid">`;
    for (const p of inRegion(r)) {
      const a = p.id === SEL.a, b = p.id === SEL.b;
      const t = a ? TM.get(SEL.ta) : b ? TM.get(SEL.tb) : null;
      html += `<div class="pc" ${on(() => onPick(p.id))}>${portrait(p, t, a ? COL.p1 : b ? COL.p2 : COL.panel2)}${a || b ? `<span class="num">${a ? 1 : 2}</span>` : ''}<div class="nick ell">${esc(p.nickname)}</div></div>`;
    }
    html += '</div>';
  }
  const pa = PL.get(SEL.a), pb = PL.get(SEL.b), ta = TM.get(SEL.ta), tb = TM.get(SEL.tb);
  const ready = pa && pb && ta && tb;
  html += `</div><div class="bottom"><div class="slots">${slot(pa, ta, 1)}${slot(pb, tb, 2)}<span class="vs">VS</span></div>
    <button class="btn main" ${ready ? on(() => startMatch(pa, ta, pb, tb, 'Свободный бой · до 2 побед', () => showSelect(), showSelect, true)) : 'disabled'}>В БОЙ!</button></div>`;
  screen(html);
  app.querySelector('.scroll.sel').scrollTop = keep;
}
function slot(p, t, n) {
  const c = n === 1 ? COL.p1 : COL.p2;
  if (!p) return `<div class="slot empty" style="--c:${c}"><b>Игрок ${n}</b>нажми на портрет, потом выбери команду</div>`;
  return `<div class="slot tap" style="--c:${c}" ${on(() => { if (n === 1) { SEL.a = -1; SEL.ta = -1; } else { SEL.b = -1; SEL.tb = -1; } showSelect(); })}>
    ${portrait(p, t, c)}<div class="nm ell">${esc(p.nickname)}</div>${teamTag(t)}<div style="color:${RCOL[p.region]};font-size:12px">${esc(REG[p.region])}</div><div class="sk">${esc(p.skill)}</div></div>`;
}
function onPick(id) {
  if (SEL.a === id) { SEL.a = -1; SEL.ta = -1; showSelect(); return; }
  if (SEL.b === id) { SEL.b = -1; SEL.tb = -1; showSelect(); return; }
  const slotN = SEL.a < 0 ? 1 : 2;
  teamPicker(PL.get(id), slotN === 1 ? SEL.ta : SEL.tb, tid => {
    if (slotN === 1) { SEL.a = id; SEL.ta = tid; } else { SEL.b = id; SEL.tb = tid; }
    showSelect();
  });
}
function teamPicker(p, current, onPickTeam) {
  let chosen = TM.get(current) && TM.get(current).region === p.region ? current : -1;
  const m = document.createElement('div');
  m.className = 'modal';
  const draw = () => {
    const t = TM.get(chosen);
    const old = m.querySelector('.scroll'), top = old ? old.scrollTop : 0;
    m.innerHTML =`<div class="head"><h3>Команда для ${esc(p.nickname)}</h3><div style="color:${RCOL[p.region]}">${esc(REG[p.region])}</div></div>
      <div class="preview">${portrait(p, t, t ? COL.gold : COL.panel2)}<div class="inf"><b class="ell">${esc(p.nickname)}</b>${t ? teamTag(t) : '<span class="dim">выбери команду ниже</span>'}<span class="acc" style="font-size:13px">${esc(p.skill)}</span></div></div>
      <div class="scroll">${teamsIn(p.region).map(x => `<div class="trow ${x.id === chosen ? 'on' : ''}" ${on(() => { chosen = x.id; draw(); })}>${logo(x)}<b>${esc(x.name)}</b></div>`).join('')}</div>
      <div class="bottom"><div class="row2" style="grid-template-columns:2fr 3fr"><button class="btn" ${on(() => m.remove())}>Отмена</button><button class="btn main" ${t ? on(() => { m.remove(); onPickTeam(chosen); }) : 'disabled'}>ВЫБРАТЬ</button></div></div>`;
    m.querySelector('.scroll').scrollTop = top;
  };
  draw();
  app.appendChild(m);
}

// ================= БОЙ =================
let SPEED = 1;
const ACT = [['attack', 'АТАКА', 'a'], ['retreat', 'ОТХОД', 'r'], ['defend', 'ЗАЩИТА', 'd']];
function sideHtml(n, p, t) {
  const c = n === 1 ? COL.p1 : COL.p2;
  return `<div class="side s${n}"><div class="ph">${portrait(p, t, c)}</div><div class="info">
    <div class="hp"><div class="tr"><div class="fill" style="width:100%"></div></div><span>100</span></div>
    <div class="nk">${flagIcon(p)}<b class="ell" style="color:${c}">${esc(p.nickname)}</b></div>
    ${teamTag(t)}
    <div class="rg" style="color:${RCOL[p.region]}">${esc(REG[p.region])}</div>
    <div class="sk">${esc(p.skill)}</div>
    <div class="acts">${ACT.map(a => `<i>${a[1]}</i>`).join('')}</div>
    <div class="wins"><i></i><i></i></div>
  </div></div>`;
}
function startMatch(a, ta, b, tb, title, onFinished, onBack, rematch = false) {
  screen(`<div class="match"><div class="arena" style="background-image:url('${img('arena')}')"></div>
    ${topbar(title, onBack)}
    ${sideHtml(1, a, ta)}
    <div class="chat"><div class="rnd"></div><div class="log" aria-live="polite"></div><div class="banner" hidden></div></div>
    ${sideHtml(2, b, tb)}
    <div class="speed">${[1, 2, 4].map(s => `<button class="btn sp ${s === SPEED ? 'on' : ''}" data-s="${s}" ${on((e, el) => { SPEED = s; app.querySelectorAll('.sp').forEach(x => x.classList.toggle('on', +x.dataset.s === SPEED)); })}>x${s}</button>`).join('')}
      <button class="btn" ${on(() => { if (M) M.skip = true; })}>ПРОПУСТИТЬ</button></div></div>`);
  const root = app.querySelector('.match');
  const side = n => { const el = root.querySelector('.s' + n); return { n, el, ph: el.querySelector('.ph'), pt: el.querySelector('.ph .pt'), flash: el.querySelector('.flash'), fill: el.querySelector('.fill'), hpt: el.querySelector('.hp span'), acts: [...el.querySelectorAll('.acts i')], wins: [...el.querySelectorAll('.wins i')], c: n === 1 ? COL.p1 : COL.p2, p: n === 1 ? a : b }; };
  M = { alive: true, skip: false, root, A: side(1), B: side(2), rnd: root.querySelector('.rnd'), log: root.querySelector('.log'), banner: root.querySelector('.banner') };
  runMatch(M, a, ta, b, tb, title, onFinished, onBack, rematch);
}
const raf = () => new Promise(r => requestAnimationFrame(() => r()));
const sleepReal = ms => new Promise(r => setTimeout(r, ms));
async function wait(m, sec) {
  let t = 0, last = performance.now();
  while (t < sec && !m.skip && m.alive) {
    await Promise.race([raf(), sleepReal(50)]);
    const now = performance.now(); t += (now - last) / 1000 * SPEED; last = now;
  }
}
async function banner(m, html, sec) { m.banner.innerHTML = html; m.banner.hidden = false; await wait(m, sec); if (m.alive) m.banner.hidden = true; }
function log(m, html) { const d = document.createElement('div'); d.innerHTML = html; m.log.appendChild(d); while (m.log.children.length > 14) m.log.firstChild.remove(); }
function setHp(s, hp) {
  const k = clamp(hp / MAXHP, 0, 1);
  s.fill.style.width = (k * 100) + '%';
  s.fill.style.background = k > 0.5 ? COL.good : k > 0.25 ? COL.gold : COL.bad;
  s.fill.style.opacity = k > 0.01 ? 1 : 0;
  s.hpt.textContent = Math.ceil(Math.max(0, hp));
}
function showActs(s, a) { ACT.forEach((x, i) => { s.acts[i].className = a && a[x[0]] ? x[2] : ''; }); }
const nickTag = s => `<b style="color:${s.c}">${esc(s.p.nickname)}</b>`;
const anim = (el, frames, ms) => { try { el.animate(frames, { duration: ms / SPEED, easing: 'ease-out' }); } catch (e) { } };
function floatText(m, s, text, color) {
  const r = s.pt.getBoundingClientRect(), base = m.root.getBoundingClientRect();
  const f = document.createElement('div');
  f.className = 'float'; f.textContent = text; f.style.color = color;
  f.style.left = (r.left - base.left + r.width / 2 + (Math.random() * 60 - 30)) + 'px';
  f.style.top = (r.top - base.top + r.height / 2) + 'px';
  m.root.appendChild(f);
  const ms = 900 / SPEED;
  try { f.animate([{ transform: 'translate(-50%,-50%)', opacity: 1 }, { transform: 'translate(-50%,-50%) translateY(-60px)', opacity: 0 }], { duration: ms, easing: 'ease-in' }); } catch (e) { }
  setTimeout(() => f.remove(), ms);
}
function presentStrike(m, atk, def, s, dir) {
  anim(atk.ph, [{ transform: 'translate(0,0)' }, { transform: `translate(${dir * 16}px,${dir * 16}px)` }, { transform: 'translate(0,0)' }], 180);
  const dmg = Math.round(s.dmg), crit = s.crit ? ' <b class="gold">КРИТ!</b>' : '';
  if (s.res === 'dodge') { log(m, `${nickTag(atk)} бьёт — ${nickTag(def)} уклоняется`); floatText(m, def, 'УКЛОН', COL.accent); }
  else if (s.res === 'block') {
    log(m, `${nickTag(atk)} бьёт — ${nickTag(def)} блокирует, -${dmg}${crit}`); floatText(m, def, 'БЛОК -' + dmg, COL.dim);
    def.flash.style.background = 'rgba(150,200,255,.35)'; anim(def.flash, [{ opacity: 1 }, { opacity: 0 }], 300);
  } else {
    log(m, `${nickTag(atk)} попадает по ${nickTag(def)}: -${dmg}${crit}`); floatText(m, def, (s.crit ? 'КРИТ -' : '-') + dmg, s.crit ? COL.gold : COL.bad);
    def.flash.style.background = 'rgba(255,50,60,.5)'; anim(def.flash, [{ opacity: 1 }, { opacity: 0 }], 300);
    anim(def.pt, [{ transform: 'translate(0,0)' }, { transform: 'translate(-6px,4px)' }, { transform: 'translate(5px,-5px)' }, { transform: 'translate(-3px,2px)' }, { transform: 'translate(0,0)' }], 200);
  }
}
async function runMatch(m, a, ta, b, tb, title, onFinished, onBack, rematch) {
  let w1 = 0, w2 = 0, round = 1;
  while (w1 < 2 && w2 < 2) {
    if (!m.alive) return;
    const f1 = fighter(a, ta), f2 = fighter(b, tb);
    setHp(m.A, f1.hp); setHp(m.B, f2.hp); showActs(m.A, null); showActs(m.B, null);
    m.rnd.textContent = `РАУНД ${round}  ·  ${w1} : ${w2}`;
    if (!m.skip) { log(m, `<b class="gold">— Раунд ${round} —</b>`); await banner(m, `РАУНД ${round}`, 1.0); }
    let winner;
    if (m.skip) winner = finishRound(f1, f2);
    else {
      while (f1.hp > 0 && f2.hp > 0 && !m.skip && m.alive) {
        const rep = tick(f1, f2);
        showActs(m.A, rep.a1); showActs(m.B, rep.a2);
        if (rep.a1.attack) presentStrike(m, m.A, m.B, rep.on2, 1);
        if (rep.a2.attack) presentStrike(m, m.B, m.A, rep.on1, -1);
        if (!rep.a1.attack && !rep.a2.attack) log(m, '<span class="dim">…игроки выжидают</span>');
        setHp(m.A, f1.hp); setHp(m.B, f2.hp);
        await wait(m, TICK);
      }
      if (!m.alive) return;
      winner = finishRound(f1, f2);
    }
    setHp(m.A, f1.hp); setHp(m.B, f2.hp);
    const ws = winner === 1 ? m.A : m.B;
    if (winner === 1) w1++; else w2++;
    ws.wins[(winner === 1 ? w1 : w2) - 1].classList.add('on');
    m.rnd.textContent = `РАУНД ${round}  ·  ${w1} : ${w2}`;
    if (!m.skip) { log(m, `${nickTag(ws)} выигрывает раунд ${round}!`); await banner(m, `Раунд ${round}<br><span style="color:${ws.c}">${esc(ws.p.nickname)}</span>`, 1.6); }
    round++;
  }
  if (!m.alive) return;
  showResult(m, a, ta, b, tb, w1, w2, title, onFinished, onBack, rematch);
}
function showResult(m, a, ta, b, tb, s1, s2, title, onFinished, onBack, rematch) {
  const w = s1 > s2 ? a : b, wt = s1 > s2 ? ta : tb, wc = s1 > s2 ? COL.p1 : COL.p2;
  const d = document.createElement('div');
  d.className = 'result';
  d.innerHTML = `<h2>ПОБЕДИТЕЛЬ</h2>${portrait(w, wt, COL.gold)}<div class="wn" style="color:${wc}">${esc(w.nickname)}</div>
    <div class="acc">${esc(w.skill)}</div>${teamTag(wt)}
    <div class="score"><span style="color:${COL.p1}">${esc(a.nickname)}</span><b>${s1} : ${s2}</b><span style="color:${COL.p2}">${esc(b.nickname)}</span></div>
    ${rematch ? `<button class="btn" ${on(() => startMatch(a, ta, b, tb, title, onFinished, onBack, true))}>РЕВАНШ</button>` : ''}
    <button class="btn main" ${on(() => onFinished(s1, s2))}>ДАЛЕЕ</button>`;
  m.root.appendChild(d);
}

// ================= СЕЗОН =================
let seasonResetArmed = false;
function showSeason() {
  seasonResetArmed = false;
  const fin = D.phase === PH.Finished;
  const reset = `<button class="side-btn" ${on((e, el) => { if (!seasonResetArmed) { seasonResetArmed = true; el.textContent = 'точно?'; el.style.color = COL.bad; return; } S.resetAll(); showSeason(); })}>сброс</button>`;
  let h = topbar(`Сезон ${D.number}`, showMenu, reset) + `<div class="scroll"><div class="phase">${esc(S.phaseName())}</div>`;
  if (fin) h += championBlock();
  h += '<div class="sec">Регионы</div>';
  for (let r = 0; r < 5; r++) h += regionCard(r);
  h += '<div class="sec">Турниры</div>' + wcCard() + worldsCard() + historyBlock() + '</div>';
  if (fin) h += `<div class="bottom"><button class="btn main" ${on(() => { S.newSeason(); showSeason(); })}>НОВЫЙ СЕЗОН</button></div>`;
  screen(h);
}
function regionCard(r) {
  const st = D.regionState[r], done = st === RS.Done;
  const tap = on(() => { if (D.regionState[r] === RS.NotStarted) S.startRegion(r); showRegion(r); });
  let h = `<div class="card tap ${done ? 'grey' : ''}" ${tap}><div class="rc"><span class="stripe" style="background:${RCOL[r]}"></span>${regionLogo(r)}
    <div class="tx"><b>${esc(REG[r])}</b><span class="${done ? 'dim' : 'acc'}">${st === RS.NotStarted ? 'нажми, чтобы начать драфт' : esc(S.regionStateName(r))}</span></div>${done ? '' : '<span class="chev">›</span>'}</div>`;
  if (done) {
    const ct = TM.get(S.qualified(r, 0)), cp = S.playerOf(ct.id);
    h += `<div class="champ-line">${logo(ct)}${portrait(cp, null, COL.gold)}<div class="who"><b class="ell">${esc(cp ? cp.nickname : '')}</b><span>${esc(ct.name)} · чемпион</span></div>
      <div class="q">${[1, 2].map(k => logo(TM.get(S.qualified(r, k)))).join('')}</div></div>`;
  }
  return h + '</div>';
}
function wcCard() {
  const locked = D.phase === PH.Regions, done = D.phase > PH.WC;
  let sub = locked ? 'откроется, когда будут сыграны все 5 регионов' : !done ? `«царь горы» · ждут своей очереди: ${S.wcWaiting()}` : 'победитель — 16-я команда чемпионата мира';
  let right = '';
  if (done) { const t = TM.get(D.wcWinner), p = S.playerOf(D.wcWinner); right = `<div class="wcr">${logo(t)}${portrait(p, null, COL.gold)}</div>`; }
  return `<div class="card ${locked ? 'locked' : 'tap'} ${done ? 'grey' : ''}" ${locked ? '' : on(showWildCard)}><div class="rc"><div class="tx"><b class="gold">WILD CARD</b><span class="${locked ? 'dim' : ''}">${sub}</span></div>${right}${!locked && !done ? '<span class="chev">›</span>' : ''}</div></div>`;
}
function worldsCard() {
  const locked = D.phase < PH.WG;
  const sub = locked ? '15 команд из регионов + победитель Wild Card' : D.phase === PH.Finished ? 'чемпион мира определён' : S.phaseName();
  return `<div class="card ${locked ? 'locked grey' : 'tap'}" ${locked ? '' : on(showWorlds)}><div class="wl"><div class="logo"><img src="${img('worlds')}" alt="ЧМ"></div>
    <div class="tx" style="min-width:0"><b>ЧЕМПИОНАТ МИРА</b><span class="${locked ? 'dim' : 'acc'}">${esc(sub)}</span></div></div></div>`;
}
const sec = t => `<div class="sec">${esc(t)}</div>`;
const playerLabel = p => p ? `<span class="acc">${esc(p.nickname)}</span>` : '<span class="dim">игрок не выбран</span>';
const tableHead = () => '<div class="th"><span></span><span></span><span></span><span>команда</span><span class="c">В-П</span><span class="c">карты</span></div>';
function tableRow(place, row, mark, note) {
  const t = TM.get(row.team), p = S.playerOf(row.team);
  return `<div class="tr2"><span class="mk" style="background:${mark}"></span><span class="pl">${place}</span>${logo(t)}
    <div class="nm"><b class="ell">${esc(t.name)}</b><small>${playerLabel(p)}${note ? ` <span style="color:${mark}">${esc(note)}</span>` : ''}</small></div>
    <span class="c w">${row.wins}-${row.losses}</span><span class="c dim">${row.mw}:${row.ml}</span></div>`;
}
function matchRow(m, tag, isNext) {
  const ta = TM.get(m.t1), tb = TM.get(m.t2), pa = S.playerOf(m.t1), pb = S.playerOf(m.t2);
  const aw = m.played && win(m) === m.t1, bw = m.played && win(m) === m.t2;
  const mid = m.played ? `<span class="mid">${m.s1} : ${m.s2}</span>` : isNext ? '<span class="mid nx">далее</span>' : '<span class="mid vv">vs</span>';
  return `<div class="mr ${isNext ? 'next' : ''}">${tag ? `<div class="tag">${esc(tag)}</div>` : ''}<div class="ln">${logo(ta)}
    <div class="t ${m.played && !aw ? 'lose' : ''}"><div class="ell">${aw ? '<b>' + esc(ta.name) + '</b>' : esc(ta.name)}</div><small class="ell">${playerLabel(pa)}</small></div>${mid}
    <div class="t r ${m.played && !bw ? 'lose' : ''}"><div class="ell">${bw ? '<b>' + esc(tb.name) + '</b>' : esc(tb.name)}</div><small class="ell">${playerLabel(pb)}</small></div>${logo(tb)}</div></div>`;
}
function nextPanel(m, refresh, simStage) {
  const ta = TM.get(m.t1), tb = TM.get(m.t2), pa = S.playerOf(m.t1), pb = S.playerOf(m.t2);
  return `<div class="bottom"><div class="np-title">Следующий матч · ${esc(S.matchName(m))}</div>
    <div class="np">${portrait(pa, ta, COL.p1)}<div class="t"><b class="ell" style="color:${COL.p1}">${esc(pa ? pa.nickname : '?')}</b><small class="ell" style="display:block">${esc(ta.name)}</small></div><span class="v">VS</span>
    <div class="t r"><b class="ell" style="color:${COL.p2}">${esc(pb ? pb.nickname : '?')}</b><small class="ell" style="display:block">${esc(tb.name)}</small></div>${portrait(pb, tb, COL.p2)}</div>
    <button class="btn main" ${on(() => watchMatch(m, refresh))}>СМОТРЕТЬ МАТЧ</button>
    <div class="row2"><button class="btn" ${on(() => { S.quickSim(m); refresh(); })}>Быстрый результат</button><button class="btn" ${on(() => { simStage(); refresh(); })}>Досчитать этап</button></div></div>`;
}
function watchMatch(m, back) {
  const a = S.playerOf(m.t1), b = S.playerOf(m.t2);
  if (!a || !b) return;
  startMatch(a, TM.get(m.t1), b, TM.get(m.t2), `Сезон ${D.number} · ${S.matchName(m)}`, (s1, s2) => { S.report(m, s1, s2); back(); }, back);
}

function showRegion(r) {
  if (D.regionState[r] === RS.Draft) { showDraft(r); return; }
  const st = D.regionState[r], next = S.nextInRegion(r);
  let h = topbar(REG[r], showSeason) + `<div class="scroll"><div class="card alt"><div class="rc"><span class="stripe" style="background:${RCOL[r]}"></span>
    <div class="tx"><b>${esc(REG[r])}</b><span class="dim">${esc(S.regionStateName(r))}</span></div>${regionLogo(r)}</div></div>`;
  if (st === RS.Done) {
    h += sec('Путёвки на чемпионат мира');
    ['Чемпион', '2-е место', '3-е место'].forEach((pl, k) => {
      const t = TM.get(S.qualified(r, k)), p = S.playerOf(t.id);
      h += `<div class="card"><div class="champ-line" style="margin:0">${logo(t)}${portrait(p, null, k === 0 ? COL.gold : COL.panel2)}<div class="who"><b class="ell">${esc(p ? p.nickname : '')}</b><span>${esc(t.name)}</span></div><b class="${k === 0 ? 'gold' : 'dim'}">${pl}</b></div></div>`;
    });
  }
  h += sec('Таблица лиги') + tableHead();
  S.standings(ST.League, r).forEach((row, i) => h += tableRow(i + 1, row, i < 2 ? COL.gold : i < 6 ? COL.good : '#1A2135', i < 2 ? 'в полуфинал' : i < 6 ? 'плей-ин' : ''));
  if (st >= RS.PlayIn) {
    h += sec('Плей-офф');
    S.matches(ST.PlayIn, r).forEach(m => h += matchRow(m, 'Плей-ин', m === next));
    S.matches(ST.Semi, r).forEach(m => h += matchRow(m, 'Полуфинал', m === next));
    S.matches(ST.Final, r).sort((x, y) => y.kind - x.kind).forEach(m => h += matchRow(m, m.kind === 1 ? 'Матч за 3-е место' : 'Финал', m === next));
  }
  const all = S.matches(ST.League, r), played = all.filter(m => m.played);
  if (st === RS.League) { h += sec(`Матчи лиги · сыграно ${played.length} из ${all.length}`); all.filter(m => !m.played).slice(0, 6).forEach(m => h += matchRow(m, null, m === next)); }
  if (played.length) { h += sec('Результаты лиги'); played.slice(-8).reverse().forEach(m => h += matchRow(m, null, false)); }
  h += '</div>';
  if (next) h += nextPanel(next, () => showRegion(r), () => S.simRegionStage(r));
  screen(h);
}

function showDraft(r) {
  if (D.regionState[r] !== RS.Draft) { showRegion(r); return; }
  const picker = S.currentPicker(r), order = teamsIn(r), av = S.available(r);
  let h = topbar('Драфт · ' + REG[r], showSeason) + `<div class="picker">${logo(picker)}<div style="min-width:0"><small>ПИК ${S.pickNumber(r)} ИЗ ${order.length} · ВЫБИРАЕТ</small><b class="ell">${esc(picker.name)}</b><span class="dim" style="font-size:12px">нажми на игрока, чтобы выбрать его в эту команду</span></div></div>
    <div class="scroll">${sec('Свободные игроки · ' + av.length)}<div class="dgrid">`;
  for (const p of av) h += `<div class="dc" ${on(() => { S.pick(r, p.id); showDraft(r); })}>${portrait(p, null)}<div class="nick ell">${esc(p.nickname)}</div><div class="sk">${esc(p.skill)}</div></div>`;
  h += '</div>' + sec('Порядок выбора');
  order.forEach((t, k) => {
    const p = S.playerOf(t.id), now = picker && t.id === picker.id;
    h += `<div class="or ${now ? 'now' : ''}"><span class="n">${k + 1}</span>${logo(t)}<span class="nm ell">${esc(t.name)}</span>${p ? `<span class="pk">${esc(p.nickname)}${portrait(p, null)}</span>` : now ? '<span class="pk">выбирает…</span>' : ''}</div>`;
  });
  h += `</div><div class="bottom"><div class="row2" style="grid-template-columns:2fr 3fr"><button class="btn" ${on(() => { S.autoPick(r); showDraft(r); })}>Автовыбор за команду</button>
    <button class="btn main" ${on(() => { S.autoDraft(r); showRegion(r); })}>АВТОДРАФТ ДО КОНЦА</button></div></div>`;
  screen(h);
}

function showWildCard() {
  const next = D.phase === PH.WC ? S.nextWC() : null;
  const ms = S.matches(ST.WC);
  let h = topbar('Wild Card', showSeason) + `<div class="scroll"><div class="card alt dim">«Царь горы»: победитель остаётся, проигравший вылетает, его место занимает следующая команда. Последний матч — финал. Победитель едет на чемпионат мира 16-й командой.</div>`;
  const holderBox = (label, tid, extra) => { const t = TM.get(tid), p = S.playerOf(tid); return `<div class="card"><div class="gold" style="font:14px var(--display);margin-bottom:8px">${label}</div><div class="champ-line" style="margin:0">${logo(t, '')}${portrait(p, t, COL.gold)}<div class="who"><b class="ell">${esc(t.name)}</b><span>${esc(p ? p.nickname : '')}${extra}</span></div></div></div>`; };
  if (D.wcWinner >= 0) h += holderBox('ПОБЕДИТЕЛЬ WILD CARD', D.wcWinner, '');
  else if (next) {
    let streak = 0; for (let i = ms.length - 1; i >= 0 && ms[i].played && win(ms[i]) === next.t1; i--) streak++;
    h += holderBox('ЦАРЬ ГОРЫ', next.t1, ` · побед подряд: ${streak} · ждут: ${S.wcWaiting()}`);
  }
  if (ms.length) { h += sec(`Матчи · ${ms.filter(m => m.played).length} сыграно`); for (let i = ms.length - 1; i >= 0; i--) h += matchRow(ms[i], ms[i].kind === 1 ? 'Финал Wild Card' : null, ms[i] === next); }
  if (D.phase === PH.WC && D.wcNext < D.wcQueue.length) {
    h += sec('Очередь');
    D.wcQueue.slice(D.wcNext).forEach(id => { const t = TM.get(id); h += `<div class="or">${logo(t)}<span class="nm ell">${esc(t.name)} <small>${playerLabel(S.playerOf(id))} · ${esc(REG[t.region])}</small></span></div>`; });
  }
  h += '</div>';
  if (next) h += nextPanel(next, showWildCard, S.simWC);
  screen(h);
}

function seedLabel(tid) {
  if (tid === D.wcWinner) return 'Wild Card';
  const i = D.qualified.indexOf(tid);
  return i < 0 ? '' : `${REG_SHORT[Math.floor(i / 3)]} #${i % 3 + 1}`;
}
function showWorlds() {
  const next = S.nextWorlds();
  let h = topbar('Чемпионат мира', showSeason) + `<div class="scroll"><div class="card alt"><div class="wl"><div class="logo"><img src="${img('worlds')}" alt="ЧМ"></div><div style="min-width:0"><b>ЧЕМПИОНАТ МИРА</b><span class="acc">${esc(S.phaseName())}</span></div></div></div>`;
  if (D.phase === PH.Finished) h += championBlock();
  if (D.phase >= PH.WQ) {
    h += sec('Плей-офф');
    const qf = ['A1 – B2', 'B1 – A2', 'C1 – D2', 'D1 – C2'];
    S.matches(ST.WQ, 0).forEach((m, i) => h += matchRow(m, 'Четвертьфинал · ' + qf[Math.min(i, 3)], m === next));
    S.matches(ST.WS, 0).forEach(m => h += matchRow(m, 'Полуфинал', m === next));
    S.matches(ST.WF, 0).forEach(m => h += matchRow(m, 'Финал чемпионата мира', m === next));
  }
  for (let g = 0; g < 4; g++) {
    h += sec('Группа ' + S.letter(g)) + tableHead();
    S.standings(ST.WG, g).forEach((row, i) => h += tableRow(i + 1, row, i < 2 ? COL.good : '#1A2135', seedLabel(row.team)));
  }
  h += '</div>';
  if (next) h += nextPanel(next, showWorlds, S.simWorldsStage);
  screen(h);
}
function championBlock() {
  const t = TM.get(D.champion); if (!t) return '';
  const p = S.playerOf(t.id);
  return `<div class="big-champ"><h3>ЧЕМПИОН МИРА · СЕЗОН ${D.number}</h3><div class="pair">${logo(t)}${portrait(p, t, COL.gold)}</div>
    <div class="nm">${esc(t.name)} · ${esc(p ? p.nickname : '')}</div><div class="dim">${esc(REG[t.region])}</div></div>`;
}
function historyBlock() {
  const h = D.historyTeams; if (!h.length) return '';
  let out = sec('Зал славы');
  for (let i = h.length - 1; i >= Math.max(0, h.length - 10); i--) {
    const t = TM.get(h[i]); if (!t) continue;
    const no = D.number - (D.phase === PH.Finished ? 0 : 1) - (h.length - 1 - i);
    const who = D.historyPlayers[i] || '';
    out += `<div class="hist"><span class="s">Сезон ${no}</span>${logo(t)}<b class="ell" style="flex:1">${esc(t.name)}</b><span class="dim">${esc(who)}</span></div>`;
  }
  return out;
}

// ================= НАСТРОЙКИ =================
function showSettings(tab) {
  const prev = app.querySelector('.scroll.set'); const keep = prev && prev.dataset.tab == tab ? prev.scrollTop : 0;
  let armed = false;
  let h = topbar('Настройки', showMenu) + `<div class="tabs">${['LCS', 'LEC', 'КЛ', 'LPL', 'LCK', 'Команды'].map((n, i) => `<button class="btn ${i === tab ? 'on' : ''}" ${on(() => showSettings(i))}>${n}</button>`).join('')}</div>
    <div class="scroll set" data-tab="${tab}">`;
  if (tab < 5) {
    h += `<div class="dim" style="font-size:13px">${esc(REG[tab])} · сила (50–75) и описание (нажми, чтобы изменить)</div>`;
    for (const p of inRegion(tab)) {
      const ch = (p.skill || '') !== BASE.skill[p.id];
      h += `<div class="sr">${portrait(p, null)}<div class="mid2"><b class="ell">${esc(p.nickname)}</b><div class="skb ${ch ? 'ch' : ''}" ${on(() => editSkill(p, tab))}>${p.skill ? esc(p.skill) : '<span class="dim">добавить описание…</span>'} ›</div></div>${stepper(p.power, 50, 75, BASE.power[p.id], v => setOV('power', p.id, v))}</div>`;
    }
  } else {
    h += '<div class="dim" style="font-size:13px">Бонус команды к силе игрока (от 1 до 10)</div>';
    for (let r = 0; r < 5; r++) {
      h += `<div class="sec" style="color:${RCOL[r]}">${esc(REG[r])}</div>`;
      for (const t of teamsIn(r)) h += `<div class="sr">${logo(t)}<div class="mid2"><b class="ell">${esc(t.name)}</b></div>${stepper(t.bonus, 1, 10, BASE.bonus[t.id], v => setOV('bonus', t.id, v))}</div>`;
    }
  }
  h += `</div><div class="bottom"><div class="dim" style="text-align:center;font-size:12px">Сохраняется автоматически</div>
    <button class="btn" ${on((e, el) => { if (!armed) { armed = true; el.textContent = 'Точно сбросить?'; el.style.color = COL.bad; return; } resetOV(); showSettings(tab); })}>Сбросить всё к исходным</button></div>`;
  screen(h);
  app.querySelector('.scroll.set').scrollTop = keep;
}
function stepper(start, min, max, def, set) {
  let v = start;
  const id = 'st' + (++hid);
  const paint = () => { const s = document.getElementById(id); if (s) { s.textContent = v; s.classList.toggle('ch', v !== def); } };
  const ch = d => { const n = clamp(v + d, min, max); if (n === v) return; v = n; set(v); paint(); };
  return `<div class="step"><button ${on(() => ch(-1))} aria-label="Меньше">-</button><span id="${id}" class="${v !== def ? 'ch' : ''}">${v}</span><button ${on(() => ch(1))} aria-label="Больше">+</button></div>`;
}
function editSkill(p, tab) {
  const m = document.createElement('div');
  m.className = 'modal';
  m.innerHTML = `<div class="scroll" style="align-items:center;gap:12px;padding-top:24px">
    <div style="width:140px">${portrait(p, null)}</div><b style="font:24px var(--display)">${esc(p.nickname)}</b>
    <div class="dim" style="font-size:13px;text-align:center">Описание игрока (показывается в профиле и в бою)</div>
    <textarea id="skill-input" maxlength="80" placeholder="например: Ветеран. Огромный боевой опыт.">${esc(p.skill)}</textarea>
    <div style="display:grid;grid-template-columns:1fr 1fr 1.4fr;gap:6px;width:100%">
      <button class="btn" ${on(() => m.remove())}>Отмена</button>
      <button class="btn" ${on(() => { m.querySelector('textarea').value = BASE.skill[p.id]; })}>Исходное</button>
      <button class="btn main" ${on(() => { setOV('skill', p.id, m.querySelector('textarea').value.trim()); showSettings(tab); })}>СОХРАНИТЬ</button></div></div>`;
  app.appendChild(m);
  setTimeout(() => { try { m.querySelector('textarea').focus(); } catch (e) { } }, 50);
}

// ================= ОБЛАЧНОЕ СОХРАНЕНИЕ =================
// Владелец страницы хранит сезон и настройки в базе на claude.ai (одинаково на телефоне и компьютере).
// Остальные — только в браузере. Сначала всегда грузится локальная копия, потом подтягивается облако.
function syncText() {
  return { local: 'Сохранение: на этом устройстве', loading: 'Сохранение: подключаюсь к облаку…', cloud: 'Сохранение: в облаке claude.ai', error: 'Облако недоступно, сохраняю на устройстве' }[CLOUD.state];
}
function paintSync() { const el = document.getElementById('sync'); if (el) { el.textContent = syncText(); el.className = 'sync ' + CLOUD.state; } }
const cloudBody = k => k === 'season' ? D : OV;
async function cloudSave(k) {
  if (!CLOUD.on) return;
  CLOUD.dirty[k] = true;
  if (CLOUD.busy[k]) return;
  CLOUD.busy[k] = true;
  try {
    while (CLOUD.dirty[k]) {
      CLOUD.dirty[k] = false;
      await new Promise(r => setTimeout(r, 600));
      try { await CLOUD.db.doc('save/' + k).set(JSON.parse(JSON.stringify(cloudBody(k)))); if (CLOUD.state !== 'cloud') { CLOUD.state = 'cloud'; paintSync(); } }
      catch (e) { CLOUD.state = 'error'; paintSync(); if (e && (e.code === 'revoked' || e.code === 'not_granted')) { CLOUD.on = false; break; } }
    }
  } finally { CLOUD.busy[k] = false; }
}
async function cloudInit() {
  if (!window.claude || !window.claude.use) return;
  CLOUD.state = 'loading'; paintSync();
  try {
    const [db, user] = await Promise.all([window.claude.use('db'), window.claude.use('user')]);
    if (!db || !user || !user.isOwner()) { CLOUD.state = 'local'; paintSync(); return; }
    CLOUD.db = db;
    const [s, o] = await Promise.all([db.doc('save/season').get(), db.doc('save/settings').get()]);
    let changed = false;
    if (o.exists) { const v = o.data(); OV = { power: { ...(v.power || {}) }, bonus: { ...(v.bonus || {}) }, skill: { ...(v.skill || {}) } }; store.set(OKEY, OV); applyOV(); changed = true; }
    if (s.exists) {
      const prev = D; D = JSON.parse(JSON.stringify(s.data())); store.set(SKEY, D); loadSeason();
      changed = changed || JSON.stringify(prev) !== JSON.stringify(D);
    }
    CLOUD.on = true; CLOUD.state = 'cloud'; paintSync();
    if (!s.exists) cloudSave('season');      // первый запуск: переносим локальный прогресс в облако
    if (!o.exists) cloudSave('settings');
    if (changed && !M) showMenu();
  } catch (e) { CLOUD.state = 'error'; paintSync(); }
}

showMenu();
cloudInit();
