using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Wayfinder
{
    /// <summary>
    /// Behavior added to chisels — only used for interaction help tooltips.
    /// Actual placement is handled by WayfinderModSystem via mouse event interception.
    /// </summary>
    public class CollectibleBehaviorWayfinder : CollectibleBehavior
    {
        public CollectibleBehaviorWayfinder(CollectibleObject collObj) : base(collObj)
        {
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot, ref EnumHandling handling)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction
                {
                    ActionLangCode = "wayfinder:heldhelp-placemark",
                    MouseButton = EnumMouseButton.Right
                },
                new WorldInteraction
                {
                    ActionLangCode = "wayfinder:heldhelp-cyclemark",
                    HotKeyCode = "wayfindercycle"
                }
            };
        }
    }
}
