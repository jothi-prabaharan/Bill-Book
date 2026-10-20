#!/usr/bin/env python3
"""Builds docs/Task_List.md from the cards in docs/Task_Details.md.

The details file is the source of truth: a card's status line, title, group and issue are read
from it. Run this after editing a card (the pre-commit hook does it for you and stages the
result). Task_List.md is never edited by hand.

    python3 scripts/update-task-list.py          # rewrite docs/Task_List.md
    python3 scripts/update-task-list.py --check  # exit 1 if it is out of date
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DETAILS = ROOT / "docs" / "Task_Details.md"
LIST = ROOT / "docs" / "Task_List.md"
DATE = r"\d{4}-\d{2}-\d{2}"


def slug(heading, seen):
    s = re.sub(r"[^\w\- ]", "", heading.lower()).replace(" ", "-")
    n = seen.get(s, 0)
    seen[s] = n + 1
    return s if n == 0 else f"{s}-{n}"


def parse_status(line):
    """Returns (status, by, date, tests, note) from a card's first line."""
    text = line.strip()
    m = re.match(r"- \[(.)\]\s*(.*)", text)
    if not m:
        return "unknown", "—", "—", "—", text
    mark, rest = m.group(1), m.group(2)
    by = date = "—"
    tests = "—"
    note = ""
    if mark == "x":
        status = "completed"
        who = re.search(r"completed \(([^)]*)\)", rest)
        by = who.group(1) if who else "—"
        d = re.search(DATE, rest)
        date = d.group(0) if d else "—"
        if "tests passed (owner)" in rest:
            pd = re.search(r"tests passed \(owner\)\s*[—-]\s*(" + DATE + ")", rest)
            tests = "passed (owner) " + pd.group(1) if pd else "passed (owner)"
        elif "tests written, not run" in rest:
            tests = "written, not run"
        elif "design only" in rest:
            tests = "design only"
        elif "documentation only" in rest:
            tests = "documentation only"
        elif "no tests needed" in rest:
            tests = "not needed"
        # whatever follows the first " · " that is not the test marker is worth keeping
        parts = [p.strip() for p in rest.split(" · ")[1:]]
        keep = [p for p in parts if not re.match(
            r"(tests written, not run|design only, no code|documentation only, no tests)", p)
            and not re.fullmatch(DATE, p)]
        note = " · ".join(keep)
        # "completed (X) · tests written, not run — DATE": strip the date left on the test part
        note = re.sub(r"\s*[—-]\s*" + DATE + "$", "", note)
    elif mark == "~":
        status = "working"
        who = re.search(r"working \(([^)]*)\)", rest)
        by = who.group(1) if who else "—"
        d = re.search(DATE, rest)
        date = d.group(0) if d else "—"
    elif mark == "!":
        status = "blocked"
        note = re.sub(r"^blocked\s*[—-]\s*", "", rest)
    else:
        status = "open"
    return status, by, date, tests, note


def clip(s, n=160):
    s = s.replace("|", "\\|").replace("\n", " ")
    return s if len(s) <= n else s[: n - 1].rstrip() + "…"


def main():
    lines = DETAILS.read_text(encoding="utf-8").split("\n")
    seen = {}
    in_code = False
    cards = []
    for i, line in enumerate(lines):
        if line.startswith("```"):
            in_code = not in_code
        if in_code:
            continue
        if line.startswith("#"):
            a = slug(line.lstrip("#").strip(), seen)
        m = re.match(r"### (TK-(\d+)) · (.*)", line)
        if not m:
            continue
        anchor = a
        body = []
        for nxt in lines[i + 1:]:
            if nxt.startswith("#"):
                break
            body.append(nxt)
        status, by, date, tests, note = parse_status(body[0] if body else "")
        grp = re.search(r"\*\*Group:\*\*\s*([^\n]*)", "\n".join(body))
        iss = re.search(r"\*\*Issue:\*\*\s*(?:\[)?#(\d+)", "\n".join(body))
        cards.append(dict(id=m.group(1), n=int(m.group(2)), title=m.group(3).strip(),
                          anchor=anchor, status=status, by=by, date=date, tests=tests,
                          note=note, group=(grp.group(1).strip() if grp else "—"),
                          issue=iss.group(1) if iss else None))
    cards.sort(key=lambda c: c["n"])

    count = {k: sum(1 for c in cards if c["status"] == k) for k in ("completed", "working", "open", "blocked")}
    icon = {"completed": "✅ completed", "working": "🔶 working", "open": "🟪 open", "blocked": "🚫 blocked"}

    out = [
        "# Task list",
        "",
        "One row per task card: its status, who completed it and when. **This file is generated** from",
        "[`Task_Details.md`](./Task_Details.md) by `scripts/update-task-list.py`, and the pre-commit hook",
        "rewrites and stages it whenever the details file changes. **Never edit it by hand.** To change a",
        "status, edit the card's first line in the details file; the card there holds the rules, lanes,",
        "where to start, sub-tasks and notes.",
        "",
        f"**Summary:** {count['completed']} completed, {count['working']} working, "
        f"{count['open']} open, and {count['blocked']} blocked ({len(cards)} cards).",
        "",
        "| Card | Title | Group | Status | Completed by | Completed at | Tests | Issue | Note |",
        "| :--- | :--- | :--- | :---: | :--- | :--- | :--- | :---: | :--- |",
    ]
    for c in cards:
        by = c["by"] if c["status"] == "completed" else ("—" if c["status"] != "working" else c["by"] + " (working)")
        at = c["date"] if c["status"] == "completed" else (f"since {c['date']}" if c["status"] == "working" else "—")
        iss = f"[#{c['issue']}](https://github.com/jothi-prabaharan/Bill-Book/issues/{c['issue']})" if c["issue"] else "—"
        grp = clip(c["group"].split(" · ")[0], 40) if c["group"] != "—" else "—"
        out.append("| [{id}](./Task_Details.md#{a}) | {t} | {g} | {s} | {by} | {at} | {tests} | {iss} | {note} |".format(
            id=c["id"], a=c["anchor"], t=clip(c["title"], 110), g=grp, s=icon[c["status"]],
            by=clip(by, 60), at=at, tests=c["tests"], iss=iss, note=clip(c["note"]) or "—"))
    out.append("")
    text = "\n".join(out)

    if "--check" in sys.argv:
        current = LIST.read_text(encoding="utf-8") if LIST.exists() else ""
        if current != text:
            print("docs/Task_List.md is out of date: run python3 scripts/update-task-list.py", file=sys.stderr)
            sys.exit(1)
        return
    LIST.write_text(text, encoding="utf-8")
    print(f"docs/Task_List.md: {len(cards)} cards ({count})")


main()
