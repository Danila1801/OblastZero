"""Parse a Unity scene file and print the GameObject hierarchy."""
import re
import sys

path = sys.argv[1] if len(sys.argv) > 1 else "Assets/Scenes/_Bootstrap.unity"
t = open(path, encoding="utf-8").read()

go_names = {}     # go_id -> name
tr_go = {}        # transform_id -> go_id
tr_parents = {}   # transform_id -> parent_id

# Parse GameObjects: name
for m in re.finditer(
    r"--- !u!1 &(\d+)\r?\nGameObject:\r?\n(?:.*\r?\n)*?\s*m_Name: ([^\r\n]+)", t
):
    go_names[m.group(1)] = m.group(2).strip()

# Parse Transforms
for m in re.finditer(
    r"--- !u!4 &(\d+)\r?\nTransform:\r?\n"
    r"(?:.*\r?\n)*?\s*m_GameObject: \{fileID: (\d+)\}\r?\n"
    r"(?:.*\r?\n)*?\s*m_Father: \{fileID: (\d+)\}", t
):
    tr_id, go_id, parent_id = m.group(1), m.group(2), m.group(3)
    tr_go[tr_id] = go_id
    tr_parents[tr_id] = parent_id

tr_to_go = tr_go
seen = set()

def print_tree(tr_id, depth=0):
    if tr_id in seen:
        return
    seen.add(tr_id)
    go_id = tr_to_go.get(tr_id, "?")
    name = go_names.get(go_id, f"unknown({go_id})")
    print("  " * depth + f"|-{name}  [tr={tr_id}, go={go_id}]")
    for child_tr, parent_tr in tr_parents.items():
        if parent_tr == tr_id:
            print_tree(child_tr, depth + 1)

print("=== Scene hierarchy ===")
for tr_id, parent_id in tr_parents.items():
    if parent_id == "0":
        print_tree(tr_id)
