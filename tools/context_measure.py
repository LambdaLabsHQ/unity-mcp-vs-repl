#!/usr/bin/env python3
import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
RESULTS = ROOT / "results"
RESULTS.mkdir(exist_ok=True)


def measure_file(label, path):
    path = Path(path)
    if not path.exists():
        return {
            "label": label,
            "path": str(path),
            "exists": False,
        }
    text = path.read_text(errors="ignore")
    return {
        "label": label,
        "path": str(path),
        "exists": True,
        "chars": len(text),
        "words": len(text.split()),
        "approx_tokens_chars_div_4": round(len(text) / 4),
        "lines": text.count("\n") + 1,
    }


def coplay_tools_from_readme(readme_path):
    text = Path(readme_path).read_text(errors="ignore")
    match = re.search(r"### Available Tools\n(.+?)\n### Available Resources", text, re.S)
    if not match:
        return []
    return re.findall(r"`([^`]+)`", match.group(1))


def coplay_csharp_tool_attrs(tools_dir):
    attrs = []
    tools_dir = Path(tools_dir)
    for path in tools_dir.rglob("*.cs"):
        text = path.read_text(errors="ignore")
        for match in re.finditer(r'\[McpForUnityTool\(\s*"([^"]+)"', text):
            attrs.append({
                "name": match.group(1),
                "file": str(path.relative_to(tools_dir)),
            })
    return sorted(attrs, key=lambda item: (item["name"], item["file"]))


def main():
    repl_root = Path("/tmp/lambdalabs-unity-repl")
    coplay_root = Path("/tmp/coplay-unity-mcp")
    files = [
        measure_file("unity-repl skill", repl_root / ".agents/skills/unity-repl/SKILL.md"),
        measure_file("unity-repl README", repl_root / "README.md"),
        measure_file("coplay MCP skill", coplay_root / "unity-mcp-skill/SKILL.md"),
        measure_file("coplay tools reference", coplay_root / "unity-mcp-skill/references/tools-reference.md"),
        measure_file("coplay resources reference", coplay_root / "unity-mcp-skill/references/resources-reference.md"),
        measure_file("coplay workflows reference", coplay_root / "unity-mcp-skill/references/workflows.md"),
        measure_file("coplay README", coplay_root / "README.md"),
    ]

    coplay_tools = []
    coplay_attrs = []
    if (coplay_root / "README.md").exists():
        coplay_tools = coplay_tools_from_readme(coplay_root / "README.md")
    if (coplay_root / "MCPForUnity/Editor/Tools").exists():
        coplay_attrs = coplay_csharp_tool_attrs(coplay_root / "MCPForUnity/Editor/Tools")

    payload = {
        "method": "Approx tokens are chars/4. This is a coarse proxy for tool-reference context footprint, not tokenizer-exact accounting.",
        "files": files,
        "coplay_readme_tool_count": len(coplay_tools),
        "coplay_readme_tools": coplay_tools,
        "coplay_csharp_tool_attribute_count": len(coplay_attrs),
        "coplay_csharp_tool_attributes": coplay_attrs,
        "official_unity_ai_note": (
            "Unity AI official MCP tool schemas are not publicly available as a local file in this environment. "
            "The local machine also has no ~/.unity/relay directory, so official MCP runtime was not measurable here."
        ),
    }

    (RESULTS / "context_measure.json").write_text(json.dumps(payload, indent=2))

    print(json.dumps(payload, indent=2))


if __name__ == "__main__":
    main()
