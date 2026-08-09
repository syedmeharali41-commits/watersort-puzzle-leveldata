import json, os, sys, time

PARTIAL = "levels.partial.json"
LOG = "generation_log.txt"

def main():
    with open(PARTIAL, encoding="utf-8-sig") as f:
        data = json.load(f)
    levels = data.get("levels", [])
    by = {l["levelNumber"]: l for l in levels}
    n = len(levels)

    # rate from log timestamps
    rate = "?"
    try:
        stamps = []
        for line in open(LOG, encoding="utf-8", errors="ignore"):
            if "Generated & Validated" in line:
                try:
                    t = time.mktime(time.strptime(line[1:9], "%H:%M:%S"))
                    stamps.append((t, int(line.split("Validated ")[1].split("/")[0])))
                except Exception:
                    pass
        if len(stamps) >= 2:
            dt = stamps[-1][0] - stamps[-2][0]
            dn = stamps[-1][1] - stamps[-2][1]
            if dt > 0 and dn > 0:
                rate = f"{dn/dt*60:.1f}/min"
    except Exception:
        pass

    print(f"Levels: {n}/10000 | rate: {rate} | failures: {sum(1 for l in levels if l.get('minMoves',0)<=0)}")

    prev = None
    for L in sorted(by):
        if L % 250 == 0 or L == 1:
            lv = by[L]
            g = f"{(lv['minMoves']-prev)*100.0/prev:.0f}%" if prev else "-"
            exact = lv.get("validationExact", True)
            print(f"  L{L:6} K={lv['colorCount']:2} N={lv['tubeCount']:2} C={lv['capacity']} min={lv['minMoves']:3} floor={lv['requiredMinMoves']:3} grow={g:>5} exact={exact}")
            prev = lv["minMoves"]

if __name__ == "__main__":
    main()
