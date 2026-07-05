#!/usr/bin/env python3
from pathlib import Path
import re


def extract_block(text: str, oid: str) -> str | None:
    m = re.search(rf"^--- !u!\d+ &{oid}\n", text, flags=re.M)
    if not m:
        return None
    nxt = re.search(r"^--- !u!", text[m.end() :], flags=re.M)
    end = m.end() + nxt.start() if nxt else len(text)
    return text[m.start() : end]


def add_label(path: Path, image_rt_id: str, id_base: int, label_text: str) -> None:
    text = path.read_text(encoding="utf-8")
    if "m_Text: Level 4" in text and f"&{id_base}" in text:
        print(f"{path}: label already present")
        return

    src_ids: list[str] = []
    for m in re.finditer(r"^--- !u!1 &(\d+)\nGameObject:\n", text, flags=re.M):
        start = m.start()
        nxt = re.search(r"^--- !u!", text[m.end() :], flags=re.M)
        end = m.end() + nxt.start() if nxt else len(text)
        block = text[start:end]
        if not re.search(r"^  m_Name: Label$", block, flags=re.M):
            continue
        comps = re.findall(r"component: \{fileID: (\d+)\}", block)
        for cid in comps:
            tb = extract_block(text, cid)
            if tb and "m_Text: Level 3" in tb:
                src_ids = [m.group(1)] + comps
                break
        if src_ids:
            break

    if not src_ids:
        print(f"{path}: source Label not found")
        return

    id_map = {oid: str(id_base + i) for i, oid in enumerate(src_ids)}
    cloned: list[str] = []
    for oid in src_ids:
        block = extract_block(text, oid)
        if not block:
            continue
        for old in sorted(id_map.keys(), key=len, reverse=True):
            block = block.replace(f"&{old}", f"&{id_map[old]}")
            block = block.replace(f"{{fileID: {old}}}", f"{{fileID: {id_map[old]}}}")
        block = block.replace("m_Text: Level 3", f"m_Text: {label_text}")
        block = re.sub(
            r"m_Father: \{fileID: \d+\}",
            f"m_Father: {{fileID: {image_rt_id}}}",
            block,
            count=1,
        )
        cloned.append(block.rstrip("\n"))

    go_block = extract_block(text, src_ids[0])
    comps = re.findall(r"component: \{fileID: (\d+)\}", go_block or "")
    label_rt = id_map[comps[0]]

    replaced = False
    text2, n = re.subn(
        rf"(--- !u!224 &{image_rt_id}\nRectTransform:\n(?:.*\n)*?  m_Children: )\[\]\n",
        rf"\1\n  - {{fileID: {label_rt}}}\n",
        text,
        count=1,
    )
    if n:
        text = text2
        replaced = True
    else:
        text2, n = re.subn(
            rf"(--- !u!224 &{image_rt_id}\nRectTransform:\n(?:.*\n)*?  m_Children:\n(?:  - \{{fileID: \d+\}}\n)*)",
            rf"\g<0>  - {{fileID: {label_rt}}}\n",
            text,
            count=1,
        )
        if n:
            text = text2
            replaced = True

    if not replaced:
        print(f"{path}: could not attach label to {image_rt_id}")
        return

    if not text.endswith("\n"):
        text += "\n"
    text += "\n".join(cloned) + "\n"
    path.write_text(text, encoding="utf-8", newline="\n")
    print(f'{path}: added Label "{label_text}" under {image_rt_id}')


def main() -> None:
    add_label(Path("Assets/_Scenes/MainMenu.unity"), "920000051", 921000000, "Level 4")

    prefab = Path("Assets/Prefabs/UI/MainMenu.prefab")
    pf = prefab.read_text(encoding="utf-8")
    for gm in re.finditer(r"^--- !u!1 &(\d+)\nGameObject:\n", pf, flags=re.M):
        start = gm.start()
        nxt = re.search(r"^--- !u!", pf[gm.end() :], flags=re.M)
        end = gm.end() + nxt.start() if nxt else len(pf)
        block = pf[start:end]
        if not re.search(r"^  m_Name: LevelFourButton$", block, flags=re.M):
            continue
        comps = re.findall(r"component: \{fileID: (\d+)\}", block)
        rtb = extract_block(pf, comps[0])
        child = re.search(r"m_Children:\n  - \{fileID: (\d+)\}", rtb or "")
        if not child:
            print("Prefab image child not found")
            return
        add_label(prefab, child.group(1), 931000000, "Level 4")
        break


if __name__ == "__main__":
    main()
