#!/usr/bin/env python3
"""Restore Menu 3D prefab pause panel on Level1/3/4 to match Level2."""
from __future__ import annotations

import re
import sys
from collections import deque
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SCENES = [
    ROOT / "Assets" / "_Scenes" / "Level1.unity",
    ROOT / "Assets" / "_Scenes" / "Level3.unity",
    ROOT / "Assets" / "_Scenes" / "Level4.unity",
]

CANVAS_GUID = "7135b71d079e09345a61f0a94d870493"
UI_MANAGER_GUID = "1c0df30cd27710240931e8cee3eaa949"
CANVAS_INSTANCE_ID = "6492225691611727013"
PAUSE_REMOVED = f"{{fileID: 5098315453134981279, guid: {CANVAS_GUID}, type: 3}}"

PAUSE_ANIM_STRIPPED = """--- !u!95 &1164040838 stripped
Animator:
  m_CorrespondingSourceObject: {fileID: 5586925435619079409, guid: 7135b71d079e09345a61f0a94d870493, type: 3}
  m_PrefabInstance: {fileID: 6492225691611727013}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
"""

PAUSE_SIZE_OVERRIDES = """
    - target: {fileID: 5098315453134981279, guid: 7135b71d079e09345a61f0a94d870493, type: 3}
      propertyPath: m_IsActive
      value: 0
      objectReference: {fileID: 0}
    - target: {fileID: 7160744394460488682, guid: 7135b71d079e09345a61f0a94d870493, type: 3}
      propertyPath: m_SizeDelta.x
      value: 400
      objectReference: {fileID: 0}
    - target: {fileID: 7160744394460488682, guid: 7135b71d079e09345a61f0a94d870493, type: 3}
      propertyPath: m_SizeDelta.y
      value: 600
      objectReference: {fileID: 0}"""

LEGACY_ANIM_IDS = {
    "Level1.unity": "1696205298",
    "Level3.unity": "2028082937",
    "Level4.unity": "676089673",
}

LEGACY_RECT_IDS = {
    "Level1.unity": "1696205295",
    "Level3.unity": "2028082934",
    "Level4.unity": "676089670",
}


def split_yaml_documents(text: str) -> list[str]:
    parts = re.split(r"(?=^--- !u!)", text, flags=re.MULTILINE)
    return [p for p in parts if p.strip()]


def parse_doc_header(doc: str) -> tuple[str, str] | None:
    m = re.match(r"^--- !u!(\d+) &(\d+)", doc)
    if not m:
        return None
    return m.group(1), m.group(2)


def refs_in_doc(doc: str) -> set[str]:
    return {m for m in re.findall(r"\{fileID: (\d+)\}", doc) if m != "0"}


def find_legacy_pause_root_id(docs: list[str]) -> str | None:
    for doc in docs:
        m = re.match(r"^--- !u!1 &(\d+)", doc)
        if not m:
            continue
        if not re.search(r"^  m_Name: Pause Screen\s*$", doc, re.MULTILINE):
            continue
        if not re.search(r"^  m_CorrespondingSourceObject: \{fileID: 0\}", doc, re.MULTILINE):
            continue
        if not re.search(r"^  m_PrefabInstance: \{fileID: 0\}", doc, re.MULTILINE):
            continue
        return m.group(1)
    return None


def collect_legacy_pause_ids(docs: list[str], root_go_id: str) -> set[str]:
    by_id: dict[str, str] = {}
    by_game_object: dict[str, list[str]] = {}
    for doc in docs:
        header = parse_doc_header(doc)
        if not header:
            continue
        _, doc_id = header
        by_id[doc_id] = doc
        go_match = re.search(r"m_GameObject: \{fileID: (\d+)\}", doc)
        if go_match:
            by_game_object.setdefault(go_match.group(1), []).append(doc_id)

    def child_ids(doc: str) -> set[str]:
        ids: set[str] = set()
        for match in re.finditer(r"m_Children:\n((?:  - \{fileID: \d+\}\n)*)", doc):
            ids.update(re.findall(r"\{fileID: (\d+)\}", match.group(1)))
        for match in re.finditer(r"  - component: \{fileID: (\d+)\}", doc):
            ids.add(match.group(1))
        return ids

    queue: deque[str] = deque([root_go_id])
    seen: set[str] = set()
    while queue:
        current = queue.popleft()
        if current in seen:
            continue
        seen.add(current)
        for doc_id in by_game_object.get(current, []):
            if doc_id not in seen:
                queue.append(doc_id)
        if current in by_id:
            for ref in child_ids(by_id[current]):
                if ref not in seen:
                    queue.append(ref)
    return seen


