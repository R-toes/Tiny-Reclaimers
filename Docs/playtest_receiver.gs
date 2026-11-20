/**
 * TRASH TD playtest receiver (Google Apps Script, bound to the playtest Google Sheet).
 *
 * The game posts one event at a time (session, platform, build, event, stage, data). This script
 *   - keeps every event in the "Log" tab (a raw backup),
 *   - writes it, split into readable columns, to "Sessions", "Stages", "Feedback" or "Errors",
 *   - keeps per-operator totals in "Operators",
 *   - and "Dashboard" shows the totals and charts (it reads the other tabs with formulas, so it updates by itself).
 *
 * Setup: Docs/PLAYTEST_SETUP.md. After pasting this file: run setupSheets() once (it clears the old rows and builds the
 * tabs and charts), then Deploy > Manage deployments > pencil > New version > Deploy. Later you can rebuild any time from
 * the sheet's "TRASH TD" menu (this erases the collected rows).
 */

var HEADERS = {
  Stages: ['Time', 'Tester', 'Platform', 'Device', 'Build', 'Stage', 'Difficulty', 'Result', 'Stars', 'Seconds', 'Wave', 'Waves',
    'Lives left', 'Lives lost', 'Rerolls', 'Avg FPS', 'Hitches', 'Worst frame (ms)', 'Operators used', 'Operators lost', 'Drafted'],
  Operators: ['Operator', 'Drafted', 'Deployed', 'Retreated', 'Lost', 'Stages used', 'Stages cleared', 'Clear rate'],
  Sessions: ['Time', 'Tester', 'Session', 'Platform', 'Build', 'Device', 'OS', 'RAM (MB)', 'GPU', 'Screen', 'DPI', 'Refresh (Hz)',
    'Controls', 'CRT', 'Touch', 'UI size', 'FPS cap', 'Picture'],
  Feedback: ['Time', 'Tester', 'Platform', 'Build', 'Stage', 'Rating', 'Comment'],
  Errors: ['Time', 'Tester', 'Platform', 'Build', 'Stage', 'Type', 'Message', 'Where'],
  Log: ['Time', 'Session', 'Platform', 'Build', 'Event', 'Stage', 'Data']
};

var DATA_TABS = ['Stages', 'Operators', 'Sessions', 'Feedback', 'Errors', 'Log'];
var BANDING_ROWS = 3000;

// ---------------------------------------------------------------- receiving

function doPost(e) {
  var p = (e && e.parameter) || {};
  var lock = LockService.getScriptLock();
  lock.waitLock(20000);
  try {
    recordEvent_(SpreadsheetApp.getActiveSpreadsheet(), p);
    return ContentService.createTextOutput('ok');
  } finally {
    lock.releaseLock();
  }
}

/** Writes one event (the six fields the game posts) into the Log and the tab it belongs to. */
function recordEvent_(book, p) {
  {
    var now = new Date();
    var data = parseData_(p.data);
    var session = String(p.session || '');
    var tester = session.split('-')[0];
    var platform = String(p.platform || '');
    var build = String(p.build || '');
    var stage = String(p.stage || '') === '-' ? '' : String(p.stage || '');

    appendRow_(sheet_(book, 'Log'), [now, session, platform, build, String(p.event || ''), stage, clip_(p.data, 1000)]);

    switch (String(p.event)) {
      case 'session_start':
        appendRow_(sheet_(book, 'Sessions'), [now, tester, session, platform, build, text_(data.device), text_(data.os),
          num_(data.ramMb), text_(data.gpu), text_(data.screen), num_(data.dpi), num_(data.refreshHz), text_(data.controls),
          text_(data.crt), data.touch === true ? 'yes' : 'no', text_(data.uiSize), num_(data.fpsCap), text_(data.picture)]);
        break;
      case 'stage_end':
        appendRow_(sheet_(book, 'Stages'), [now, tester, platform, text_(data.device), build, stage, text_(data.difficulty),
          text_(data.result), num_(data.stars), num_(data.seconds), num_(data.wave), num_(data.waves), num_(data.livesLeft),
          num_(data.livesLost), num_(data.rerolls), num_(data.avgFps), num_(data.hitches), num_(data.worstMs),
          counts_(data.deployed), counts_(data.died), list_(data.picks)]);
        updateOperators_(book, data);
        break;
      case 'feedback':
        appendRow_(sheet_(book, 'Feedback'), [now, tester, platform, build, stage, num_(data.rating), text_(data.comment)]);
        break;
      case 'error':
        appendRow_(sheet_(book, 'Errors'), [now, tester, platform, build, stage, text_(data.type), text_(data.message), text_(data.where)]);
        break;
      default:
        break; // stage_start and anything new stay in the Log only
    }
  }
}

