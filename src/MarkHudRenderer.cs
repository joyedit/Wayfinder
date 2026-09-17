using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Wayfinder
{
    /// <summary>
    /// Shows the selected mark type as a small icon while a chisel is held, so
    /// players can see what Ctrl+U has cycled to without reading chat.
    ///
    /// It sits just left of Pattern Mining's pattern grid (left of the hotbar)
    /// so the two indicators read as one row. Wayfinder doesn't depend on that
    /// mod: when it's installed we read its HUD size settings to find the grid's
    /// left edge; otherwise the icon takes the same spot using its defaults.
    /// </summary>
    public class MarkHudRenderer : IRenderer
    {
        // Pattern Mining's grid placement and defaults, mirrored from its
        // PatternHud / PatternMiningConfig. All in unscaled GUI units.
        private const string PatternMiningModId = "patternmining";
        private const string PatternMiningConfigFile = "patternmining.json";
        private const double GridOffsetLeftOfCenter = 420; // grid's right edge, left of center
        private const double GridOffsetFromBottom = 10;    // grid's bottom, up from screen bottom
        private const float DefaultHudScale = 1.6f;
        private const float DefaultDotSpacing = 0.72f;

        // Space between our icon and the grid.
        private const double Gap = 6;

        private readonly ICoreClientAPI capi;
        private readonly WayfinderModSystem mod;
        private readonly Dictionary<string, int> textureIds = new();

        // The grid's left edge (offset left of screen center) and its height,
        // which the icon matches so both indicators are the same size.
        private readonly double gridLeftOfCenter;
        private readonly double iconSize;

        public double RenderOrder => 1.0;
        public int RenderRange => 0;

        public MarkHudRenderer(ICoreClientAPI capi, WayfinderModSystem mod)
        {
            this.capi = capi;
            this.mod = mod;

            float hudScale = DefaultHudScale;
            float dotSpacing = DefaultDotSpacing;
            if (capi.ModLoader.IsModEnabled(PatternMiningModId))
            {
                try
                {
                    JsonObject config = capi.LoadModConfig(PatternMiningConfigFile);
                    if (config != null)
                    {
                        hudScale = config["HudScale"].AsFloat(DefaultHudScale);
                        dotSpacing = config["HudDotSpacing"].AsFloat(DefaultDotSpacing);
                    }
                }
                catch
                {
                    // Malformed config — Pattern Mining falls back to its defaults too.
                }
            }

            // Same clamps and pitch math as Pattern Mining's 3x3 grid.
            hudScale = GameMath.Clamp(hudScale, 0.5f, 4.0f);
            dotSpacing = GameMath.Clamp(dotSpacing, 0.4f, 2.0f);
            double pitch = GuiStyle.SmallFontSize * hudScale * dotSpacing;
            double gridSize = 3 * pitch;

            gridLeftOfCenter = GridOffsetLeftOfCenter + gridSize;
            iconSize = gridSize;
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (capi.HideGuis) return;

            ItemSlot activeSlot = capi.World?.Player?.InventoryManager?.ActiveHotbarSlot;
            if (!WayfinderModSystem.IsChisel(activeSlot?.Itemstack)) return;

            int textureId = GetTextureId(mod.GetCurrentMarkType());
            if (textureId == 0) return;

            // Layout is in GUI units; the render call wants pixels.
            float guiScale = RuntimeEnv.GUIScale;
            double screenWidth = capi.Render.FrameWidth / guiScale;
            double screenHeight = capi.Render.FrameHeight / guiScale;

            double x = screenWidth / 2.0 - gridLeftOfCenter - Gap - iconSize;
            double y = screenHeight - GridOffsetFromBottom - iconSize;

            capi.Render.Render2DTexture(textureId,
                (float)(x * guiScale), (float)(y * guiScale),
                (float)(iconSize * guiScale), (float)(iconSize * guiScale));
        }

        private int GetTextureId(string markType)
        {
            if (!textureIds.TryGetValue(markType, out int id))
            {
                // Loaded through the game's texture cache, which owns and frees it.
                var location = new AssetLocation("wayfinder", $"textures/hud/{markType}.png");
                id = capi.Assets.Exists(location) ? capi.Render.GetOrLoadTexture(location) : 0;
                textureIds[markType] = id;
            }
            return id;
        }

        public void Dispose()
        {
        }
    }
}
