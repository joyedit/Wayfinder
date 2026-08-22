# Changelog

All notable changes to Wayfinder are documented here. Newest first.

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