// Opening the web app address in a browser shows this, so you can check it is deployed.
function doGet() {
  return ContentService.createTextOutput('TRASH TD playtest receiver is running');
}

function onOpen() {
  SpreadsheetApp.getUi().createMenu('TRASH TD')
    .addItem('Rebuild sheets and charts (erases the collected rows)', 'setupSheets')
    .addItem('Fill with SAMPLE data (to preview the dashboard)', 'addSampleData')
    .addToUi();
}

/**
 * Adds made-up testers and stage runs so you can see the dashboard and charts with data in them. The rows are fake:
 * run setupSheets() afterwards (it erases everything) before real testers send their data.
 */
function addSampleData() {
  var book = SpreadsheetApp.getActiveSpreadsheet();
  var seed = 7;
  function random() {
    seed = (seed * 16807) % 2147483647;
    return seed / 2147483647;
  }
  function pick(items) { return items[Math.floor(random() * items.length)]; }

  var testers = [
    { id: 'a1b2c3d4', platform: 'Android', device: 'Pixel 7', fps: 58, os: 'Android 14', ram: 8000, touch: true },
    { id: 'e5f6a7b8', platform: 'Android', device: 'Galaxy A32', fps: 31, os: 'Android 12', ram: 4000, touch: true },
    { id: 'c9d0e1f2', platform: 'WindowsPlayer', device: 'Desktop PC', fps: 144, os: 'Windows 11', ram: 16000, touch: false },
    { id: '0a1b2c3d', platform: 'WindowsPlayer', device: 'Laptop', fps: 75, os: 'Windows 10', ram: 8000, touch: false }
  ];
  var stages = ['STAGE 1', 'STAGE 2', 'STAGE 3', 'STAGE 4'];
  var operators = ['Guard', 'Sniper', 'Medic', 'Caster', 'Defender', 'Specialist'];

  var lock = LockService.getScriptLock();
  lock.waitLock(30000);
  try {
    for (var t = 0; t < testers.length; t++) {
      var tester = testers[t];
      var session = tester.id + '-' + ('00' + t).slice(-2) + 'ab';
      var base = { session: session, platform: tester.platform, build: '0.1' };
      recordEvent_(book, merge_(base, { event: 'session_start', stage: '-', data: JSON.stringify({
        device: tester.device, os: tester.os, ramMb: tester.ram, gpu: 'Sample GPU', screen: tester.touch ? '2400x1080' : '1920x1080',
        dpi: tester.touch ? 420 : 96, refreshHz: tester.touch ? 90 : 144, controls: tester.touch ? 'Swipe' : 'Precise',
        crt: tester.touch ? 'Off' : 'Classic', touch: tester.touch, uiSize: 'Normal', fpsCap: 60, picture: 'Full', t: 2 }) }));

      var runs = 5 + Math.floor(random() * 4);
      for (var r = 0; r < runs; r++) {
        var stage = stages[Math.min(stages.length - 1, Math.floor(r / 2))];
        var roll = random();
        var result = roll < 0.55 ? 'clear' : roll < 0.8 ? 'fail' : 'quit';
        var waves = 8 + stages.indexOf(stage) * 2;
        var wave = result === 'clear' ? waves : Math.max(1, Math.floor(waves * random()));
        var deployed = {};
        var picks = [];
        for (var o = 0; o < 4; o++) {
          var name = pick(operators);
          picks.push(name);
          deployed[name] = (deployed[name] || 0) + 1;
        }
        recordEvent_(book, merge_(base, { event: 'stage_start', stage: stage, data: '{"difficulty":"Normal"}' }));
        recordEvent_(book, merge_(base, { event: 'stage_end', stage: stage, data: JSON.stringify({
          result: result, stars: result === 'clear' ? 1 + Math.floor(random() * 3) : 0, difficulty: 'Normal', device: tester.device,
          seconds: 240 + Math.floor(random() * 600), wave: wave, waves: waves, livesLeft: result === 'clear' ? 1 + Math.floor(random() * 3) : 0,
          livesLost: Math.floor(random() * 4), rerolls: Math.floor(random() * 3), avgFps: Math.max(15, Math.round(tester.fps * (0.85 + random() * 0.2))),
          hitches: Math.floor(random() * 30), worstMs: 40 + Math.floor(random() * 200), deployed: deployed,
          retreated: random() < 0.4 ? (function () { var x = {}; x[pick(operators)] = 1; return x; })() : {},
          died: random() < 0.5 ? (function () { var x = {}; x[pick(operators)] = 1; return x; })() : {}, picks: picks, t: 300 + r * 400 }) }));

        if (random() < 0.35) {
          recordEvent_(book, merge_(base, { event: 'feedback', stage: stage, data: JSON.stringify({
            rating: 2 + Math.floor(random() * 4), comment: pick(['Fun!', 'Too hard on this stage', 'The deck bar is small', 'Loved the art', 'Wave 6 is brutal']), t: 310 + r * 400 }) }));
        }
      }
      if (t === 1) {
        recordEvent_(book, merge_(base, { event: 'error', stage: 'STAGE 2', data: JSON.stringify({
          type: 'Exception', message: 'NullReferenceException: sample error', where: 'Sample.cs:42', t: 900 }) }));
      }
    }
  } finally {
    lock.releaseLock();
  }
}

