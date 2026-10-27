const fs = require('fs');
const path = require('path');

const ROOT = path.resolve(__dirname, '..');
const DETAILS = path.join(ROOT, 'docs', 'Task_Details.md');
const LIST = path.join(ROOT, 'docs', 'Task_List.md');
const DATE = /\d{4}-\d{2}-\d{2}/;

function slug(heading, seen) {
  let s = heading.toLowerCase().replace(/[^\w\- ]/g, '').replace(/ /g, '-');
  let n = seen[s] || 0;
  seen[s] = n + 1;
  return n === 0 ? s : `${s}-${n}`;
}

function parseStatus(line) {
  const text = line.trim();
  const m = text.match(/^- \[(.)\]\s*(.*)/);
  if (!m) return { status: 'unknown', by: '—', date: '—', tests: '—', note: text };
  const mark = m[1];
  const rest = m[2];
  let by = '—';
  let date = '—';
  let tests = '—';
  let note = '';

  if (mark === 'x') {
    const status = 'completed';
    const who = rest.match(/completed \(([^)]*)\)/);
    by = who ? who[1] : '—';
    const d = rest.match(/\d{4}-\d{2}-\d{2}/);
    date = d ? d[0] : '—';
    if (rest.includes('tests passed (owner)')) {
      const pd = rest.match(/tests passed \(owner\)\s*[—-]\s*(\d{4}-\d{2}-\d{2})/);
      tests = pd ? 'passed (owner) ' + pd[1] : 'passed (owner)';
    } else if (rest.includes('tests written, not run')) {
      tests = 'written, not run';
    } else if (rest.includes('design only')) {
      tests = 'design only';
    } else if (rest.includes('documentation only')) {
      tests = 'documentation only';
    } else if (rest.includes('no tests needed')) {
      tests = 'not needed';
    }
    const parts = rest.split(' · ').slice(1).map(p => p.trim());
    const keep = parts.filter(p => !p.match(/^(tests written, not run|design only, no code|documentation only, no tests)$/) && !p.match(/^\d{4}-\d{2}-\d{2}$/));
    note = keep.join(' · ');
    note = note.replace(/\s*[—-]\s*\d{4}-\d{2}-\d{2}$/, '');
    return { status, by, date, tests, note };
  } else if (mark === '~') {
    const status = 'working';
    const who = rest.match(/working \(([^)]*)\)/);
    by = who ? who[1] : '—';
    const d = rest.match(/\d{4}-\d{2}-\d{2}/);
    date = d ? d[0] : '—';
    return { status, by, date, tests, note };
  } else if (mark === '!') {
    const status = 'blocked';
    note = rest.replace(/^blocked\s*[—-]\s*/, '');
    return { status, by, date, tests, note };
  } else {
    return { status: 'open', by, date, tests, note };
  }
}

function clip(s, n = 160) {
  s = s.replace(/\|/g, '\\|').replace(/\n/g, ' ');
  return s.length <= n ? s : s.slice(0, n - 1).trimEnd() + '…';
}

function main() {
  const content = fs.readFileSync(DETAILS, 'utf8');
  const lines = content.split(/\r?\n/);
  const seen = {};
  let inCode = false;
  const cards = [];
  let currentAnchor = '';

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    if (line.startsWith('```')) {
      inCode = !inCode;
    }
    if (inCode) continue;

    if (line.startsWith('#')) {
      currentAnchor = slug(line.replace(/^#+/, '').trim(), seen);
    }

    const m = line.match(/^### (TK-(\d+)) · (.*)/);
    if (!m) continue;

    const anchor = currentAnchor;
    const body = [];
    for (let j = i + 1; j < lines.length; j++) {
      if (lines[j].startsWith('#')) break;
      body.push(lines[j]);
    }

    const { status, by, date, tests, note } = parseStatus(body[0] || '');
    const bodyText = body.join('\n');
    const grp = bodyText.match(/\*\*Group:\*\*\s*([^\n]*)/);
    const iss = bodyText.match(/\*\*Issue:\*\*\s*(?:\[)?#(\d+)/);

    cards.push({
      id: m[1],
      n: parseInt(m[2], 10),
      title: m[3].trim(),
      anchor,
      status,
      by,
      date,
      tests,
      note,
      group: grp ? grp[1].trim() : '—',
      issue: iss ? iss[1] : null,
    });
  }

  cards.sort((a, b) => a.n - b.n);

  const count = {
    completed: cards.filter(c => c.status === 'completed').length,
    working: cards.filter(c => c.status === 'working').length,
    open: cards.filter(c => c.status === 'open').length,
    blocked: cards.filter(c => c.status === 'blocked').length,
  };

  const icon = {
    completed: '✅ completed',
    working: '🔶 working',
    open: '🟪 open',
    blocked: '🚫 blocked',
  };

  const out = [
    '# Task list',
    '',
    'One row per task card: its status, who completed it and when. **This file is generated** from',
    '[`Task_Details.md`](./Task_Details.md) by `scripts/update-task-list.py`, and the pre-commit hook',
    'rewrites and stages it whenever the details file changes. **Never edit it by hand.** To change a',
    'status, edit the card\'s first line in the details file; the card there holds the rules, lanes,',
    'where to start, sub-tasks and notes.',
    '',
    `**Summary:** ${count.completed} completed, ${count.working} working, ${count.open} open, and ${count.blocked} blocked (${cards.length} cards).`,
    '',
    '| Card | Title | Group | Status | Completed by | Completed at | Tests | Issue | Note |',
    '| :--- | :--- | :--- | :---: | :--- | :--- | :--- | :---: | :--- |',
  ];

  for (const c of cards) {
    const by = c.status === 'completed' ? c.by : (c.status === 'working' ? c.by + ' (working)' : '—');
    const at = c.status === 'completed' ? c.date : (c.status === 'working' ? `since ${c.date}` : '—');
    const iss = c.issue ? `[#${c.issue}](https://github.com/jothi-prabaharan/Bill-Book/issues/${c.issue})` : '—';
    const grp = c.group !== '—' ? clip(c.group.split(' · ')[0], 40) : '—';
    out.push(`| [${c.id}](./Task_Details.md#${c.anchor}) | ${clip(c.title, 110)} | ${grp} | ${icon[c.status]} | ${clip(by, 60)} | ${at} | ${c.tests} | ${iss} | ${clip(c.note) || '—'} |`);
  }

  out.push('');
  fs.writeFileSync(LIST, out.join('\n'), 'utf8');
  console.log(`docs/Task_List.md: ${cards.length} cards (${JSON.stringify(count)})`);
}

main();
