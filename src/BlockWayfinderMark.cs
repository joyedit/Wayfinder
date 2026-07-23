using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Wayfinder
{
    /// <summary>
    /// A thin mark block that attaches to cave walls.
    /// Similar to how torches or signs attach to surfaces.
    /// </summary>
    public class BlockWayfinderMark : Block
    {
        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            // Check if the target is a valid surface
            Block targetBlock = world.BlockAccessor.GetBlock(blockSel.Position);
            if (!WayfinderModSystem.IsMarkableSurface(targetBlock))
            {
                failureCode = "requirestone";
                return false;
            }

            // Get the position where the mark will go (adjacent air block)
            BlockPos markPos = blockSel.Position.AddCopy(blockSel.Face);
            Block blockAtMarkPos = world.BlockAccessor.GetBlock(markPos);
            
            // Must be air or replaceable
            if (blockAtMarkPos.Id != 0 && !blockAtMarkPos.IsReplacableBy(this))
            {
                failureCode = "notair";
                return false;
            }

            // Get the correct orientation variant
            string orientation = GetOrientationFromFace(blockSel.Face.Opposite);
            Block orientedBlock = world.GetBlock(CodeWithVariant("orientation", orientation));
            
            if (orientedBlock == null)
            {
                orientedBlock = this;
            }

            // Place the block
            world.BlockAccessor.SetBlock(orientedBlock.BlockId, markPos);
            
            // Play chisel sound
            world.PlaySoundAt(new AssetLocation("sounds/block/rock-hit-pickaxe"), markPos.X + 0.5, markPos.Y + 0.5, markPos.Z + 0.5, byPlayer);
            
            return true;
        }

        private string GetOrientationFromFace(BlockFacing attachFace)
        {
            // The orientation indicates which direction the mark faces (away from the wall)
            return attachFace.Code; // north, south, east, west
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neigbourPos)
        {
            base.OnNeighbourBlockChange(world, pos, neigbourPos);

            // Check if the supporting block was removed
            string orientation = Variant["orientation"];
            if (string.IsNullOrEmpty(orientation)) return;

            BlockFacing attachedFace;
            if (orientation.StartsWith("up"))
            {
                attachedFace = BlockFacing.DOWN;
            }
            else if (orientation.StartsWith("down"))
            {
                attachedFace = BlockFacing.UP;
            }
            else
            {
                attachedFace = BlockFacing.FromCode(orientation)?.Opposite;
                if (attachedFace == null) return;
            }

            BlockPos attachedPos = pos.AddCopy(attachedFace);
            Block attachedBlock = world.BlockAccessor.GetBlock(attachedPos);

            // If the wall we're attached to is gone, break the mark
            if (!WayfinderModSystem.IsMarkableSurface(attachedBlock))
            {
                world.BlockAccessor.SetBlock(0, pos);
                // Marks don't drop anything - they're just scratches in rock
            }
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            // Chiseled marks don't drop anything
            return new ItemStack[0];
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            string markType = Variant["type"];
            if (string.IsNullOrEmpty(markType)) return base.GetPlacedBlockInfo(world, pos, forPlayer);

            WayfinderModSystem mod = world.Api.ModLoader.GetModSystem<WayfinderModSystem>();
            return mod?.GetMarkDisplayName(markType) ?? markType;
        }
    }
}