function merge_(a, b) {
  var out = {};
  var k;
  for (k in a) out[k] = a[k];
  for (k in b) out[k] = b[k];
  return out;
}

/** Adds this stage's operators to the per-operator totals. */
function updateOperators_(book, data) {
  var sheet = sheet_(book, 'Operators');
  var cleared = String(data.result) === 'clear';
  var tally = {};
  function entry(name) {
    if (!tally[name]) tally[name] = { drafted: 0, deployed: 0, retreated: 0, lost: 0, used: 0 };
    return tally[name];
  }

  var picks = Array.isArray(data.picks) ? data.picks : [];
  for (var i = 0; i < picks.length; i++) entry(String(picks[i])).drafted += 1;
  addCounts_(data.deployed, entry, 'deployed');
  addCounts_(data.retreated, entry, 'retreated');
  addCounts_(data.died, entry, 'lost');
  var deployedNames = data.deployed && typeof data.deployed === 'object' ? Object.keys(data.deployed) : [];
  for (var d = 0; d < deployedNames.length; d++) entry(deployedNames[d]).used = 1;

  var last = sheet.getLastRow();
  var rowOf = {};
  if (last > 1) {
    var names = sheet.getRange(2, 1, last - 1, 1).getValues();
    for (var r = 0; r < names.length; r++) rowOf[String(names[r][0])] = r + 2;
  }

  var keys = Object.keys(tally);
  for (var k = 0; k < keys.length; k++) {
    var name = keys[k];
    var t = tally[name];
    var add = [t.drafted, t.deployed, t.retreated, t.lost, t.used, cleared ? t.used : 0]; // columns B..G
    if (rowOf[name]) {
      var range = sheet.getRange(rowOf[name], 2, 1, 6);
      var current = range.getValues()[0];
      var next = [];
      for (var c = 0; c < 6; c++) next.push((Number(current[c]) || 0) + add[c]);
      range.setValues([next]);
    } else {
      sheet.appendRow([name].concat(add));
      var newRow = sheet.getLastRow();
      rowOf[name] = newRow;
      sheet.getRange(newRow, 8).setFormula('=IFERROR(G' + newRow + '/F' + newRow + ',0)');
    }
  }
}

function addCounts_(counts, entry, field) {
  if (!counts || typeof counts !== 'object') return;
  var names = Object.keys(counts);
  for (var i = 0; i < names.length; i++) entry(names[i])[field] += Number(counts[names[i]]) || 0;
}

// ---------------------------------------------------------------- small helpers

