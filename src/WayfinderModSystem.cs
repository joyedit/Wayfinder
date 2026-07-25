using System;
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
            "cache"
        };

        private ICoreAPI api;
        
        // Track selected mark type per player (client-side only for now)
        public int SelectedMarkIndex { get; set; } = 0;

        public override void Start(ICoreAPI api)
        {
            this.api = api;
            base.Start(api);
            
            api.RegisterBlockClass("BlockWayfinderMark", typeof(BlockWayfinderMark));
            api.RegisterCollectibleBehaviorClass("Wayfinder", typeof(CollectibleBehaviorWayfinder));
        }

        public override void StartClientSide(ICoreClientAPI capi)
        {
            base.StartClientSide(capi);
            
            // Register hotkey for cycling mark types
            capi.Input.RegisterHotKey("wayfindercycle", "Cycle Wayfinder Mark", GlKeys.U, HotkeyType.GUIOrOtherControls, ctrlPressed: true);
            capi.Input.SetHotKeyHandler("wayfindercycle", OnCycleMarkHotkey);

            // Intercept right-click BEFORE ItemChisel gets it
            capi.Event.MouseDown += OnMouseDown;
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

            // Only when holding a chisel
            if (!IsChisel(activeSlot.Itemstack)) return;

            // Don't intercept if hammer is in offhand (let normal chiseling work)
            ItemSlot offhandSlot = player.InventoryManager.GetHotbarInventory()?[10];
            if (offhandSlot?.Itemstack?.Collectible?.Code?.Path?.Contains("hammer") == true)
            {
                return;
            }

            var blockSel = player.CurrentBlockSelection;
            if (blockSel == null) return;

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

            // Only cycle if holding a chisel
            if (!IsChisel(activeSlot.Itemstack)) return false;

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
                _ => markType
            };
        }

        public string GetCurrentMarkType()
        {
            return MarkTypes[SelectedMarkIndex];
        }
    }
}
