#!/bin/bash
# Builds the selected-mark HUD icons from the block mark textures.
#
# The block textures are dark grooves meant to sit on rock, which vanish against
# a dark cave on screen. For the HUD we keep only the groove's shape, recolor it
# to the game's default dialog text color (#e9ddce, the same as Pattern Mining's
# dots) and back it with a soft dark outline so it reads over any background.
#
# Re-run after adding or redrawing a mark texture. Requires ImageMagick.
set -e
cd "$(dirname "$0")/.."

SRC=assets/wayfinder/textures/block
OUT=assets/wayfinder/textures/hud
mkdir -p "$OUT"

for f in "$SRC"/*.png; do
    name=$(basename "$f")
    [ "$name" = "mark-back.png" ] && continue

    # Groove pixels are dark; the chipped-edge highlight is light and dropped.
    convert "$f" \
        -channel A -fx "r<0.5 ? min(1, a*1.15) : 0" +channel \
        -fill '#e9ddce' -colorize 100 \
        -filter point -resize 200% \
        \( +clone -fill black -colorize 100 \
           -channel A -morphology Dilate Disk:1.5 -evaluate multiply 0.6 +channel \) \
        +swap -composite \
        "$OUT/$name"
    echo "  $OUT/$name"
done