function parseData_(raw) {
  try {
    var parsed = JSON.parse(String(raw || '{}'));
    return parsed && typeof parsed === 'object' ? parsed : {};
  } catch (err) {
    return {};
  }
}

function text_(value) {
  return value === undefined || value === null ? '' : String(value).slice(0, 500);
}

function num_(value) {
  var n = Number(value);
  return value === undefined || value === null || value === '' || isNaN(n) ? '' : n;
}

function clip_(value, max) {
  return String(value || '').slice(0, max);
}

/** {"Guard":2,"Sniper":1} -> "Guard x2, Sniper x1" (most first). */
function counts_(counts) {
  if (!counts || typeof counts !== 'object') return '';
  var names = Object.keys(counts);
  names.sort(function (a, b) { return (Number(counts[b]) || 0) - (Number(counts[a]) || 0); });
  return names.map(function (name) { return name + ' x' + counts[name]; }).join(', ');
}

/** ["Guard","Sniper","Guard"] -> "Guard x2, Sniper x1". */
function list_(items) {
  if (!Array.isArray(items)) return '';
  var counts = {};
  for (var i = 0; i < items.length; i++) counts[items[i]] = (counts[items[i]] || 0) + 1;
  return counts_(counts);
}

function appendRow_(sheet, row) {
  sheet.appendRow(row);
}

/** The tab with this name; made (with its header row) when it is missing. */
function sheet_(book, name) {
  var sheet = book.getSheetByName(name);
  if (!sheet) {
    sheet = book.insertSheet(name);
    sheet.appendRow(HEADERS[name]);
  } else if (sheet.getLastRow() === 0) {
    sheet.appendRow(HEADERS[name]);
  }
  return sheet;
}

// ---------------------------------------------------------------- building the sheets

/**
 * Run this once from the editor (select setupSheets, press Run). It ERASES the collected rows and builds: the data tabs
 * with headers and formatting, a "Tables" tab of summary formulas and a "Dashboard" tab with totals and charts.
 */
function setupSheets() {
  var book = SpreadsheetApp.getActiveSpreadsheet();

  // 1. The data tabs: empty, with a header row, formatted
  for (var i = 0; i < DATA_TABS.length; i++) {
    var name = DATA_TABS[i];
    var sheet = book.getSheetByName(name) || book.insertSheet(name);
    resetSheet_(sheet);
    var headers = HEADERS[name];
    sheet.getRange(1, 1, 1, headers.length).setValues([headers]);
    formatDataTab_(sheet, name, headers.length);
  }

  // 2. The summary tab and the dashboard
  var tables = book.getSheetByName('Tables') || book.insertSheet('Tables');
  resetSheet_(tables);
  buildTables_(tables);

  var dash = book.getSheetByName('Dashboard') || book.insertSheet('Dashboard');
  resetSheet_(dash);
  buildDashboard_(dash, tables);

  // 3. Tidy: the old single "Playtest" tab is gone, Dashboard first
  var old = book.getSheetByName('Playtest');
  if (old && book.getSheets().length > 1) book.deleteSheet(old);

  var order = ['Dashboard', 'Stages', 'Operators', 'Sessions', 'Feedback', 'Errors', 'Tables', 'Log'];
  for (var o = 0; o < order.length; o++) {
    var tab = book.getSheetByName(order[o]);
    if (tab) {
      book.setActiveSheet(tab);
      book.moveActiveSheet(o + 1);
    }
  }
  book.setActiveSheet(book.getSheetByName('Dashboard'));
  SpreadsheetApp.flush();
}

function resetSheet_(sheet) {
  var charts = sheet.getCharts();
  for (var i = 0; i < charts.length; i++) sheet.removeChart(charts[i]);
  var bandings = sheet.getBandings();
  for (var b = 0; b < bandings.length; b++) bandings[b].remove();
  sheet.clearConditionalFormatRules();
  sheet.clear();
}

var HEADER_BG = '#1c4a3d';
var HEADER_FG = '#ffffff';

