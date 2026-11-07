"""Check concrete XAML risks in the desktop control-panel sources (stdlib only).

Run from any directory: python Tests/Regression/Test-PanelDesign.py
Use --report-only to print findings without failing, or --root for a source fixture.

Scope: Widget/Pages/Main, Settings, GameStyles, the update overlay, the settings
DanmakuOptionsPanel, and CompatibilityHost/Integration outside Validation. Runtime
game overlays, generated output, animation canvases and assets are not scanned.
Referenced dictionaries are parsed for resources, but their overlay-only controls
are not treated as desktop UI. UserControls must resolve application resources
through their own local/merged dictionaries; an unrelated host is not a fallback.

Checks: XML validity and merged dictionary availability; StaticResource closure;
explicit readable TextBlock FontSize below 12; explicit Height below the effective
MinHeight (including Style/BasedOn). ThemeResource and dynamic bindings remain the
platform/runtime's responsibility. This does not replace a WinUI build or visual QA.
"""

from __future__ import annotations

import argparse
from dataclasses import dataclass, field
import math
from pathlib import Path
import re
from urllib.parse import unquote, urlparse
from xml.parsers import expat


XAML = "http://schemas.microsoft.com/winfx/2006/xaml}"
STATIC = re.compile(r"\{StaticResource\s+(?:ResourceKey\s*=\s*)?([^{}]+)\}")
ICON_FONTS = ("segoe mdl2 assets", "segoe fluent icons", "segoe ui symbol", "wingdings", "webdings")
SCAN_DIRS = (
    "Widget/Pages/Main", "Widget/Controls/Settings", "Widget/Controls/GameStyles",
    "Widget/Controls/Overlays/Update", "CompatibilityHost/Integration",
)


