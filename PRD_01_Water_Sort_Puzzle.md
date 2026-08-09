# PRD — Water Sort Puzzle
**Platform:** Flutter (Android first) · **Owner:** Mehar / Designcoffers · **Style:** Dark background + Orange accent (#F97316)

---

## 1. Game Overview

Player ko colored liquid ko tubes (test tubes) mein sort karna hai jab tak har tube ek hi color se bhara ho ya empty ho. Pour rule: sirf tabhi pour kar sakte ho jab dono tubes ke top color same ho, aur destination tube mein enough empty space ho.

**Why this genre works:** Zero art overhead, single-finger control, no time pressure, "just one more level" loop. Research confirms minimal-UI water sort games with 1000+ levels and 5 difficulty tiers perform best.

---

## 2. Core Mechanic (Rules)

- Grid of N tubes, each with capacity C (default C = 4 segments).
- Tap Tube A (source) → Tap Tube B (destination).
- Pour valid only if:
  - Tube B is empty, OR top color of Tube B == top color of Tube A
  - Tube B has enough empty space for the contiguous same-color block being poured
- Level solved when every non-empty tube contains a single color only (or is empty).
- **Undo button:** last 1 move free, extra undos cost soft-currency (rewarded ad or coins).
- **Add Tube button:** adds one empty tube as a lifeline (limited uses, ad-gated).
- No timer — untimed puzzle, stress-free positioning (matches top competitor reviews).

---

## 3. UI/UX Spec (based on researched best practices)

**Design language:** Dark-first UI (default, per 2026 mobile trend — dark mode is now the default design surface, not a toggle), OLED-true-black background `#0A0A0A`, orange accent `#F97316` for active/selected tube glow and primary CTA buttons only. No gradients-everywhere approach — keep flat with soft shadows for depth (subtle, not decorative).

**Screen layout:**
- **Top bar:** Level number (center, large), Settings icon (left), Coins/currency (right) — thin, minimal, no clutter.
- **Game board (center, 70% of screen):** Tubes arranged in a single responsive row/grid, auto-wrap for larger levels (e.g., 2 rows if tube count > 7). Tubes rendered as rounded-bottom glass shapes with visible color segments — flat colors, no busy gradients (color itself is the visual interest, UI stays quiet).
- **Bottom action bar:** Undo, Add Tube, Restart — 3 icon buttons, equal spacing, single-finger reachable zone (bottom third of screen — critical for one-handed mobile play).
- **Feedback:** Micro-interaction on valid pour (liquid animates flowing between tubes, ~300ms ease); invalid tap = subtle shake, no harsh error sound.
- **Win screen:** Confetti/particle burst restrained to orange + white tones only (stay on-brand, don't rainbow-explode).
- **Rounded corners everywhere** (soft, approachable — 2026 trend away from sharp edges).

**What to avoid (explicitly, per your preference):** No skeuomorphic glass textures, no busy background patterns, no more than 2 accent colors on any screen (orange + one semantic color e.g. green for success), no cluttered HUD.

---

## 4. Difficulty Progression Logic (critical — must scale properly to 1000+ levels)

**Problem to solve:** Levels must NOT repeat and must NOT stay easy — difficulty must scale mathematically with level number so level 1000 is meaningfully harder than level 50.

### 4.1 Parameters that scale with level number `L`:

| Parameter | Formula | Notes |
|---|---|---|
| Number of colors `K` | `K = min(3 + floor(L / 40), 16)` | Starts at 3 colors, +1 every 40 levels, caps at 16 (device screen limit) |
| Number of tubes `N` | `N = K + 2` (levels 1–200), `N = K + 1` (201–600), `N = K` (601+) | Fewer "buffer" empty tubes as level increases = harder |
| Tube capacity `C` | Fixed at 4 for L ≤ 500, increases to 5 for L > 500 | More segments per tube = more combinations to plan |
| Shuffle depth `S` (reverse-shuffles from solved state used to generate the puzzle) | `S = 20 + L * 3`, capped at `2500` | Higher shuffle depth = statistically harder-to-solve start state |
| Minimum optimal-solution length | Must be ≥ `8 + floor(L/10)` moves (validated by solver, see 4.3) | Ensures a level "looks solved" isn't accidentally easy |

### 4.2 Level generation algorithm (deterministic + non-repeating)

1. Use `level number L` as a **seed** for a seeded PRNG (e.g., `Random(seed: L * 7919 + salt)`).
2. Generate a **solved state**: K colors × C segments each, distributed into K tubes, N−K tubes empty.
3. Apply `S` reverse-legal-pour moves (random legal moves run backward) to scramble it into the puzzle's starting state. Because it's seeded by `L`, the exact same level number always generates the exact same puzzle — but every level number produces a structurally different puzzle (no repeats, ever, up to the seed space).
4. **Validate solvability + minimum move count** using a BFS/A* solver on the generated state:
   - If solver finds no solution → regenerate with new sub-seed.
   - If solver's optimal path is shorter than the level's minimum-required move count → increase `S` and regenerate (this is what prevents "level 1000 accidentally easy" — every level is solver-verified to meet a difficulty floor before it ships to the player).
5. Cache the validated level layout (don't regenerate at runtime after first solve — store in local level table or generate-once-on-first-launch-and-cache).

### 4.3 Why this guarantees "level 1000 is actually hard, not repeated":
- Seed-per-level means **no two levels are ever identical** (astronomically low collision chance).
- Solver-verified minimum move count means difficulty is **guaranteed monotonic**, not just "looks harder" — it's mathematically confirmed harder before the player ever sees it.
- Color count + tube buffer + shuffle depth all scale together, compounding difficulty rather than just one dimension.

### 4.4 Difficulty tiers (matches top competitor's proven 5-tier model)
- Beginner: L 1–100 (K 3–5)
- Advanced: L 101–300 (K 5–8)
- Master: L 301–600 (K 8–12)
- Expert: L 601–850 (K 12–14)
- Challenge: L 851–1000+ (K 14–16, zero buffer tubes)

---

## 5. Feature List (MVP → v1)

**MVP (must ship):**
- Core pour mechanic + seeded level generator + solver validator
- 1000 pre-validated levels (generate once, bundle as JSON)
- Undo (1 free/level), Restart, Add Tube (limited)
- Progress save (local, SharedPreferences/Hive)
- 5 difficulty tier labels shown on level-select map

**v1.1+ (post-launch):**
- Daily challenge (fixed seed shared across all users that day)
- Themed tube skins (unlockable, still monochrome-compatible — e.g., different tube shapes, not colorful themes)
- Rewarded-ad for extra undo/add-tube
- Level-select as a vertical scrolling path map (not grid) — matches current casual-game navigation trend

---

## 6. Tech Notes (Flutter)

- State management: Riverpod or Bloc (match your existing ToolKit app pattern for consistency)
- Level data: pre-generate + validate all 1000 levels via a one-time Dart script (run offline, export to `levels.json`, bundle as asset) — avoids runtime solver cost on low-end devices
- Animation: `AnimatedContainer` / custom `CustomPainter` for liquid pour, avoid heavy Lottie files to keep app size small
- Persistence: Hive (fast, lightweight, fits offline-first pattern you already use)

---

## 7. Monetization (reference only — finalize separately)
- Rewarded video for extra undo/hints
- Optional: Remove-ads IAP one-time purchase
- No hard paywalls before level 20 (retention-first, matches "46% discover via store, need good first-session experience" data)
