"""
Soát API Unity: đọc Tools/UnityCompileCheck/UnityStubs.cs (= danh sách mọi API Unity mà code PawFit dùng),
tìm từng kiểu / thành viên trong mã nguồn tham chiếu chính thức của Unity (UnityCsReference) đúng namespace,
in khai báo THẬT cạnh khai báo trong stub ra file api_report.txt để soát chữ ký.
Chỉ soát được engine + editor; package URP / SRP Core / uGUI không nằm trong UnityCsReference.

Cách chạy (từ gốc repo, cần mạng đọc GitHub):
    GIT_LFS_SKIP_SMUDGE=1 git clone --depth 1 --branch 6000.3.29f1 \
        https://github.com/Unity-Technologies/unitycsreference /tmp/unitycsreference
    python3 Tools/ApiAudit/api_audit.py Tools/UnityCompileCheck/UnityStubs.cs /tmp/unitycsreference
"VẤN ĐỀ" rỗng (trừ các kiểu thuộc package) = mọi API engine / editor đều có trong bản Unity đó.

Soát API của 1 package (vd Mobile Notifications 2.5.0, tải từ packages.unity.com) — chỉ namespace đó:
    python3 Tools/ApiAudit/api_audit.py Tools/UnityCompileCheck/UnityStubs.cs <thư mục package> Unity.Notifications
"""
import os
import re
import sys
from collections import defaultdict

STUBS, REF = sys.argv[1], sys.argv[2]
ONLY_NS = sys.argv[3] if len(sys.argv) > 3 else None   # soát riêng 1 namespace của package
SKIP_NS = ("UnityEngine.UI", "UnityEngine.EventSystems", "UnityEngine.Rendering.Universal", "UnityEngine.Events", "Unity.Notifications")

files_by_type = defaultdict(list)
texts = {}
decl = re.compile(r'\b(?:class|struct|enum|interface)\s+([A-Za-z_]\w*)')
for root, _, files in os.walk(REF):
    if "/.git" in root:
        continue
    for f in files:
        if f.endswith(".cs"):
            p = os.path.join(root, f)
            t = open(p, encoding="utf-8", errors="ignore").read()
            texts[p] = t
            for m in decl.finditer(t):
                files_by_type[m.group(1)].append(p)


def in_ns(text, ns):
    return re.search(r'namespace\s+' + re.escape(ns) + r'\s*(\{|$|\n|;)', text, re.M) is not None


# ---- đọc stub: (ns, type) -> [(member, dòng stub)] ----
lines = open(STUBS, encoding="utf-8").read().splitlines()
ns, stack, depth, pending = None, [], 0, None
used = defaultdict(list)
mem = re.compile(r'\bpublic\s+(?:(?:static|readonly|const|virtual|override|sealed|abstract|new|event)\s+)*'
                 r'(?:[\w<>\[\],\.\?]+(?:\s*<[^>]*>)?\s+)?([A-Za-z_]\w*)\s*(\(|\{|=>|=|;|<)')
for raw in lines:
    s = raw.strip()
    m = re.match(r'namespace\s+([\w\.]+)', s)
    if m:
        ns = m.group(1)
    tm = re.search(r'\b(class|struct|enum|interface)\s+([A-Za-z_]\w*)', s)
    o, c = s.count("{"), s.count("}")
    if pending is not None and s.startswith("{"):   # dấu { của kiểu khai báo ở dòng trước
        stack.append((pending, depth))
        pending = None
        depth += o - c
        continue
    pending = None
    if tm and not s.startswith("//") and "{" not in s and not s.endswith(";"):
        used.setdefault((ns, tm.group(2)), [])
        pending = tm.group(2)
        continue
    if tm and not s.startswith("//"):
        kind, tname = tm.group(1), tm.group(2)
        owner = stack[-1][0] if stack else None
        key = (ns, tname)
        used.setdefault(key, [])
        rest = s.split("{", 1)[1] if "{" in s else ""
        if kind == "enum":
            for v in re.findall(r'([A-Za-z_]\w*)\s*(?:=\s*-?[\w\.]+)?\s*[,}]', rest):
                used[key].append((v, s))
        else:
            for mm in mem.finditer(rest):
                if mm.group(1) != tname:
                    used[key].append((mm.group(1), s))
        if o > c:
            stack.append((tname, depth))
    elif stack:
        for mm in mem.finditer(s):
            used[(ns, stack[-1][0])].append((mm.group(1), s))
    depth += o - c
    while stack and depth <= stack[-1][1]:
        stack.pop()

report = []
problems = []
for (n, t), members in sorted(used.items(), key=lambda kv: ((kv[0][0] or ""), kv[0][1])):
    if n is None:
        continue
    if ONLY_NS is not None:
        if n != ONLY_NS and not n.startswith(ONLY_NS + "."):
            continue
    elif n in SKIP_NS:
        continue
    cands = [p for p in files_by_type.get(t, []) if in_ns(texts[p], n)]
    if not cands:
        problems.append("KIỂU KHÔNG THẤY trong namespace %s: %s" % (n, t))
        continue
    blob = "\n".join(texts[p] for p in cands)
    for name, stub_line in members:
        pat = re.compile(r'^[^\n]*\bpublic\b[^\n;]*\b' + re.escape(name) + r'\b[^\n]*$', re.M)
        hits = [h.strip() for h in pat.findall(blob)][:2]
        if not hits:   # enum value / thành viên không có chữ public trên cùng dòng
            hits = [h.strip() for h in re.findall(r'^[^\n]*\b' + re.escape(name) + r'\b\s*(?:=|,|\(|;)[^\n]*$', blob, re.M)][:1]
        if not hits:
            problems.append("THÀNH VIÊN KHÔNG THẤY: %s.%s.%s   (stub: %s)" % (n, t, name, stub_line[:110]))
        else:
            report.append("%s.%s.%s\n    stub: %s\n    real: %s" % (n, t, name, stub_line[:150], " || ".join(h[:170] for h in hits)))

open("api_report.txt", "w").write("\n".join(report))
print("== VẤN ĐỀ ==")
print("\n".join(problems) if problems else "(không có)")
print("\n%d thành viên có khai báo thật (xem api_report.txt)" % len(report))