function formatDataTab_(sheet, name, columns) {
  var header = sheet.getRange(1, 1, 1, columns);
  header.setBackground(HEADER_BG).setFontColor(HEADER_FG).setFontWeight('bold').setVerticalAlignment('middle');
  sheet.setRowHeight(1, 30);
  sheet.setFrozenRows(1);
  if (name !== 'Operators') sheet.setFrozenColumns(1);
  sheet.getRange(1, 1, BANDING_ROWS, columns).applyRowBanding(SpreadsheetApp.BandingTheme.LIGHT_GREY, false, false);

  // Time column: readable date and hour
  if (name !== 'Operators') sheet.getRange(2, 1, BANDING_ROWS, 1).setNumberFormat('yyyy-mm-dd hh:mm');

  var widths = {
    Stages: [130, 80, 90, 150, 70, 90, 80, 70, 50, 70, 50, 50, 70, 70, 60, 70, 70, 100, 260, 220, 260],
    Operators: [170, 80, 90, 90, 70, 100, 110, 100],
    Sessions: [130, 80, 120, 90, 70, 150, 220, 80, 180, 90, 60, 90, 90, 80, 60, 70, 70, 80],
    Feedback: [130, 80, 90, 70, 90, 60, 480],
    Errors: [130, 80, 90, 70, 90, 80, 420, 320],
    Log: [130, 120, 90, 70, 110, 90, 600]
  }[name];
  for (var c = 0; c < widths.length; c++) sheet.setColumnWidth(c + 1, widths[c]);

  var rules = [];
  if (name === 'Stages') {
    var result = sheet.getRange(2, 8, BANDING_ROWS, 1);
    rules.push(textRule_('clear', '#b7e1cd', '#0b5329', result));
    rules.push(textRule_('fail', '#f4c7c3', '#a50e0e', result));
    rules.push(textRule_('quit', '#e0e0e0', '#555555', result));
    var fps = sheet.getRange(2, 16, BANDING_ROWS, 1);
    rules.push(SpreadsheetApp.newConditionalFormatRule().whenNumberBetween(1, 29).setBackground('#f4c7c3').setRanges([fps]).build());
    rules.push(SpreadsheetApp.newConditionalFormatRule().whenNumberBetween(30, 44).setBackground('#fce8b2').setRanges([fps]).build());
    sheet.getRange(2, 8, BANDING_ROWS, 2).setHorizontalAlignment('center');
    sheet.getRange(2, 10, BANDING_ROWS, 9).setHorizontalAlignment('right');
  }
  if (name === 'Operators') {
    sheet.getRange(2, 8, BANDING_ROWS, 1).setNumberFormat('0%');
    sheet.getRange(2, 2, BANDING_ROWS, 6).setHorizontalAlignment('right');
  }
  if (name === 'Feedback') {
    var rating = sheet.getRange(2, 6, BANDING_ROWS, 1);
    rules.push(SpreadsheetApp.newConditionalFormatRule().whenNumberLessThanOrEqualTo(2).setBackground('#f4c7c3').setRanges([rating]).build());
    rules.push(SpreadsheetApp.newConditionalFormatRule().whenNumberGreaterThanOrEqualTo(4).setBackground('#b7e1cd').setRanges([rating]).build());
    sheet.getRange(2, 6, BANDING_ROWS, 1).setHorizontalAlignment('center');
    sheet.getRange(2, 7, BANDING_ROWS, 1).setWrap(true);
  }
  if (name === 'Errors') {
    sheet.getRange(2, 7, BANDING_ROWS, 2).setWrap(true);
  }
  if (rules.length) sheet.setConditionalFormatRules(rules);
}

function textRule_(text, background, fontColor, range) {
  return SpreadsheetApp.newConditionalFormatRule().whenTextEqualTo(text).setBackground(background).setFontColor(fontColor)
    .setRanges([range]).build();
}

