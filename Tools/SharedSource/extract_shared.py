"""Extract code duplicated between Widget/ and CompatibilityHost/Renderer/ into Shared/.

Each shared file is compiled by both hosts. Lines that differ between the two
copies are kept verbatim per host inside `#if WINDOWS_UWP` / `#else` blocks
(Widget defines WINDOWS_UWP; CompatibilityHost does not), so every host keeps
compiling exactly the code it compiled before. Later cleanup can collapse
blocks that only differ by an outdated fix.

  python Tools/SharedSource/extract_shared.py inventory
  python Tools/SharedSource/extract_shared.py apply --max-diff-ratio 0.4 [--dry-run]

Inventory: Output/SharedSource/inventory.json. Files are left host-specific when
they are XAML code-behind, missing from one side, absent from the Widget
project, or differ in more than --max-diff-ratio of their lines.
"""
import argparse
import difflib
import json
import re
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RENDERER = ROOT / "CompatibilityHost" / "Renderer"
WIDGET = ROOT / "Widget"
SHARED = ROOT / "Shared"
WIDGET_PROJECT = WIDGET / "KillConfirmGameBar.csproj"
OUT = ROOT / "Output" / "SharedSource"
BACKUP = OUT / ("backup-" + time.strftime("%Y%m%d-%H%M%S"))
BOM = b"\xef\xbb\xbf"


def read(path):
    raw = path.read_bytes()
    bom = raw.startswith(BOM)
    text = raw[3:].decode("utf-8") if bom else raw.decode("utf-8")
    # Working copies can mix CRLF and LF; split on both, write the dominant one.
    crlf = text.count("\r\n")
    newline = "\r\n" if crlf and crlf >= text.count("\n") - crlf else "\n"
    lines = re.split(r"\r?\n", text)
    return lines, newline, bom


DIRECTIVE = re.compile(r"^\s*#\s*(if|elif|else|endif)\b")


class UnsafeMerge(Exception):
    """A differing hunk touches an original preprocessor block."""


def depths(lines):
    """Preprocessor nesting depth before each line (and after the last)."""
    result, depth = [], 0
    for line in lines:
        result.append(depth)
        match = DIRECTIVE.match(line)
        if match and match.group(1) == "if":
            depth += 1
        elif match and match.group(1) == "endif":
            depth -= 1
    result.append(depth)
    return result


def normalized(lines, root_namespace):
    return [line.rstrip("\r").replace(root_namespace, "@NS@") for line in lines]


def widget_compile_items():
    text = WIDGET_PROJECT.read_text(encoding="utf-8-sig")
    return set(m.group(1) for m in re.finditer(r'<Compile Include="([^"]+)"\s*/>', text))


def classify():
    items = widget_compile_items()
    rows = []
    for compat in sorted(RENDERER.rglob("*.cs")):
        rel = compat.relative_to(RENDERER)
        widget = WIDGET / rel
        row = {"path": rel.as_posix(), "status": "", "diff_lines": 0, "lines": 0}
        if rel.name.endswith(".xaml.cs"):
            row["status"] = "host_specific_xaml"
        elif not widget.exists():
            row["status"] = "compat_only"
        elif str(rel).replace("/", "\\") not in items:
            row["status"] = "not_in_widget_project"
        else:
            w_lines, _, _ = read(widget)
            c_lines, _, _ = read(compat)
            a = normalized(w_lines, "KillConfirmGameBar")
            b = normalized(c_lines, "KillConfirmCompatibility")
            matcher = difflib.SequenceMatcher(a=a, b=b, autojunk=False)
            changed = sum(max(i2 - i1, j2 - j1) for tag, i1, i2, j1, j2 in matcher.get_opcodes() if tag != "equal")
            row["lines"] = max(len(a), len(b))
            row["diff_lines"] = changed
            row["status"] = "namespace_only" if changed == 0 else "drifted"
        rows.append(row)
    return rows


