# -*- coding: utf-8 -*-
"""
Simulation of the agent-pattern selection rule on git history (200 commits).

NOTE: this is a test of the rule's CONSISTENCY AND ROBUSTNESS on proxy features from git,
not a test of selection accuracy. Task features (subtask independence, path uncertainty,
risk) are approximated here from git data. The result does not prove that the choice is good.

Usage:  python -I select_pattern.py [number_of_commits] > results.md
Requires: git in PATH, the repo at REPO.
"""
import subprocess, re, sys, random, collections, statistics, io

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", newline="\n")

REPO = "C:/Projekty/ECommerceApp"
N = int(sys.argv[1]) if len(sys.argv) > 1 else 200


def git(*args):
    return subprocess.run(["git", "-C", REPO] + list(args), capture_output=True,
                          text=True, encoding="utf-8", errors="replace").stdout


def load(n):
    commits = {}
    order = []
    num = git("log", "--no-merges", "-n", str(n), "--format=@@%h|%ad|%s", "--date=short", "--numstat")
    cur = None
    for line in num.splitlines():
        if line.startswith("@@"):
            h, d, s = line[2:].split("|", 2)
            cur = {"h": h, "d": d, "s": s, "files": [], "add": 0, "del": 0, "status": {}}
            commits[h] = cur
            order.append(h)
        elif line.strip() and cur is not None:
            p = line.split("\t")
            if len(p) == 3:
                a = 0 if p[0] == "-" else int(p[0])
                b = 0 if p[1] == "-" else int(p[1])
                cur["files"].append(p[2])
                cur["add"] += a
                cur["del"] += b
    ns = git("log", "--no-merges", "-n", str(n), "--format=@@%h", "--name-status")
    cur = None
    for line in ns.splitlines():
        if line.startswith("@@"):
            cur = commits.get(line[2:])
        elif line.strip() and cur is not None:
            p = line.split("\t")
            if len(p) >= 2:
                cur["status"][p[-1]] = p[0][0]
    return [commits[h] for h in order]


def area(path):
    seg = path.split("/")
    if seg[0].startswith("ECommerceApp") and len(seg) > 2:
        return seg[0] + "/" + seg[1]
    return seg[0]


def features(c):
    files = c["files"]
    nf = len(files)
    areas = collections.Counter(area(f) for f in files)
    projects = set(f.split("/")[0] for f in files)
    src_projects = set(f.split("/")[0] for f in files
                       if not (f.startswith("docs/") or f.startswith(".github/") or "Tests" in f or "E2E" in f))
    docs_only = all(f.startswith("docs/") or f.endswith(".md") for f in files) if files else False
    tests_only = all(("Tests" in f or "E2E" in f) for f in files) if files else False
    has_tests = any(("Tests" in f or "E2E" in f) for f in files)
    mig = any("/Migrations/" in f for f in files)
    saga = any("Saga" in f for f in files)
    domain = any("/Domain/" in f for f in files)
    msg = any("Messaging" in f for f in files)
    added = sum(1 for f in files if c["status"].get(f) == "A")
    m = re.match(r"(\w+)(\([^)]*\))?!?:", c["s"])
    typ = m.group(1) if m else "other"
    return {
        "nf": nf, "lines": c["add"] + c["del"], "nareas": len(areas), "nproj": len(projects),
        "nsrc": len(src_projects),
        "maxshare": (max(areas.values()) / nf) if nf else 1.0,
        "docs_only": docs_only, "tests_only": tests_only, "has_tests": has_tests,
        "mig": mig, "saga": saga, "domain": domain, "msg": msg,
        "risky": mig or saga or domain or msg,
        "added_ratio": (added / nf) if nf else 0.0, "type": typ,
    }


BASE = {"S_f": 2, "S_l": 60, "L_f": 12, "L_l": 800, "L_p": 4, "A_thr": 4, "share": 0.6,
        "U_add": 0.4, "U_files": 8, "risk_mode": "broad"}


def size_class(f, p):
    if f["nf"] <= p["S_f"] and f["lines"] <= p["S_l"]:
        return "S"
    if f["nf"] > p["L_f"] or f["nproj"] >= p["L_p"] or f["lines"] > p["L_l"]:
        return "L"
    return "M"


def is_risk_high(f, p):
    if p.get("risk_mode") == "src":
        return f["mig"] or f["saga"] or f["nsrc"] >= 3
    if p.get("risk_mode") == "narrow":
        return f["mig"] or f["saga"] or f["nproj"] >= 4
    return f["risky"] or f["nproj"] >= 3