/** Summary tables the charts read. Formulas only, so they update by themselves. */
function buildTables_(sheet) {
  sheet.setHiddenGridlines(true);
  sheet.getRange('A1').setValue('Summary tables (formulas; the Dashboard charts read these)').setFontWeight('bold').setFontSize(12);

  // Block A: results by stage (A:D)
  sheet.getRange('A2:D2').setValues([['Stage', 'Cleared', 'Failed', 'Left']]);
  sheet.getRange('A3').setFormula('=IFERROR(SORT(UNIQUE(FILTER(Stages!F2:F, Stages!F2:F<>""))),"")');
  sheet.getRange('B3').setFormula('=ARRAYFORMULA(IF(A3:A="","",COUNTIFS(Stages!F2:F,A3:A,Stages!H2:H,"clear")))');
  sheet.getRange('C3').setFormula('=ARRAYFORMULA(IF(A3:A="","",COUNTIFS(Stages!F2:F,A3:A,Stages!H2:H,"fail")))');
  sheet.getRange('D3').setFormula('=ARRAYFORMULA(IF(A3:A="","",COUNTIFS(Stages!F2:F,A3:A,Stages!H2:H,"quit")))');

  // Block B: average minutes to clear each stage (F:G)
  sheet.getRange('F2').setFormula('=IFERROR(QUERY(Stages!A1:U,"select F, avg(J)/60 where F is not null and H = \'clear\' group by F order by F label F \'Stage\', avg(J)/60 \'Avg minutes (cleared)\'",1),{"Stage","Avg minutes (cleared)"})');
  sheet.getRange('G3:G200').setNumberFormat('0.0');

  // Block C: average lives lost per stage (I:J)
  sheet.getRange('I2').setFormula('=IFERROR(QUERY(Stages!A1:U,"select F, avg(N) where F is not null group by F order by F label F \'Stage\', avg(N) \'Avg lives lost\'",1),{"Stage","Avg lives lost"})');
  sheet.getRange('J3:J200').setNumberFormat('0.0');

  // Block D: ratings (L:M)
  sheet.getRange('L2:M2').setValues([['Rating', 'Count']]);
  for (var star = 1; star <= 5; star++) {
    sheet.getRange(2 + star, 12).setValue(String(star));
    sheet.getRange(2 + star, 13).setFormula('=COUNTIF(Feedback!$F$2:$F,' + star + ')');
  }

  // Block E: launches by platform (O:P)
  sheet.getRange('O2').setFormula('=IFERROR(QUERY(Sessions!A1:R,"select D, count(C) where D is not null group by D label D \'Platform\', count(C) \'Launches\'",1),{"Platform","Launches"})');

  // Block F: average fps by device (R:S)
  sheet.getRange('R2').setFormula('=IFERROR(QUERY(Stages!A1:U,"select D, avg(P) where P > 0 group by D order by avg(P) label D \'Device\', avg(P) \'Avg FPS\'",1),{"Device","Avg FPS"})');
  sheet.getRange('S3:S200').setNumberFormat('0');

  // Block G: most drafted operators (U:V)
  sheet.getRange('U2').setFormula('=IFERROR(QUERY(Operators!A1:H,"select A, B where B > 0 order by B desc limit 10 label A \'Operator\', B \'Times drafted\'",1),{"Operator","Times drafted"})');

  // Block H: clear rate of operators used in at least 3 stages (X:Y)
  sheet.getRange('X2').setFormula('=IFERROR(QUERY(Operators!A1:H,"select A, H where F >= 3 order by H desc limit 10 label A \'Operator\', H \'Clear rate\'",1),{"Operator","Clear rate"})');
  sheet.getRange('Y3:Y200').setNumberFormat('0%');

  var headers = ['A2:D2', 'F2:G2', 'I2:J2', 'L2:M2', 'O2:P2', 'R2:S2', 'U2:V2', 'X2:Y2'];
  for (var i = 0; i < headers.length; i++) {
    sheet.getRange(headers[i]).setBackground(HEADER_BG).setFontColor(HEADER_FG).setFontWeight('bold');
  }
  var titles = {
    A: 'Results by stage', F: 'Minutes to clear', I: 'Lives lost', L: 'Ratings', O: 'Launches', R: 'Frame rate by device',
    U: 'Most drafted', X: 'Operator clear rate (3+ stages)'
  };
  var letters = Object.keys(titles);
  for (var t = 0; t < letters.length; t++) {
    sheet.getRange(letters[t] + '1').setValue(titles[letters[t]]).setFontWeight('bold');
  }
  sheet.getRange('A1').setValue('Results by stage');
  sheet.setColumnWidths(1, 25, 110);
}

