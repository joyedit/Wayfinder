# Changelog

All notable changes to Wayfinder are documented here. Newest first.

## 1.5.0 — 2026-09-18

### Added
- **Stone Age marking tools.** You don't need a copper chisel to leave marks
  anymore: Sneak+Right-Click rock with **flint**, **chert**, **obsidian**, or
  any **antler** to scratch the selected mark. Ctrl+U cycling and the
  selected-mark icon work with these too.
  - Flint, chert, and obsidian mark **walls and ceilings only**. Their
    Sneak+Right-Click on the ground is vanilla knapping and loose-stone
    placement, which keeps working as before.
  - Antlers have no ground action, so like chisels they can mark floors too.

## 1.4.0 — 2026-09-17

### Added
- **Translocator ◎ mark** — a chiseled spiral closing into a ring, for marking
  the way to a translocator.
- **Selected-mark icon.** While you hold a chisel, a small icon of the selected
  mark sits to the left of the hotbar and changes as you cycle with Ctrl+U. If
  Pattern Mining is installed, it lines up just left of that mod's pattern grid
  and matches its size (following its `HudScale` / `HudDotSpacing` settings).

## 1.3.1 — 2026-08-22

### Fixed
- **Chisels now show their Wayfinder interaction help again.** The chisel patch
  targeted `/behaviors/0`, but vanilla declares the chisel's behaviors under
  `behaviorsByType` — there is no `behaviors` array at the root — so the patch
  failed on every world load and the behavior was never attached. Placing and
  cycling marks always worked (that runs through the mod's own input handling),
  but the held-item help text never appeared, so the controls were undiscoverable
  in game. Retargeted to both `behaviorsByType` branches, so every chisel metal
  is covered.

### Changed
- Compatibility release for Vintage Story 1.22.7. Rebuilt against the 1.22.7
  assemblies.

## 1.3.0 — 2026-07-24

- Marks now render as bare etched grooves directly on the rock — no more
  stone panel background. Textures redrawn at 32×32 with a hand-chiseled
  look (jittered lines, recessed shadow, chipped-edge highlight) over a
  transparent background so the actual wall shows through.
- Four new mark types: Home ⌂, Water ≈, Dead End ⊥, and Cache ◆.

## 1.2.1 — 2026-07-23

- Compatibility release for Vintage Story 1.22.5. Rebuilt against the 1.22.5
  assemblies; no code changes.

## 1.2.0

- Last release before this changelog was introduced.