def remove_corrupt_pause_overrides(text: str) -> str:
    """Remove pause overrides accidentally inserted into non-canvas prefab instances."""
    corrupt = (
        r"\n\n    - target: \{fileID: 5098315453134981279, guid: "
        + CANVAS_GUID
        + r", type: 3\}\n      propertyPath: m_IsActive\n      value: 0\n      objectReference: \{fileID: 0\}\n"
        r"    - target: \{fileID: 7160744394460488682, guid: "
        + CANVAS_GUID
        + r", type: 3\}\n      propertyPath: m_SizeDelta\.x\n      value: 400\n      objectReference: \{fileID: 0\}\n"
        r"    - target: \{fileID: 7160744394460488682, guid: "
        + CANVAS_GUID
        + r", type: 3\}\n      propertyPath: m_SizeDelta\.y\n      value: 600\n      objectReference: \{fileID: 0\}\n"
        r"    m_AddedGameObjects: \[\]\n    m_AddedComponents: \[\]\n  m_SourcePrefab: \{fileID: 100100000, guid: (?!7135b71d)"
    )
    return re.sub(corrupt, r"\n    m_AddedGameObjects: []\n    m_AddedComponents: []\n  m_SourcePrefab: {fileID: 100100000, guid: ", text)


def extract_canvas_ui_block(text: str) -> str:
    """Extract CanvasInGameUI prefab instance and related stripped docs from a scene."""
    docs = split_yaml_documents(text)
    keep: list[str] = []
    for doc in docs:
        header = parse_doc_header(doc)
        if header and header[1] == CANVAS_INSTANCE_ID:
            keep.append(doc)
            continue
        if f"m_PrefabInstance: {{fileID: {CANVAS_INSTANCE_ID}}}" in doc:
            keep.append(doc)
    return "".join(keep)


def fix_canvas_prefab_instance(text: str, legacy_rect_id: str) -> str:
    marker = f"--- !u!1001 &{CANVAS_INSTANCE_ID}\n"
    start = text.find(marker)
    if start == -1:
        return text

    end = text.find("\n--- !u!", start + len(marker))
    if end == -1:
        end = len(text)
    block = text[start:end]

    removed_block = (
        "    m_RemovedComponents: []\n"
        "    m_RemovedGameObjects:\n"
        f"    - {PAUSE_REMOVED}\n"
    )
    block = block.replace(
        removed_block,
        "    m_RemovedComponents: []\n    m_RemovedGameObjects: []\n",
    )

    added_entry = (
        "    - targetCorrespondingSourceObject: {fileID: 7818137013894652087, guid: "
        + CANVAS_GUID
        + ", type: 3}\n"
        "      insertIndex: -1\n"
        f"      addedObject: {{fileID: {legacy_rect_id}}}\n"
    )
    block = block.replace(added_entry, "")

    if f"7160744394460488682, guid: {CANVAS_GUID}" not in block:
        block = block.replace(
            "    m_RemovedComponents: []",
            PAUSE_SIZE_OVERRIDES + "\n    m_RemovedComponents: []",
            1,
        )

    return text[:start] + block + text[end:]


