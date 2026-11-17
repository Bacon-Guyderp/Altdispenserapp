# Simulates the puzzle answers (a port of FirGameManager.CodeAt/Mix/Perm with C# int semantics) for
# 300,000 random seeds: no 32-bit overflow, and every puzzle has exactly one solution.  python check_puzzles.py
# Port of FirGameManager.CodeAt / Hash / Perm, with C# int semantics, checked for every puzzle.
import random, itertools
I32 = 2**31 - 1
def cmod(a, b):            # C# % truncates toward zero
    r = abs(a) % b
    return -r if a < 0 else r
peak = 0
def chk(v):
    global peak
    peak = max(peak, abs(v)); assert abs(v) <= I32, v
    return v
def Mix(x, salt, i):
    h = cmod(cmod(x, 46309) + cmod(x // 46309, 46309) + salt * 7919 + i * 104729 + 1, 46309)
    for k in range(4): h = cmod(chk(h * h + h * 37 + salt * 31 + i * 7 + k * 13 + 17), 46309)
    return h
def Hash(seed, salt, i): return Mix(seed, salt, i)
def Perm(seed, salt, n, i):
    order = [0, 1, 2, 3]; s = Mix(seed, salt, 0)
    for k in range(n - 1, 0, -1):
        s = Mix(s, salt, k); j = cmod(s, k + 1); order[k], order[j] = order[j], order[k]
    return order[i]
def CodeAt(seed, ch, i):
    if ch == 1:
        v = cmod(Hash(seed, 1, 0), 4)
        for k in range(1, i + 1): v = cmod(v + 1 + cmod(Hash(seed, 1, k), 3), 4)
        return v
    if ch == 2: return cmod(Hash(seed, 2, i), 10)
    if ch == 3: return Perm(seed, 3, 4, i)
    if ch == 30: return Perm(seed, 30, 4, i)
    if ch == 4: return cmod(seed // 10**i, 10)
    if ch == 5: return Perm(seed, 5, 3, i)
COUNTS = None
def office_counts():
    # port of the BuildOffices counting loop
    emp = hat = phones = cal = 0; n = 0
    for c in range(4):
        for r in range(3):
            if (c * 3 + r) % 3 != 1: phones += 1
            if (c + 2 * r) % 3 == 0: cal += 1
            if (c + r) % 2 == 0:
                emp += 1
                if n % 3 == 0: hat += 1
            n += 1
    return [emp, hat, phones, cal]
COUNTS = office_counts()
seeds = [1, 2, 9, 10, 99990, 99991, 100002, 100003, 2**30 - 1] + [random.randint(1, 2**30 - 1) for _ in range(300000)]
fails = {}
def fail(k, s): fails.setdefault(k, s)
dist_carol = set()
for s in seeds:
    carol = [CodeAt(s, 1, i) for i in range(5)]
    if not all(0 <= v < 4 for v in carol) or any(a == b for a, b in zip(carol, carol[1:])): fail('carol', s)
    dist_carol.add(tuple(carol))
    if not all(0 <= CodeAt(s, 2, i) <= 9 for i in range(3)): fail('glow digits', s)
    order = [CodeAt(s, 3, i) for i in range(4)]; names = [CodeAt(s, 30, i) for i in range(4)]
    if sorted(order) != [0, 1, 2, 3] or sorted(names) != [0, 1, 2, 3]: fail('tape perms', s)
    board = [names[order[i]] for i in range(4)]                      # whiteboard: session names in order
    solved = [names.index(b) for b in board]                          # player: name -> colour via the booth card
    if solved != order: fail('tape solve', s)
    code = [CodeAt(s, 4, i) for i in range(4)]
    adds = [((code[i] - COUNTS[i] % 10) % 10 + 10) % 10 for i in range(4)]   # (python % already >= 0; mirrors C#)
    if [(COUNTS[i] + adds[i]) % 10 for i in range(4)] != code: fail('memo solve', s)
    hats = [CodeAt(s, 5, i) for i in range(3)]
    if sorted(hats) != [0, 1, 2]: fail('hat perm', s)
    r = cmod(s // 7, 3); b = (r + 1) % 3; c = (r + 2) % 3
    sols = [p for p in itertools.permutations(range(3)) if p[r] == hats[r] and p[b] != hats[c]]
    if sols != [tuple(hats)]: fail('hat clues', s)
print(f"{len(seeds)} seeds checked; largest intermediate value {peak:,} (int32 max {I32:,})")
print("office counts (employees, hatted, phones, calendars):", COUNTS)
print("distinct carols seen:", len(dist_carol), "of", 4 * 3**4, "possible")
print("failures:", fails or "none")