def recommend(f, p):
    """Returns (set of patterns, autonomy level A/B/C or '-')."""
    s = size_class(f, p)
    risk_high = is_risk_high(f, p)
    if (f["docs_only"] or f["tests_only"]) and s != "L":
        return frozenset(["NONE"]), "-"
    if s == "S" and not risk_high:
        return frozenset(["NONE"]), "-"
    pats = {"SEQUENTIAL_GATES"}
    tier = "A"
    open_ended = (f["type"] == "feat" and f["added_ratio"] >= p["U_add"] and f["nf"] >= p["U_files"])
    decomposable = (f["nareas"] >= p["A_thr"] and f["maxshare"] <= p["share"])
    if open_ended:
        pats.add("PLANNER_EXECUTOR")
        tier = "B"
    if decomposable and s == "L":
        pats.add("FANOUT_DECOMP_CHECK")
    if risk_high:
        pats.add("INDEPENDENT_VALIDATION")
        tier = "B"
        if not f["has_tests"]:
            pats.add("ADD_DETERMINISTIC_CHECK_FIRST")
    if s == "L" and risk_high and open_ended:
        pats.add("DEBATE_CANDIDATE_ASK")
        tier = "C"
    return frozenset(pats), tier


def dist(recs, n):
    cnt = collections.Counter()
    for pats, _ in recs:
        for pt in pats:
            cnt[pt] += 1
    tiers = collections.Counter(t for _, t in recs)
    return cnt, tiers


