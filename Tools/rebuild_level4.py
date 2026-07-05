#!/usr/bin/env python3
"""Safely rebuild Level4 from Level3: E walls, enemies, player — without deleting core objects."""
from __future__ import annotations

import re
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
LEVEL3 = ROOT / "Assets" / "_Scenes" / "Level3.unity"
LEVEL4 = ROOT / "Assets" / "_Scenes" / "Level4.unity"

CELL = 6.0
ORIGIN_X = 0.0
ORIGIN_Y = 0.0
E_WIDTH = 102.0
E_HEIGHT = 138.0
CORRIDOR = 22.0
SPINE_RIGHT = 28.0
MID_BAR_RIGHT = 88.0

PLAYER_GUID = "f9b09d7adee881048a63dfe0f900e3b6"
CHASER = "cfd62ad4c55daa942bae8f75211b08e0"
DIAG_CHASER = "576cba70e4daccf488dda522850ba325"
STRAIGHT_SHOOTER = "fd821cc36f666d54592388e02f75fcb5"
DIAG_SHOOTER = "91411278b0dfa9a468f417268eba15a3"
ENEMY_GUIDS = {CHASER, DIAG_CHASER, STRAIGHT_SHOOTER, DIAG_SHOOTER}

# Предпочтительные GUID для размещений (должны существовать в Level3 для обновлений на месте).
# Extra placements beyond existing instances are appended by cloning same-guid templates.
HEALTH_IDS = {
    CHASER: "2029536735206158367",
    DIAG_CHASER: "8022954021460962557",
    STRAIGHT_SHOOTER: "8022954021460962557",
    DIAG_SHOOTER: "8022954021460962557",
}


def fmt(v: float) -> str:
    s = f"{v:.3f}".rstrip("0").rstrip(".")
    return s if s else "0"


def in_corridor(x: float, y: float) -> bool:
    left = ORIGIN_X + 4
    spine_right = ORIGIN_X + SPINE_RIGHT
    bottom_top = ORIGIN_Y + CORRIDOR
    mid_bottom = ORIGIN_Y + (E_HEIGHT - CORRIDOR) * 0.5 - CORRIDOR * 0.5
    mid_top = mid_bottom + CORRIDOR
    top_bottom = ORIGIN_Y + E_HEIGHT - CORRIDOR
    top_top = ORIGIN_Y + E_HEIGHT - 4
    bottom_right = ORIGIN_X + E_WIDTH - 4
    mid_right = ORIGIN_X + MID_BAR_RIGHT
    top_right = ORIGIN_X + E_WIDTH - 4
    return (
        (left <= x <= spine_right and (ORIGIN_Y + 4) <= y <= top_top)
        or (left <= x <= bottom_right and (ORIGIN_Y + 4) <= y <= bottom_top)
        or (left <= x <= mid_right and mid_bottom <= y <= mid_top)
        or (left <= x <= top_right and top_bottom <= y <= top_top)
    )


