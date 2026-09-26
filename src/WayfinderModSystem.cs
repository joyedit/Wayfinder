using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
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

        private const string ChannelName = "wayfinder";

        /// <summary>
        /// How far from the clicked rock a player may be when the mark request
        /// lands, squared. Generous next to the ~4.5 block picking range, so lag
        /// never eats a legitimate mark.
        /// </summary>
        private const float MaxMarkDistanceSquared = 100f;

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

            // Registered on both sides here so the channel's message types line up.
            api.Network.RegisterChannel(ChannelName)
                .RegisterMessageType<PlaceMarkPacket>();
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

        public override void StartServerSide(ICoreServerAPI sapi)
        {
            base.StartServerSide(sapi);

            sapi.Network.GetChannel(ChannelName)
                .SetMessageHandler<PlaceMarkPacket>(OnPlaceMarkPacket);
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

            // Ask the server to place it. Setting the block here instead would only
            // touch the client's copy of the chunk: the server would never learn
            // about the mark, so it'd never be saved and would vanish on relog.
            // The sound and the confirmation come back from the server with it.
            int yawFacingIndex = -1;
            if (blockSel.Face == BlockFacing.UP || blockSel.Face == BlockFacing.DOWN)
            {
                yawFacingIndex = BlockFacing.HorizontalFromYaw(player.Entity.Pos.Yaw).Index;
            }

            capi.Network.GetChannel(ChannelName).SendPacket(new PlaceMarkPacket
            {
                X = blockSel.Position.X,
                Y = blockSel.Position.Y,
                Z = blockSel.Position.Z,
                Dimension = blockSel.Position.dimension,
                FaceIndex = blockSel.Face.Index,
                YawFacingIndex = yawFacingIndex,
                MarkIndex = SelectedMarkIndex
            });

            // Consume the event so ItemChisel never sees it
            e.Handled = true;
        }

        /// <summary>
        /// Authoritative placement. The client has already checked all of this, but
        /// it checks it on its own copy of the world and nothing stops a crafted
        /// packet, so every condition is re-tested here against server state.
        /// </summary>
        private void OnPlaceMarkPacket(IServerPlayer fromPlayer, PlaceMarkPacket packet)
        {
            ICoreServerAPI sapi = api as ICoreServerAPI;
            if (sapi == null || fromPlayer?.Entity == null) return;

            if (packet.MarkIndex < 0 || packet.MarkIndex >= MarkTypes.Length) return;
            if (packet.FaceIndex < 0 || packet.FaceIndex >= BlockFacing.ALLFACES.Length) return;

            BlockFacing face = BlockFacing.ALLFACES[packet.FaceIndex];
            BlockPos targetPos = new BlockPos(packet.X, packet.Y, packet.Z, packet.Dimension);

            ItemStack tool = fromPlayer.InventoryManager?.ActiveHotbarSlot?.Itemstack;
            if (!IsMarkingTool(tool)) return;
            if (face == BlockFacing.UP && !CanMarkFloors(tool)) return;

            float distSq = fromPlayer.Entity.Pos.XYZ.SquareDistanceTo(
                targetPos.X + 0.5, targetPos.Y + 0.5, targetPos.Z + 0.5);
            if (distSq > MaxMarkDistanceSquared) return;

            Block targetBlock = sapi.World.BlockAccessor.GetBlock(targetPos);
            if (!IsMarkableSurface(targetBlock)) return;

            BlockPos markPos = targetPos.AddCopy(face);
            Block blockAtMarkPos = sapi.World.BlockAccessor.GetBlock(markPos);
            if (blockAtMarkPos.Id != 0 && blockAtMarkPos.Code?.Path != "air") return;

            string markType = MarkTypes[packet.MarkIndex];
            string orientation = face.Code;
            if (face == BlockFacing.UP || face == BlockFacing.DOWN)
            {
                if (packet.YawFacingIndex < 0 || packet.YawFacingIndex >= BlockFacing.ALLFACES.Length) return;
                BlockFacing yawFacing = BlockFacing.ALLFACES[packet.YawFacingIndex];
                if (!yawFacing.IsHorizontal) return;
                orientation += yawFacing.Code.Substring(0, 1);
            }

            string blockCode = $"wayfinder:wayfindermark-{markType}-{orientation}";
            Block markBlock = sapi.World.GetBlock(new AssetLocation(blockCode));

            if (markBlock == null)
            {
                sapi.Logger.Warning($"[Wayfinder] Could not find block: {blockCode}");
                return;
            }

            sapi.World.BlockAccessor.SetBlock(markBlock.BlockId, markPos);

            // null, not fromPlayer: that argument is the player to skip, and nothing
            // plays this sound client-side any more, so the marker must hear it too.
            sapi.World.PlaySoundAt(new AssetLocation("sounds/block/rock-hit-pickaxe"),
                markPos.X + 0.5, markPos.Y + 0.5, markPos.Z + 0.5, (IPlayer)null, true, 32f);

            fromPlayer.SendMessage(GlobalConstants.GeneralChatGroup,
                $"Marked: {GetMarkDisplayName(markType)}", EnumChatType.Notification);
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