def fix_ui_manager(text: str, legacy_anim_id: str) -> str:
    text = re.sub(
        r"(- target: \{fileID: 6816893148955959799, guid: "
        + UI_MANAGER_GUID
        + r", type: 3\}\n      propertyPath: 'panels\.Array\.data\[0\]'\n      value: \n      objectReference: )\{fileID: 0\}",
        r"\g<1>{fileID: 1164040838}",
        text,
    )
    text = text.replace(
        f"objectReference: {{fileID: {legacy_anim_id}}}",
        "objectReference: {fileID: 1164040838}",
    )
    if "1164040838 stripped" not in text:
        for anchor in (
            "--- !u!224 &6492225691611727014 stripped",
            "--- !u!224 &6492225691611727015 stripped",
        ):
            if anchor in text:
                text = text.replace(anchor, PAUSE_ANIM_STRIPPED + anchor, 1)
                break
    return text


def fix_canvas_scaler(text: str) -> str:
    return re.sub(
        r"(target: \{fileID: 2177781866408772089, guid: "
        + CANVAS_GUID
        + r", type: 3\}\n      propertyPath: m_MatchWidthOrHeight\n      value: )1(\n)",
        r"\g<1>0.5\2",
        text,
    )


def find_orphan_legacy_pause_root_ids(docs: list[str]) -> set[str]:
    """Find scene-root Title ('Paused') and Unpause Button objects left from legacy pause UI."""
    orphan_go_ids: set[str] = set()
    scene_root_go_ids: set[str] = set()
    go_names: dict[str, str] = {}
    go_components: dict[str, list[str]] = {}
    tmp_text_by_id: dict[str, str] = {}

    for doc in docs:
        header = parse_doc_header(doc)
        if not header:
            continue
        type_id, doc_id = header

        if type_id == "1":
            m = re.search(r"^  m_Name: (.+)\s*$", doc, re.MULTILINE)
            if m:
                go_names[doc_id] = m.group(1)
            if re.search(r"^  m_PrefabInstance: \{fileID: 0\}", doc, re.MULTILINE):
                go_components.setdefault(doc_id, [])
        elif type_id == "224":
            if not re.search(r"^  m_Father: \{fileID: 0\}", doc, re.MULTILINE):
                continue
            go_match = re.search(r"m_GameObject: \{fileID: (\d+)\}", doc)
            if go_match:
                scene_root_go_ids.add(go_match.group(1))
        elif type_id == "114":
            go_match = re.search(r"m_GameObject: \{fileID: (\d+)\}", doc)
            text_match = re.search(r"^  m_text: (.+)\s*$", doc, re.MULTILINE)
            if go_match and text_match:
                tmp_text_by_id[doc_id] = text_match.group(1)
                go_components.setdefault(go_match.group(1), []).append(doc_id)

    for go_id in scene_root_go_ids:
        if go_id not in go_names:
            continue
        name = go_names[go_id]
        if "Unpause" in name:
            orphan_go_ids.add(go_id)
            continue
        if name != "Title":
            continue
        for comp_id in go_components.get(go_id, []):
            if tmp_text_by_id.get(comp_id) == "Paused":
                orphan_go_ids.add(go_id)
                break

    all_ids: set[str] = set()
    for go_id in orphan_go_ids:
        all_ids.update(collect_legacy_pause_ids(docs, go_id))
    return all_ids


def remove_from_scene_roots(text: str, removed_rect_ids: set[str]) -> str:
    if not removed_rect_ids:
        return text

    def strip_root(match: re.Match[str]) -> str:
        block = match.group(0)
        for rect_id in removed_rect_ids:
            block = re.sub(rf"  - {{fileID: {rect_id}}}\n", "", block)
        return block

    return re.sub(
        r"--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_Roots:\n(?:  - \{fileID: \d+\}\n)+",
        strip_root,
        text,
        count=1,
    )