function buildDashboard_(dash, tables) {
  dash.setHiddenGridlines(true);
  dash.setColumnWidths(1, 16, 78);

  dash.getRange('A1').setValue('TRASH TD  -  PLAYTEST DASHBOARD').setFontSize(22).setFontWeight('bold').setFontColor(HEADER_BG);
  dash.getRange('A2').setFormula('="Last event: "&IFERROR(TEXT(MAX(Log!A2:A),"yyyy-mm-dd hh:mm"),"none yet")&"   |   updates by itself as testers play"')
    .setFontColor('#666666');

  // Totals (each tile is two merged cells wide)
  var tiles = [
    ['TESTERS', '=IFERROR(COUNTUNIQUE(FILTER(Sessions!B2:B,Sessions!B2:B<>"")),0)', '0'],
    ['LAUNCHES', '=COUNTA(Sessions!C2:C)', '0'],
    ['STAGE RUNS', '=COUNTA(Stages!F2:F)', '0'],
    ['CLEAR RATE', '=IFERROR(COUNTIF(Stages!H2:H,"clear")/COUNTA(Stages!H2:H),0)', '0%'],
    ['AVG RATING', '=IFERROR(AVERAGE(Feedback!F2:F),0)', '0.0'],
    ['AVG FPS', '=IFERROR(AVERAGEIF(Stages!P2:P,">0"),0)', '0'],
    ['ERRORS', '=COUNTA(Errors!G2:G)', '0']
  ];
  for (var i = 0; i < tiles.length; i++) {
    var col = 1 + i * 2;
    var label = dash.getRange(4, col, 1, 2);
    label.merge().setValue(tiles[i][0]).setBackground(HEADER_BG).setFontColor('#9fe3c0').setFontWeight('bold').setFontSize(9)
      .setHorizontalAlignment('center');
    var value = dash.getRange(5, col, 1, 2);
    value.merge().setFormula(tiles[i][1]).setNumberFormat(tiles[i][2]).setBackground('#e8f3ee').setFontColor(HEADER_BG)
      .setFontWeight('bold').setFontSize(26).setHorizontalAlignment('center').setVerticalAlignment('middle');
  }
  dash.setRowHeight(5, 56);

  var green = '#34a853';
  var red = '#ea4335';
  var grey = '#9aa0a6';
  var blue = '#4285f4';
  var amber = '#fbbc04';

  addChart_(dash, tables, 'A2:D', Charts.ChartType.COLUMN, 'How each stage ends', 7, 1, { isStacked: true, colors: [green, red, grey] });
  addChart_(dash, tables, 'F2:G', Charts.ChartType.BAR, 'Minutes to clear a stage', 7, 8, { colors: [blue], legend: { position: 'none' } });
  addChart_(dash, tables, 'I2:J', Charts.ChartType.COLUMN, 'Lives lost per stage (average)', 23, 1, { colors: [red], legend: { position: 'none' } });
  addChart_(dash, tables, 'L2:M', Charts.ChartType.COLUMN, 'Ratings', 23, 8, { colors: [amber], legend: { position: 'none' } });
  addChart_(dash, tables, 'O2:P', Charts.ChartType.PIE, 'Launches by platform', 39, 1, { pieHole: 0.4 });
  addChart_(dash, tables, 'R2:S', Charts.ChartType.BAR, 'Frame rate by device (average)', 39, 8, { colors: [green], legend: { position: 'none' } });
  addChart_(dash, tables, 'U2:V', Charts.ChartType.BAR, 'Most drafted operators', 55, 1, { colors: [blue], legend: { position: 'none' } });
  addChart_(dash, tables, 'X2:Y', Charts.ChartType.BAR, 'Operator clear rate (3+ stages)', 55, 8, { colors: [green], legend: { position: 'none' } });
}

function addChart_(dash, tables, range, type, title, row, col, options) {
  var builder = dash.newChart()
    .setChartType(type)
    .addRange(tables.getRange(range))
    .setNumHeaders(1)
    .setPosition(row, col, 0, 0)
    .setOption('title', title)
    .setOption('width', 600)
    .setOption('height', 310)
    .setOption('legend', { position: 'bottom' });
  var keys = Object.keys(options || {});
  for (var i = 0; i < keys.length; i++) builder.setOption(keys[i], options[keys[i]]);
  dash.insertChart(builder.build());
}
