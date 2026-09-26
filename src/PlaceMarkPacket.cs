using ProtoBuf;

namespace Wayfinder
{
    /// <summary>
    /// Client → server request to etch a mark. Carries only the clicked block, the
    /// clicked face and indices into the mod's own tables — the server derives the
    /// mark position and block code itself, so a crafted packet can't name an
    /// arbitrary block.
    /// </summary>
    [ProtoContract]
    public class PlaceMarkPacket
    {
        /// <summary>The rock block that was clicked, not the mark position.</summary>
        [ProtoMember(1)] public int X;
        [ProtoMember(2)] public int Y;
        [ProtoMember(3)] public int Z;
        [ProtoMember(4)] public int Dimension;

        /// <summary>Index into <see cref="Vintagestory.API.MathTools.BlockFacing.ALLFACES"/>.</summary>
        [ProtoMember(5)] public int FaceIndex;

        /// <summary>
        /// Horizontal facing the mark should read along, for floor and ceiling marks.
        /// -1 for wall marks, which take their orientation from the face alone.
        /// </summary>
        [ProtoMember(6)] public int YawFacingIndex = -1;

        /// <summary>Index into <see cref="WayfinderModSystem.MarkTypes"/>.</summary>
        [ProtoMember(7)] public int MarkIndex;
    }
}