def fix_backdrop_override(text: str) -> str:
    for old_x, new_x in (("400", "800"), ("500", "800")):
        text = re.sub(
            r"(target: \{fileID: 2368212640117177249, guid: "
            + CANVAS_GUID
            + rf", type: 3\}}\n      propertyPath: m_SizeDelta\.x\n      value: ){old_x}(\n)",
            rf"\g<1>{new_x}\2",
            text,
        )
    for old_y, new_y in (("600", "1300"), ("700", "1300")):
        text = re.sub(
            r"(target: \{fileID: 2368212640117177249, guid: "
            + CANVAS_GUID
            + rf", type: 3\}}\n      propertyPath: m_SizeDelta\.y\n      value: ){old_y}(\n)",
            rf"\g<1>{new_y}\2",
            text,
        )
    return text


def fix_ui_manager_panels(text: str) -> str:
    return re.sub(
        r"\n    - target: \{fileID: 6816893148955959799, guid: "
        + UI_MANAGER_GUID
        + r", type: 3\}\n      propertyPath: 'panels\.Array\.data\[3\]'\n      value: \n      objectReference: \{fileID: \d+\}",
        "",
        text,
    )


def ensure_canvas_instance(text: str, donor_text: str) -> str:
    if f"--- !u!1001 &{CANVAS_INSTANCE_ID}\n" in text:
        return text
    ui_block = extract_canvas_ui_block(donor_text)
    if not ui_block:
        print("  ERROR: Could not extract CanvasInGameUI block from donor scene", file=sys.stderr)
        return text
    anchor = "--- !u!1660057539 &9223372036854775807\nSceneRoots:"
    if anchor in text:
        return text.replace(anchor, ui_block + anchor)
    return text + ui_block


def fix_scene(path: Path, donor: Path | None = None) -> None:
    text = path.read_text(encoding="utf-8")
    text = remove_corrupt_pause_overrides(text)

    if donor is not None:
        donor_text = donor.read_text(encoding="utf-8")
        text = ensure_canvas_instance(text, donor_text)

    docs = split_yaml_documents(text)
    root_id = find_legacy_pause_root_id(docs)
    if root_id is None:
        print(f"  No legacy Pause Screen root in {path.name} (may already be fixed)")
    else:
        legacy_ids = collect_legacy_pause_ids(docs, root_id)
        docs = [d for d in docs if (h := parse_doc_header(d)) is None or h[1] not in legacy_ids]
        print(f"  Removed {len(legacy_ids)} legacy pause object ids from {path.name}")
        text = "".join(docs)

    docs = split_yaml_documents(text)
    orphan_ids = find_orphan_legacy_pause_root_ids(docs)
    if orphan_ids:
        orphan_rect_ids = {
            h[1]
            for doc in docs
            if (h := parse_doc_header(doc)) and h[0] == "224" and h[1] in orphan_ids
        }
        docs = [d for d in docs if (h := parse_doc_header(d)) is None or h[1] not in orphan_ids]
        print(f"  Removed {len(orphan_ids)} orphan legacy pause UI ids from {path.name}")
        text = remove_from_scene_roots("".join(docs), orphan_rect_ids)
    else:
        text = "".join(docs)

    text = fix_canvas_prefab_instance(text, LEGACY_RECT_IDS[path.name])
    text = fix_ui_manager(text, LEGACY_ANIM_IDS[path.name])
    text = fix_ui_manager_panels(text)
    text = fix_backdrop_override(text)
    if path.name in ("Level3.unity", "Level4.unity"):
        text = fix_canvas_scaler(text)

    path.write_text(text, encoding="utf-8")
    print(f"Fixed {path.name}")


def main() -> int:
    level1 = ROOT / "Assets" / "_Scenes" / "Level1.unity"
    level3 = ROOT / "Assets" / "_Scenes" / "Level3.unity"
    level4 = ROOT / "Assets" / "_Scenes" / "Level4.unity"

    for scene in (level1, level3):
        if not scene.exists():
            print(f"Missing scene: {scene}", file=sys.stderr)
            return 1
        fix_scene(scene)

    if not level4.exists():
        print(f"Missing scene: {level4}", file=sys.stderr)
        return 1

    level4_text = level4.read_text(encoding="utf-8")
    donor = level3 if f"--- !u!1001 &{CANVAS_INSTANCE_ID}\n" not in level4_text else None
    fix_scene(level4, donor=donor)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