def local(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def resource_key(value: str) -> str | None:
    match = STATIC.fullmatch(value.strip())
    return match.group(1).strip().strip("\"'") if match else None


@dataclass(eq=False)
class Node:
    tag: str
    attrs: dict[str, str]
    path: Path
    line: int
    parent: Node | None = None
    children: list[Node] = field(default_factory=list)
    text: str = ""

    def walk(self):
        yield self
        for child in self.children:
            yield from child.walk()

    @property
    def label(self) -> str:
        name = self.attrs.get(XAML + "Name") or self.attrs.get(XAML + "Key")
        return f"{local(self.tag)} {name}" if name else local(self.tag)


class Audit:
    def __init__(self, root: Path):
        self.root = root.resolve()
        self.documents: dict[Path, Node | None] = {}
        self.dictionaries: dict[Node, dict[str, Node]] = {}
        self.resolving: set[Node] = set()
        self.findings: set[tuple[str, int, str, str]] = set()

    def issue(self, node: Node, rule: str, message: str):
        self.findings.add((self.relative(node.path), node.line, rule, message))

    def relative(self, path: Path) -> str:
        try:
            return path.relative_to(self.root).as_posix()
        except ValueError:
            return str(path)

    def load(self, path: Path) -> Node | None:
        path = path.resolve()
        if path in self.documents:
            return self.documents[path]
        self.documents[path] = None
        parser = expat.ParserCreate(namespace_separator="}")
        stack: list[Node] = []
        top: list[Node] = []

        def start(tag, attrs):
            node = Node(tag, attrs, path, parser.CurrentLineNumber, stack[-1] if stack else None)
            (stack[-1].children if stack else top).append(node)
            stack.append(node)

        parser.StartElementHandler = start
        parser.EndElementHandler = lambda _tag: stack.pop()
        parser.CharacterDataHandler = lambda text: setattr(stack[-1], "text", stack[-1].text + text) if stack else None
        # No network or external entity expansion is needed to inspect source XAML.
        parser.ExternalEntityRefHandler = lambda *_args: 0
        try:
            parser.Parse(path.read_bytes(), True)
            self.documents[path] = top[0]
        except (OSError, expat.ExpatError) as error:
            self.findings.add((self.relative(path), getattr(error, "lineno", 1), "XML", str(error)))
        return self.documents[path]

    def source_path(self, source: str, owner: Node) -> Path | None:
        uri = urlparse(source)
        if uri.scheme == "ms-appx" and not uri.netloc:
            relative = unquote(uri.path).lstrip("/")
            if relative.startswith("Generated/"):
                relative = relative[len("Generated/"):]
            prefix = "Features/CompatibilityDisplay/Integration/"
            result = self.root / ("CompatibilityHost/Integration/" + relative[len(prefix):] if relative.startswith(prefix) else "Widget/" + relative)
        elif not uri.scheme and not uri.netloc:
            result = owner.path.parent / unquote(source)
        else:
            self.issue(owner, "RESOURCE_URI", f"Cannot resolve source dictionary URI: {source}")
            return None
        result = result.resolve()
        if not result.is_relative_to(self.root):
            self.issue(owner, "RESOURCE_URI", f"Dictionary escapes repository: {source}")
            return None
        return result

    def dictionary(self, node: Node) -> dict[str, Node]:
        if node in self.dictionaries:
            return self.dictionaries[node]
        if node in self.resolving:
            self.issue(node, "RESOURCE_CYCLE", "Merged dictionary cycle")
            return {}
        self.resolving.add(node)
        entries: dict[str, Node] = {}
        source = node.attrs.get("Source")
        if source:
            path = self.source_path(source, node)
            loaded = self.load(path) if path else None
            if loaded:
                entries.update(self.dictionary(loaded))
        # Later merged dictionaries override earlier ones; local entries win.
        for child in node.children:
            if local(child.tag) in ("ResourceDictionary.MergedDictionaries", "ResourceDictionary.ThemeDictionaries"):
                for dictionary in child.children:
                    entries.update(self.dictionary(dictionary))
            elif local(child.tag) == "ResourceDictionary" and not child.attrs.get(XAML + "Key"):
                entries.update(self.dictionary(child))
        for child in node.children:
            key = child.attrs.get(XAML + "Key")
            if not key and local(child.tag) == "Style":
                key = "@type:" + child.attrs.get("TargetType", "")
            if key:
                entries[key] = child
        self.resolving.remove(node)
        self.dictionaries[node] = entries
        return entries

    def lookup(self, key: str, node: Node) -> Node | None:
        current: Node | None = node
        while current:
            if local(current.tag) == "ResourceDictionary" or local(current.tag).endswith(".Resources"):
                found = self.dictionary(current).get(key)
                if found:
                    return found
            for child in current.children:
                if local(child.tag).endswith(".Resources"):
                    found = self.dictionary(child).get(key)
                    if found:
                        return found
            current = current.parent
        return None

    def scalar(self, value: str, owner: Node, visited: set[Node] | None = None) -> str | None:
        key = resource_key(value)
        if not key:
            return None if value.strip().startswith("{") else value.strip()
        resource = self.lookup(key, owner)
        if not resource:
            return None
        visited = set() if visited is None else set(visited)
        if resource in visited:
            self.issue(owner, "RESOURCE_CYCLE", f"Scalar resource cycle at {key}")
            return None
        visited.add(resource)
        return self.scalar(resource.text.strip(), resource, visited)

    def numeric(self, value: str, owner: Node) -> float | None:
        try:
            result = float(self.scalar(value, owner) or "nan")
            return result if math.isfinite(result) else None
        except ValueError:
            return None

    def style_property(self, style: Node, property_name: str, visited: set[Node] | None = None) -> tuple[str, Node] | None:
        visited = set() if visited is None else set(visited)
        if style in visited:
            self.issue(style, "STYLE_CYCLE", "Style BasedOn cycle")
            return None
        visited.add(style)
        for child in reversed(style.children):
            if local(child.tag) == "Setter" and child.attrs.get("Property") == property_name and "Value" in child.attrs:
                return child.attrs["Value"], child
        key = resource_key(style.attrs.get("BasedOn", ""))
        base = self.lookup(key, style) if key else None
        return self.style_property(base, property_name, visited) if base else None

    def property(self, node: Node, name: str) -> tuple[str, Node] | None:
        if name in node.attrs:
            return node.attrs[name], node
        key = resource_key(node.attrs.get("Style", ""))
        style = self.lookup(key, node) if key else None
        if not style:
            for child in node.children:
                if local(child.tag).endswith(".Style") and child.children:
                    style = child.children[0]
                    break
        if not style and "Style" not in node.attrs:
            style = self.lookup("@type:" + local(node.tag), node)
        return self.style_property(style, name) if style else None

    def is_icon_text(self, node: Node) -> bool:
        current: Node | None = node
        while current:
            value = self.property(current, "FontFamily")
            if value:
                family = self.scalar(*value)
                return bool(family and any(font in family.casefold() for font in ICON_FONTS))
            current = current.parent
        return False

    def check_references(self, node: Node):
        for value in [*node.attrs.values(), node.text]:
            for match in STATIC.finditer(value):
                key = match.group(1).strip().strip("\"'")
                if not self.lookup(key, node):
                    self.issue(node, "RESOURCE", f"{node.label}: StaticResource '{key}' has no local/merged definition")
        if local(node.tag) == "ResourceDictionary":
            self.dictionary(node)

    def check_metrics(self, node: Node):
        if local(node.tag) == "TextBlock" and "FontSize" in node.attrs:
            size = self.numeric(node.attrs["FontSize"], node)
            if size is not None and size < 12 and not self.is_icon_text(node):
                self.issue(node, "TEXT_SIZE", f"{node.label}: readable explicit FontSize {size:g} is below 12")
        if "Height" in node.attrs:
            height = self.numeric(node.attrs["Height"], node)
            minimum_value = self.property(node, "MinHeight")
            minimum = self.numeric(*minimum_value) if minimum_value else None
            if height is not None and minimum is not None and height < minimum:
                self.issue(node, "HEIGHT", f"{node.label}: explicit Height {height:g} is below effective MinHeight {minimum:g}")

    def run(self) -> int:
        paths = set()
        for relative in SCAN_DIRS:
            paths.update(p for p in (self.root / relative).rglob("*.xaml") if "Validation" not in p.relative_to(self.root).parts)
        danmaku = self.root / "Widget/Danmaku/DanmakuOptionsPanel.xaml"
        if danmaku.exists():
            paths.add(danmaku)
        if not paths:
            self.findings.add((".", 1, "SCOPE", "No control-panel source XAML found"))
        for path in sorted(paths):
            root = self.load(path)
            if root:
                for node in root.walk():
                    self.check_references(node)
                    self.check_metrics(node)
        # Imported resource definitions need their own transitive closure too.
        checked = set(paths)
        while remaining := set(self.documents) - checked:
            for path in sorted(remaining):
                checked.add(path)
                root = self.documents[path]
                if root:
                    for node in root.walk():
                        self.check_references(node)
        return len(paths)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2], help="Repository source root")
    parser.add_argument("--report-only", action="store_true", help="Print findings but return success for an initial audit")
    args = parser.parse_args()
    audit = Audit(args.root)
    count = audit.run()
    for path, line, rule, message in sorted(audit.findings):
        print(f"{path}:{line}: {rule}: {message}")
    status = "FAIL" if audit.findings else "PASS"
    print(f"{status}: {count} control-panel XAML files; {len(audit.documents)} source/dictionary files parsed; {len(audit.findings)} findings.")
    return 1 if audit.findings and not args.report_only else 0


if __name__ == "__main__":
    raise SystemExit(main())
