using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Wayfinder
{
    public class WayfinderModSystem : ModSystem
    {
        public static readonly string[] MarkTypes = new string[]
        {
            "arrow-up",
            "arrow-down", 
            "arrow-left",
            "arrow-right",
            "explored",
            "danger",
            "resources",
            "home",
            "water",
            "deadend",
            "cache",
            "translocator"
        };

        private ICoreAPI api;
        private MarkHudRenderer markHud;
        
        // Track selected mark type per player (client-side only for now)
        public int SelectedMarkIndex { get; set; } = 0;

        public override void Start(ICoreAPI api)
        {
            this.api = api;
            base.Start(api);
            
            api.RegisterBlockClass("BlockWayfinderMark", typeof(BlockWayfinderMark));
            api.RegisterCollectibleBehaviorClass("Wayfinder", typeof(CollectibleBehaviorWayfinder));
        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            base.AssetsFinalize(api);

            // Chisels get the help behavior from a JSON patch. The primitive tools
            // are attached here instead: stone.json keys its behaviors by type with
            // a "*" catch-all, so a patch can't target just chert and obsidian.
            foreach (Item item in api.World.Items)
            {
                if (item?.Code == null || !IsPrimitiveMarker(item.Code.Path)) continue;
                if (item.HasBehavior<CollectibleBehaviorWayfinder>()) continue;

                item.CollectibleBehaviors = item.CollectibleBehaviors
                    .Append(new CollectibleBehaviorWayfinder(item))
                    .ToArray();
            }
        }

        public override void StartClientSide(ICoreClientAPI capi)
        {
            base.StartClientSide(capi);
            
            // Register hotkey for cycling mark types
            capi.Input.RegisterHotKey("wayfindercycle", "Cycle Wayfinder Mark", GlKeys.U, HotkeyType.GUIOrOtherControls, ctrlPressed: true);
            capi.Input.SetHotKeyHandler("wayfindercycle", OnCycleMarkHotkey);

            // Intercept right-click BEFORE ItemChisel gets it
            capi.Event.MouseDown += OnMouseDown;

            // Selected-mark icon beside the hotbar while a marking tool is held
            markHud = new MarkHudRenderer(capi, this);
            capi.Event.RegisterRenderer(markHud, EnumRenderStage.Ortho, "wayfindermarkhud");
        }

        public override void Dispose()
        {
            if (markHud != null)
            {
                (api as ICoreClientAPI)?.Event.UnregisterRenderer(markHud, EnumRenderStage.Ortho);
                markHud = null;
            }
            base.Dispose();
        }

        private void OnMouseDown(MouseEvent e)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            if (capi == null) return;
            if (e.Button != EnumMouseButton.Right) return;

            IPlayer player = capi.World.Player;
            if (player == null) return;

            // Must be sneaking
            if (!player.Entity.Controls.Sneak) return;

            ItemSlot activeSlot = player.InventoryManager.ActiveHotbarSlot;
            if (activeSlot?.Itemstack == null) return;

            // Only when holding a chisel or a primitive marking tool
            ItemStack tool = activeSlot.Itemstack;
            if (!IsMarkingTool(tool)) return;

            // Don't intercept if hammer is in offhand (let normal chiseling work)
            if (IsChisel(tool))
            {
                ItemSlot offhandSlot = player.InventoryManager.GetHotbarInventory()?[10];
                if (offhandSlot?.Itemstack?.Collectible?.Code?.Path?.Contains("hammer") == true)
                {
                    return;
                }
            }

            var blockSel = player.CurrentBlockSelection;
            if (blockSel == null) return;

            // Sneak+right-click on a floor with flint or stone is vanilla knapping /
            // loose-stone placement, so those tools only mark walls and ceilings.
            if (blockSel.Face == BlockFacing.UP && !CanMarkFloors(tool)) return;

            // Check if targeting markable rock
            Block targetBlock = capi.World.BlockAccessor.GetBlock(blockSel.Position);
            if (!IsMarkableSurface(targetBlock)) return;

            // Get position for the mark
            BlockPos markPos = blockSel.Position.AddCopy(blockSel.Face);
            Block blockAtMarkPos = capi.World.BlockAccessor.GetBlock(markPos);

            // Already marked?
            if (blockAtMarkPos.Code?.Path?.StartsWith("wayfindermark") == true)
            {
                capi.ShowChatMessage("There's already a mark here.");
                e.Handled = true;
                return;
            }

            // Must be air
            if (blockAtMarkPos.Id != 0 && blockAtMarkPos.Code?.Path != "air") return;

            // Place the mark
            string markType = GetCurrentMarkType();
            string orientation = blockSel.Face.Code;
            if (blockSel.Face == BlockFacing.UP || blockSel.Face == BlockFacing.DOWN)
            {
                BlockFacing yawFacing = BlockFacing.HorizontalFromYaw(player.Entity.Pos.Yaw);
                orientation += yawFacing.Code.Substring(0, 1);
            }
            string blockCode = $"wayfinder:wayfindermark-{markType}-{orientation}";
            Block markBlock = capi.World.GetBlock(new AssetLocation(blockCode));

            if (markBlock == null)
            {
                capi.Logger.Warning($"[Wayfinder] Could not find block: {blockCode}");
                return;
            }

            capi.World.BlockAccessor.SetBlock(markBlock.BlockId, markPos);
            capi.World.PlaySoundAt(new AssetLocation("sounds/block/rock-hit-pickaxe"),
                markPos.X + 0.5, markPos.Y + 0.5, markPos.Z + 0.5, player);

            string displayName = GetMarkDisplayName(markType);
            capi.ShowChatMessage($"Marked: {displayName}");

            // Consume the event so ItemChisel never sees it
            e.Handled = true;
        }

        private bool OnCycleMarkHotkey(KeyCombination comb)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            if (capi == null) return false;

            IPlayer player = capi.World.Player;
            if (player == null) return false;

            ItemSlot activeSlot = player.InventoryManager.ActiveHotbarSlot;
            if (activeSlot?.Itemstack == null) return false;

            // Only cycle if holding a marking tool
            if (!IsMarkingTool(activeSlot.Itemstack)) return false;

            SelectedMarkIndex = (SelectedMarkIndex + 1) % MarkTypes.Length;
            string markName = GetMarkDisplayName(MarkTypes[SelectedMarkIndex]);
            capi.ShowChatMessage($"Mark type: {markName}");
            
            return true;
        }

        public static bool IsChisel(ItemStack stack)
        {
            if (stack?.Collectible == null) return false;
            string code = stack.Collectible.Code?.Path ?? "";
            return code.Contains("chisel");
        }

        /// <summary>
        /// Anything that can scratch a mark: chisels, plus pre-copper-age tools
        /// (flint, chert, obsidian, antler).
        /// </summary>
        public static bool IsMarkingTool(ItemStack stack)
        {
            if (stack?.Collectible == null) return false;
            return IsChisel(stack) || IsPrimitiveMarker(stack.Collectible.Code?.Path);
        }

        public static bool IsPrimitiveMarker(string code)
        {
            if (code == null) return false;
            return code == "flint" ||
                   code == "stone-chert" ||
                   code == "stone-obsidian" ||
                   code.StartsWith("antler-");
        }

        /// <summary>
        /// Flint and stones already use Sneak+Right-Click on the ground (knapping,
        /// loose stones), so they can't claim floors. Chisels and antlers can.
        /// </summary>
        public static bool CanMarkFloors(ItemStack stack)
        {
            string code = stack?.Collectible?.Code?.Path ?? "";
            return IsChisel(stack) || code.StartsWith("antler-");
        }

        public static bool IsMarkableSurface(Block block)
        {
            if (block == null) return false;
            string code = block.Code?.Path ?? "";
            
            // Allow marking on rock and stone surfaces
            return code.StartsWith("rock-") || 
                   code.StartsWith("stone-") ||
                   code.Contains("granite") ||
                   code.Contains("basalt") ||
                   code.Contains("limestone") ||
                   code.Contains("sandstone") ||
                   code.Contains("slate") ||
                   code.Contains("marble") ||
                   code.Contains("andesite") ||
                   code.Contains("peridotite") ||
                   code.Contains("obsidian");
        }

        public string GetMarkDisplayName(string markType)
        {
            return markType switch
            {
                "arrow-up" => "Arrow Up ↑",
                "arrow-down" => "Arrow Down ↓",
                "arrow-left" => "Arrow Left ←",
                "arrow-right" => "Arrow Right →",
                "explored" => "Explored ✓",
                "danger" => "Danger ⚠",
                "resources" => "Resources ⛏",
                "home" => "Home ⌂",
                "water" => "Water ≈",
                "deadend" => "Dead End ⊥",
                "cache" => "Cache ◆",
                "translocator" => "Translocator ◎",
                _ => markType
            };
        }

        public string GetCurrentMarkType()
        {
            return MarkTypes[SelectedMarkIndex];
        }
    }
}