def merge(widget_lines, compat_lines):
    """Union both copies; differing hunks become per-host preprocessor branches.

    Raises UnsafeMerge when a differing hunk contains or sits inside an original
    #if block: inserted branches would interleave with it and change meaning.
    """
    a = [line.rstrip("\r") for line in widget_lines]
    b = [line.rstrip("\r") for line in compat_lines]
    depth_a, depth_b = depths(a), depths(b)
    out = []
    matcher = difflib.SequenceMatcher(a=a, b=b, autojunk=False)
    for tag, i1, i2, j1, j2 in matcher.get_opcodes():
        if tag == "equal":
            out.extend(a[i1:i2])
            continue
        if depth_a[i1] or depth_b[j1] or any(DIRECTIVE.match(line) for line in a[i1:i2] + b[j1:j2]):
            raise UnsafeMerge()
        out.append("#if WINDOWS_UWP")
        out.extend(a[i1:i2])
        if j2 > j1:
            out.append("#else")
            out.extend(b[j1:j2])
        out.append("#endif")
    return out


def header(rel):
    return [
        "// Shared by Widget (UWP) and CompatibilityHost; extracted from both former copies.",
        "// Per-host lines stay inside #if WINDOWS_UWP blocks. See Tools/SharedSource/extract_shared.py.",
    ]


def apply(rows, max_ratio, dry_run):
    project_text = WIDGET_PROJECT.read_bytes()
    project_bom = project_text.startswith(BOM)
    project = (project_text[3:] if project_bom else project_text).decode("utf-8")
    moved = []
    for row in rows:
        if row["status"] not in ("namespace_only", "drifted"):
            continue
        ratio = row["diff_lines"] / max(1, row["lines"])
        if row["status"] == "drifted" and ratio > max_ratio:
            row["status"] = "host_specific_diverged"
            continue
        rel = Path(row["path"])
        widget = WIDGET / rel
        compat = RENDERER / rel
        w_lines, newline, bom = read(widget)
        c_lines, _, _ = read(compat)
        try:
            merged = header(rel) + merge(w_lines, c_lines)
        except UnsafeMerge:
            row["status"] = "host_specific_preprocessor"
            continue
        include = "Include=\"" + str(rel).replace("/", "\\") + "\""
        old_item = "<Compile " + include + " />"
        if old_item not in project:
            raise SystemExit("Widget project item not found: " + old_item)
        new_item = ("<Compile Include=\"..\\Shared\\" + str(rel).replace("/", "\\")
                    + "\" Link=\"" + str(rel).replace("/", "\\") + "\" />")
        project = project.replace(old_item, new_item, 1)
        target = SHARED / rel
        row["status"] = "extracted"
        row["preprocessor_blocks"] = sum(1 for line in merged if line == "#if WINDOWS_UWP")
        moved.append(row)
        if dry_run:
            continue
        # Exact working copies, so a batch can always be rolled back.
        for source, side in ((widget, "Widget"), (compat, "Renderer")):
            backup = BACKUP / side / rel
            backup.parent.mkdir(parents=True, exist_ok=True)
            backup.write_bytes(source.read_bytes())
        target.parent.mkdir(parents=True, exist_ok=True)
        data = newline.join(merged).encode("utf-8")
        target.write_bytes((BOM if bom else b"") + data)
        widget.unlink()
        compat.unlink()
    if not dry_run:
        WIDGET_PROJECT.write_bytes((BOM if project_bom else b"") + project.encode("utf-8"))
    return moved


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("mode", choices=["inventory", "apply"])
    parser.add_argument("--max-diff-ratio", type=float, default=0.4)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--only", help="regex on relative path to limit apply")
    args = parser.parse_args()
    rows = classify()
    if args.only:
        rows = [row for row in rows if re.search(args.only, row["path"])]
    if args.mode == "apply":
        moved = apply(rows, args.max_diff_ratio, args.dry_run)
        print(("would extract " if args.dry_run else "extracted ") + str(len(moved)) + " files")
    OUT.mkdir(parents=True, exist_ok=True)
    summary = {}
    for row in rows:
        summary[row["status"]] = summary.get(row["status"], 0) + 1
    (OUT / ("inventory-dry-run.json" if args.dry_run else "inventory.json")).write_text(
        json.dumps({"summary": summary, "files": rows}, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(summary, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    sys.exit(main())