def wall_positions() -> list[tuple[float, float]]:
    walls: dict[tuple[int, int], tuple[float, float]] = {}
    min_x = int((ORIGIN_X - CELL) // CELL)
    max_x = int((ORIGIN_X + E_WIDTH + CELL) // CELL) + 1
    min_y = int((ORIGIN_Y - CELL) // CELL)
    max_y = int((ORIGIN_Y + E_HEIGHT + CELL) // CELL) + 1
    for gx in range(min_x, max_x + 1):
        for gy in range(min_y, max_y + 1):
            wx = gx * CELL + CELL * 0.5
            wy = gy * CELL + CELL * 0.5
            if in_corridor(wx, wy):
                continue
            adjacent = any(
                in_corridor((gx + dx) * CELL + CELL * 0.5, (gy + dy) * CELL + CELL * 0.5)
                for dx in (-1, 0, 1)
                for dy in (-1, 0, 1)
                if not (dx == 0 and dy == 0)
            )
            on_frame = gx in (min_x, max_x) or gy in (min_y, max_y)
            if adjacent or on_frame:
                walls[(gx, gy)] = (wx, wy)
    return list(walls.values())


def lerp(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)


def enemy_placements() -> list[dict]:
    placements: list[dict] = []

    def add_line(guid, count, start, end, hp, speed, follow):
        for i in range(count):
            t = 0.5 if count == 1 else i / (count - 1)
            x, y = lerp(start, end, t)
            placements.append(
                {"guid": guid, "x": x, "y": y, "hp": hp, "speed": speed, "follow": follow}
            )

    add_line(CHASER, 11, (ORIGIN_X + E_WIDTH - 18, ORIGIN_Y + CORRIDOR * 0.5),
             (ORIGIN_X + SPINE_RIGHT + 6, ORIGIN_Y + CORRIDOR * 0.5), 3, 2.2, 40)
    add_line(DIAG_CHASER, 8, (ORIGIN_X + SPINE_RIGHT * 0.5, ORIGIN_Y + CORRIDOR + 4),
             (ORIGIN_X + SPINE_RIGHT * 0.5, ORIGIN_Y + E_HEIGHT * 0.5 - CORRIDOR * 0.5 - 4), 4, 2.4, 50)
    add_line(STRAIGHT_SHOOTER, 6, (ORIGIN_X + SPINE_RIGHT + 8, ORIGIN_Y + E_HEIGHT * 0.5),
             (ORIGIN_X + MID_BAR_RIGHT - 8, ORIGIN_Y + E_HEIGHT * 0.5), 7, 0.0, 0.0)
    add_line(CHASER, 5, (ORIGIN_X + SPINE_RIGHT + 10, ORIGIN_Y + E_HEIGHT * 0.5 - 4),
             (ORIGIN_X + MID_BAR_RIGHT - 10, ORIGIN_Y + E_HEIGHT * 0.5 + 4), 6, 2.6, 55)
    add_line(DIAG_CHASER, 8, (ORIGIN_X + SPINE_RIGHT * 0.5, ORIGIN_Y + E_HEIGHT * 0.5 + CORRIDOR * 0.5 + 4),
             (ORIGIN_X + SPINE_RIGHT * 0.5, ORIGIN_Y + E_HEIGHT - CORRIDOR - 4), 8, 2.8, 60)
    add_line(STRAIGHT_SHOOTER, 6, (ORIGIN_X + SPINE_RIGHT + 8, ORIGIN_Y + E_HEIGHT - CORRIDOR * 0.5),
             (ORIGIN_X + E_WIDTH - 16, ORIGIN_Y + E_HEIGHT - CORRIDOR * 0.5), 10, 0.0, 0.0)
    # Use StraightShooter for last groups if DiagonalShooter templates are awkward;
    # Level3 has StraightShooter instances we can clone.
    add_line(STRAIGHT_SHOOTER, 4, (ORIGIN_X + SPINE_RIGHT + 14, ORIGIN_Y + E_HEIGHT - CORRIDOR * 0.5 + 3),
             (ORIGIN_X + E_WIDTH - 20, ORIGIN_Y + E_HEIGHT - CORRIDOR * 0.5 - 3), 12, 0.0, 0.0)
    add_line(CHASER, 4, (ORIGIN_X + SPINE_RIGHT + 20, ORIGIN_Y + E_HEIGHT - CORRIDOR * 0.5),
             (ORIGIN_X + E_WIDTH - 12, ORIGIN_Y + E_HEIGHT - CORRIDOR * 0.5), 14, 3.0, 70)
    return placements


def find_prefab_instances(text: str, guid: str) -> list[tuple[int, int, str]]:
    """Return (start, end, instance_id) for each PrefabInstance sourcing guid.

    Block includes PrefabInstance and all following stripped components that
    reference this instance id, stopping before the next unrelated document.
    """
    results = []
    for m in re.finditer(
        rf"^--- !u!1001 &(\d+)\nPrefabInstance:\n",
        text,
        flags=re.M,
    ):
        iid = m.group(1)
        start = m.start()
        # Find m_SourcePrefab within this PrefabInstance (before next --- !u!)
        nxt = re.search(r"^--- !u!", text[m.end() :], flags=re.M)
        header_end = m.end() + nxt.start() if nxt else len(text)
        header = text[start:header_end]
        src = re.search(
            r"m_SourcePrefab: \{fileID: 100100000, guid: ([a-f0-9]+)",
            header,
        )
        if not src or src.group(1) != guid:
            continue

        # Extend through stripped components belonging to this instance
        end = header_end
        pos = header_end
        while pos < len(text):
            dm = re.match(r"--- !u!(\d+) &(\d+)( stripped)?\n", text[pos:])
            if not dm:
                break
            block_start = pos
            rest = text[pos + dm.end() :]
            nxt2 = re.search(r"^--- !u!", rest, flags=re.M)
            block_end = pos + dm.end() + (nxt2.start() if nxt2 else len(rest))
            block = text[block_start:block_end]
            # Stripped components reference m_PrefabInstance: {fileID: iid}
            if f"m_PrefabInstance: {{fileID: {iid}}}" in block or (
                dm.group(3) and f"m_PrefabInstance: {{fileID: {iid}}}" in block
            ):
                end = block_end
                pos = block_end
                continue
            # Also accept stripped without explicit check if immediately after and marked stripped
            if dm.group(3) and f"fileID: {iid}" in block:
                end = block_end
                pos = block_end
                continue
            break
        results.append((start, end, iid))
    return results


def set_first_property(block: str, path: str, value: str) -> str:
    pattern = rf"(propertyPath: {re.escape(path)}\n\s+value: )([^\n]+)"
    if re.search(pattern, block):
        return re.sub(pattern, rf"\g<1>{value}", block, count=1)
    return block


def set_all_property(block: str, path: str, value: str) -> str:
    pattern = rf"(propertyPath: {re.escape(path)}\n\s+value: )([^\n]+)"
    return re.sub(pattern, rf"\g<1>{value}", block)


def ensure_health(block: str, guid: str, hp: int) -> str:
    block = set_all_property(block, "currentHealth", str(hp))
    block = set_all_property(block, "defaultHealth", str(hp))
    block = set_all_property(block, "maximumHealth", str(hp))
    if "propertyPath: defaultHealth" in block:
        return block
    hid = HEALTH_IDS.get(guid)
    if not hid:
        return block
    mods = "".join(
        "    - target: {fileID: %s, guid: %s, type: 3}\n"
        "      propertyPath: %s\n"
        "      value: %s\n"
        "      objectReference: {fileID: 0}\n"
        % (hid, guid, path, hp)
        for path in ("currentHealth", "defaultHealth", "maximumHealth")
    )
    if "m_RemovedComponents:" in block:
        return block.replace("m_RemovedComponents:", mods + "    m_RemovedComponents:", 1)
    return block


def apply_placement(block: str, placement: dict) -> str:
    block = set_first_property(block, "m_LocalPosition.x", fmt(placement["x"]))
    block = set_first_property(block, "m_LocalPosition.y", fmt(placement["y"]))
    block = set_all_property(block, "moveSpeed", fmt(placement["speed"]))
    block = set_all_property(block, "followRange", fmt(placement["follow"]))
    block = ensure_health(block, placement["guid"], placement["hp"])
    return block


def clone_block(block: str, old_iid: str, new_iid: int) -> tuple[str, int]:
    """Clone prefab instance block with new unique ids. Returns (block, next_id)."""
    # Map old instance id and all stripped ids in the block
    ids = re.findall(r"^--- !u!\d+ &(\d+)", block, flags=re.M)
    id_map: dict[str, str] = {}
    next_id = new_iid
    for oid in ids:
        if oid not in id_map:
            id_map[oid] = str(next_id)
            next_id += 1

    # Also map any &id or {fileID: id} that appear as stripped headers only
    out = block
    for old in sorted(id_map.keys(), key=len, reverse=True):
        out = out.replace(f"&{old}", f"&{id_map[old]}")
        out = out.replace(f"{{fileID: {old}}}", f"{{fileID: {id_map[old]}}}")
    return out, next_id


def update_walls(lines: list[str]) -> list[str]:
    walls = wall_positions()
    print(f"Wall cells: {len(walls)}")
    wall_idx = 0
    asteroid_count = 0
    i = 0
    while i < len(lines):
        if "m_Name: AsteroidWall_Object" in lines[i]:
            asteroid_count += 1
            for k in range(i, min(i + 15, len(lines))):
                if lines[k].lstrip().startswith("m_IsActive:"):
                    if wall_idx < len(walls):
                        lines[k] = re.sub(r"m_IsActive: \d", "m_IsActive: 1", lines[k])
                    else:
                        lines[k] = re.sub(r"m_IsActive: \d", "m_IsActive: 0", lines[k])
                    break
            for j in range(i, min(i + 30, len(lines))):
                if "m_LocalPosition:" in lines[j]:
                    if wall_idx < len(walls):
                        x, y = walls[wall_idx]
                        wall_idx += 1
                        lines[j] = re.sub(
                            r"m_LocalPosition: \{x: [^,]+, y: [^,]+, z: [^}]+\}",
                            f"m_LocalPosition: {{x: {fmt(x)}, y: {fmt(y)}, z: 0}}",
                            lines[j],
                        )
                    else:
                        lines[j] = re.sub(
                            r"m_LocalPosition: \{x: [^,]+, y: [^,]+, z: [^}]+\}",
                            "m_LocalPosition: {x: -999, y: -999, z: 0}",
                            lines[j],
                        )
                    break
        i += 1
    print(f"Asteroids updated: {asteroid_count}, walls placed: {wall_idx}")
    return lines


def update_player(text: str) -> str:
    # Find player PrefabInstance precisely by source guid line
    instances = find_prefab_instances(text, PLAYER_GUID)
    if not instances:
        print("WARNING: Player prefab instance not found")
        return text
    start, end, iid = instances[0]
    block = text[start:end]
    # Player root position in Level3 is the pair that was -2.7 / -19.8
    if "value: -2.7" in block and "value: -19.8" in block:
        block = block.replace("value: -2.7", "value: 94", 1)
        block = block.replace("value: -19.8", "value: 11", 1)
    else:
        # Fallback: set first local position pair
        block = set_first_property(block, "m_LocalPosition.x", "94")
        block = set_first_property(block, "m_LocalPosition.y", "11")
    print(f"Player instance {iid} moved to (94, 11)")
    return text[:start] + block + text[end:]


def update_enemies(text: str) -> str:
    placements = enemy_placements()
    enemy_count = len(placements)
    print(f"Enemy placements: {enemy_count}")

    # Collect existing instances per guid (preserve order in file)
    existing: list[tuple[str, int, int, str]] = []  # guid, start, end, iid
    for guid in ENEMY_GUIDS:
        for start, end, iid in find_prefab_instances(text, guid):
            existing.append((guid, start, end, iid))
    existing.sort(key=lambda t: t[1])
    print(f"Existing enemy instances: {len(existing)}")

    # Templates per guid (full blocks)
    templates: dict[str, str] = {}
    for guid, start, end, iid in existing:
        templates.setdefault(guid, text[start:end])

    # Group placements by preferred guid, assign to existing same-guid first
    by_guid: dict[str, list[dict]] = {}
    for p in placements:
        by_guid.setdefault(p["guid"], []).append(p)

    # Build list of (start, end, new_block) updates for existing instances
    updates: list[tuple[int, int, str]] = []
    remaining_placements: list[dict] = []

    used_existing = set()
    for guid, plist in by_guid.items():
        same = [(g, s, e, i) for g, s, e, i in existing if g == guid]
        for idx, placement in enumerate(plist):
            if idx < len(same):
                g, s, e, iid = same[idx]
                block = apply_placement(text[s:e], placement)
                updates.append((s, e, block))
                used_existing.add((s, e))
            else:
                remaining_placements.append(placement)

    # Any unused existing enemies: park far away (keep scene valid)
    for guid, s, e, iid in existing:
        if (s, e) not in used_existing:
            block = text[s:e]
            block = set_first_property(block, "m_LocalPosition.x", "-999")
            block = set_first_property(block, "m_LocalPosition.y", "-999")
            updates.append((s, e, block))

    # Apply updates from end to start so offsets stay valid
    updates.sort(key=lambda t: t[0], reverse=True)
    for s, e, block in updates:
        text = text[:s] + block + text[e:]

    # Append clones for remaining placements
    next_id = 910000000
    # Find Enemies parent transform id from an existing enemy block
    parent_id = None
    for guid, s, e, iid in existing:
        m = re.search(r"m_TransformParent: \{fileID: (\d+)\}", text[s:e] if False else templates.get(guid, ""))
        # re-find from current text
        inst = find_prefab_instances(text, guid)
        if inst:
            block = text[inst[0][0] : inst[0][1]]
            pm = re.search(r"m_TransformParent: \{fileID: (\d+)\}", block)
            if pm:
                parent_id = pm.group(1)
                break

    extra_blocks: list[str] = []
    for placement in remaining_placements:
        template = templates.get(placement["guid"])
        if not template:
            # fallback to any available
            template = next(iter(templates.values()), None)
        if not template:
            print(f"WARNING: no template for {placement['guid']}")
            continue
        # Extract old instance id from template header
        hm = re.match(r"--- !u!1001 &(\d+)\n", template)
        old_iid = hm.group(1) if hm else "0"
        cloned, next_id = clone_block(template, old_iid, next_id)
        if parent_id:
            cloned = re.sub(
                r"m_TransformParent: \{fileID: \d+\}",
                f"m_TransformParent: {{fileID: {parent_id}}}",
                cloned,
                count=1,
            )
        # Ensure source guid matches placement
        cloned = re.sub(
            r"m_SourcePrefab: \{fileID: 100100000, guid: [a-f0-9]+",
            f"m_SourcePrefab: {{fileID: 100100000, guid: {placement['guid']}",
            cloned,
            count=1,
        )
        cloned = apply_placement(cloned, placement)
        extra_blocks.append(cloned if cloned.endswith("\n") else cloned + "\n")

    if extra_blocks:
        # Insert before SceneRoots so Unity YAML stays valid (SceneRoots must be last).
        insert = "".join(extra_blocks)
        roots_m = re.search(r"^--- !u!1660057539 &", text, flags=re.M)
        if not roots_m:
            roots_m = re.search(r"^SceneRoots:\n", text, flags=re.M)
        if roots_m:
            text = text[: roots_m.start()] + insert + text[roots_m.start() :]
        else:
            if not text.endswith("\n"):
                text += "\n"
            text += insert
        print(f"Inserted {len(extra_blocks)} extra enemies before SceneRoots")

    # enemiesToDefeat
    text = re.sub(
        r"(propertyPath: enemiesToDefeat\n\s+value: )\d+",
        rf"\g<1>{enemy_count}",
        text,
        count=1,
    )
    print(f"enemiesToDefeat = {enemy_count}")
    return text


def update_labels_and_victory(text: str) -> str:
    text = text.replace("m_Name: Level3Label", "m_Name: Level4Label")
    text = re.sub(r"(m_text: )Level 3(\r?\n)", r"\1Level 4\2", text, count=1)

    # Level3 has intermediate victory; Level4 needs final (single-line YAML value).
    victory_text = (
        '"<color=\\"yellow\\">Congratulations!</color>\\n\\nYou WON the GAME!'
        '\\n\\n\\nYou are a \\n\\n<color=\\"red\\">hardcore</color> \\n\\n'
        'sirvival spaceship player!"'
    )
    text = re.sub(
        r'(propertyPath: m_text\n\s+value: )Level 3 <color="green">Complete!</color>',
        rf"\g<1>{victory_text}",
        text,
        count=1,
    )
    text = text.replace(
        "propertyPath: m_text\n      value: Next Level",
        "propertyPath: m_text\n      value: GO TO MAIN MENU",
        1,
    )
    # Victory "Next Level" button override on CanvasInGameUI (fileID 802629...).
    text = re.sub(
        r"(propertyPath: m_OnClick\.m_PersistentCalls\.m_Calls\.Array\.data\[0\]"
        r"\.m_Arguments\.m_StringArgument\n\s+value: )Level4(\n)",
        r"\g<1>MainMenu\2",
        text,
        count=1,
    )
    print("Labels and final victory set")
    return text


def main() -> None:
    print("Copying Level3 -> Level4 ...")
    shutil.copyfile(LEVEL3, LEVEL4)

    text = LEVEL4.read_text(encoding="utf-8")

    # Walls (line-based, safe)
    lines = text.splitlines(keepends=True)
    lines = update_walls(lines)
    text = "".join(lines)

    # Player
    text = update_player(text)

    # Enemies (in-place + append only)
    text = update_enemies(text)

    # Labels / victory
    text = update_labels_and_victory(text)

    LEVEL4.write_text(text, encoding="utf-8", newline="\n")
    print("Level4 rebuild complete.")


if __name__ == "__main__":
    main()