def main():
    commits = load(N)
    feats = [features(c) for c in commits]
    base = [recommend(f, BASE) for f in feats]
    n = len(commits)
    out = print
    out("# Pattern selection simulation results (git proxies)\n")
    out("Commits: %d (from %s to %s). Script: `scripts/select_pattern.py`.\n" % (n, commits[-1]["d"], commits[0]["d"]))
    out("**This is a test of the rule's consistency and robustness on approximate features, not a test of selection accuracy.**\n")
    out("## 1. Distribution of recommendations (base thresholds: %s)\n" % BASE)
    cnt, tiers = dist(base, n)
    out("| Pattern | Commits | Share |\n|---|---|---|")
    for k, v in cnt.most_common():
        out("| %s | %d | %.0f%% |" % (k, v, 100.0 * v / n))
    out("\n| Autonomy level | Commits | Share |\n|---|---|---|")
    for t in ["-", "A", "B", "C"]:
        out("| %s | %d | %.0f%% |" % (t if t != "-" else "no pattern", tiers[t], 100.0 * tiers[t] / n))
    sizes = collections.Counter(size_class(f, BASE) for f in feats)
    out("\nSize (S/M/L): %s\n" % dict(sizes))

    rnd = random.Random(20261015)
    space = {"S_f": [1, 2, 3], "S_l": [40, 60, 100], "L_f": [8, 12, 16], "L_l": [500, 800, 1200],
             "L_p": [3, 4, 5], "A_thr": [3, 4, 5], "share": [0.5, 0.6, 0.7], "U_add": [0.3, 0.4, 0.5],
             "U_files": [6, 8, 10]}
    K = 300
    same = [0] * n
    patflip = collections.Counter()
    tierflip = [0] * n
    for _ in range(K):
        p = dict(BASE)
        p.update({k: rnd.choice(v) for k, v in space.items()})
        for i, f in enumerate(feats):
            pats, tier = recommend(f, p)
            if pats == base[i][0]:
                same[i] += 1
            if tier != base[i][1]:
                tierflip[i] += 1
            for pt in pats.symmetric_difference(base[i][0]):
                patflip[pt] += 1
    stab = [s / K for s in same]
    out("## 2. Robustness to threshold changes (%d random threshold combinations, fixed seed)\n" % K)
    out("- Mean share of combinations in which a commit's recommendation is identical to the base one: **%.0f%%**" % (100 * statistics.mean(stab)))
    out("- Commits with a stable recommendation (identical in ≥ 90%% of combinations): **%.0f%%**" % (100.0 * sum(1 for s in stab if s >= 0.9) / n))
    out("- Unstable commits (identical in < 60%% of combinations): **%.0f%%**" % (100.0 * sum(1 for s in stab if s < 0.6) / n))
    out("- Mean share of combinations with a change of autonomy level: **%.0f%%**\n" % (100.0 * statistics.mean(t / K for t in tierflip)))
    out("| Pattern | Changes (presence/absence) in total |\n|---|---|")
    for k, v in patflip.most_common():
        out("| %s | %d |" % (k, v))

    out("\n## 3. Sheet to label (the 20 latest M/L commits, not docs/tests-only)\n")
    out("The \"Agree?\" column is empty: enter Y/N if the proposed pattern would make sense. "
        "The proposal comes from the rule and git proxies, **not** from an agent's assessment.\n")
    out("| # | Commit | Date | Subject | Files / lines / areas | Proposed | Level | Agree? |\n|---|---|---|---|---|---|---|---|")
    k = 0
    for c, f, (pats, tier) in zip(commits, feats, base):
        if size_class(f, BASE) == "S" or f["docs_only"] or f["tests_only"]:
            continue
        k += 1
        subj = c["s"].replace("|", "/")[:70]
        out("| %d | `%s` | %s | %s | %d / %d / %d | %s | %s | |" % (
            k, c["h"], c["d"], subj, f["nf"], f["lines"], f["nareas"], ", ".join(sorted(pats)), tier))
        if k >= 20:
            break

    smalls = [i for i, f in enumerate(feats) if size_class(f, BASE) == "S"]
    nonone = [i for i in smalls if base[i][0] != frozenset(["NONE"])]
    out("\n## 4. Edge test: small tasks (S)\n")
    out("- S commits: %d; of which with a recommended pattern (should occur only at high risk): **%d**" % (len(smalls), len(nonone)))
    for i in nonone[:5]:
        out("  - `%s` %s -> %s" % (commits[i]["h"], commits[i]["s"][:60], ", ".join(sorted(base[i][0]))))

    trig = collections.Counter()
    for f in feats:
        for key in ("mig", "saga", "domain", "msg"):
            if f[key]:
                trig[key] += 1
        if f["nproj"] >= 3:
            trig["nproj>=3"] += 1
    out("\n## 5. Diagnostics: what triggers \"high risk\" in the broad variant\n")
    out("| Trigger | Commits |\n|---|---|")
    for key, v in trig.most_common():
        out("| %s | %d |" % (key, v))

    nb = dict(BASE)
    nb["risk_mode"] = "narrow"
    narrow = [recommend(f, nb) for f in feats]
    cn, tn = dist(narrow, n)
    out("\n## 6. Variant with narrower risk (migrations, sagas, ≥ 4 projects): one change relative to the base\n")
    out("| Pattern | Base | Variant |\n|---|---|---|")
    for key in ["SEQUENTIAL_GATES", "INDEPENDENT_VALIDATION", "ADD_DETERMINISTIC_CHECK_FIRST",
                "FANOUT_DECOMP_CHECK", "PLANNER_EXECUTOR", "DEBATE_CANDIDATE_ASK", "NONE"]:
        out("| %s | %d (%.0f%%) | %d (%.0f%%) |" % (key, cnt[key], 100.0 * cnt[key] / n, cn[key], 100.0 * cn[key] / n))
    out("\n| Level | Base | Variant |\n|---|---|---|")
    for t in ["-", "A", "B", "C"]:
        out("| %s | %d (%.0f%%) | %d (%.0f%%) |" % (t if t != "-" else "none", tiers[t], 100.0 * tiers[t] / n,
                                                 tn[t], 100.0 * tn[t] / n))

    nb2 = dict(BASE)
    nb2["risk_mode"] = "src"
    src = [recommend(f, nb2) for f in feats]
    cs, ts = dist(src, n)
    out("\n## 7. Variant 2: risk = migrations, sagas or ≥ 3 SOURCE projects (excluding docs, tests and .github)\n")
    out("Reason: in the base variant \"≥ 3 projects\" also counted `docs/` and test projects as separate projects.\n")
    out("| Pattern | Base | Variant 2 |\n|---|---|---|")
    for key in ["SEQUENTIAL_GATES", "INDEPENDENT_VALIDATION", "ADD_DETERMINISTIC_CHECK_FIRST",
                "FANOUT_DECOMP_CHECK", "PLANNER_EXECUTOR", "DEBATE_CANDIDATE_ASK", "NONE"]:
        out("| %s | %d (%.0f%%) | %d (%.0f%%) |" % (key, cnt[key], 100.0 * cnt[key] / n, cs[key], 100.0 * cs[key] / n))
    out("\n| Level | Base | Variant 2 |\n|---|---|---|")
    for t in ["-", "A", "B", "C"]:
        out("| %s | %d (%.0f%%) | %d (%.0f%%) |" % (t if t != "-" else "none", tiers[t], 100.0 * tiers[t] / n,
                                                 ts[t], 100.0 * ts[t] / n))


main()
