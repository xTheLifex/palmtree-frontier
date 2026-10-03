using Robust.Shared.Serialization;

namespace Content.Shared.Humanoid.Markings
{
    [Serializable, NetSerializable]
    public enum MarkingCategories : byte
    {
        Special,
        Hair,
        FacialHair,
        Head,
        HeadTop,
        HeadSide,
        Snout,
        SnoutCover,
        Chest,
        NeckFluff, // Palmtree/TheDen
        UndergarmentTop,
        UndergarmentBottom,
        Genital, // Palmtree: genital markings category
        Arms,
        Legs,
        Tail,
        TailExtras, // Palmtree/Starlight
        Overlay,
        // Palmtree/Floof: base-layer replacement markings
        BaseChest,
        BaseHead,
        BaseLArm,
        BaseLFoot,
        BaseLHand,
        BaseLLeg,
        BaseRArm,
        BaseRFoot,
        BaseRHand,
        BaseRLeg,
        BaseArms,
        BaseLegs,
    }

    public static class MarkingCategoriesConversion
    {
        /// <summary>
        /// Palmtree/Floof: converts a base-marking category to the base layer it replaces.
        /// </summary>
        public static bool Category2Layer(
            MarkingCategories category,
            HumanoidVisualLayers hvLayers,
            out HumanoidVisualLayers baseLayerToHide)
        {
            baseLayerToHide = HumanoidVisualLayers.Disregard;
            switch (category)
            {
                case MarkingCategories.BaseChest:
                    baseLayerToHide = HumanoidVisualLayers.Chest;
                    return true;
                case MarkingCategories.BaseHead:
                    baseLayerToHide = HumanoidVisualLayers.Head;
                    return true;
                case MarkingCategories.BaseArms
                    or MarkingCategories.BaseLegs
                    when hvLayers
                        is HumanoidVisualLayers.LArm
                        or HumanoidVisualLayers.LHand
                        or HumanoidVisualLayers.RArm
                        or HumanoidVisualLayers.RHand
                        or HumanoidVisualLayers.LLeg
                        or HumanoidVisualLayers.LLegBehind
                        or HumanoidVisualLayers.LFoot
                        or HumanoidVisualLayers.LFootBehind
                        or HumanoidVisualLayers.RLeg
                        or HumanoidVisualLayers.RLegBehind
                        or HumanoidVisualLayers.RFoot
                        or HumanoidVisualLayers.RFootBehind:
                    baseLayerToHide = hvLayers;
                    return true;
                default:
                    return false;
            }
        }

        public static MarkingCategories FromHumanoidVisualLayers(HumanoidVisualLayers layer)
        {
            return layer switch
            {
                HumanoidVisualLayers.Special => MarkingCategories.Special,
                HumanoidVisualLayers.Hair => MarkingCategories.Hair,
                HumanoidVisualLayers.FacialHair => MarkingCategories.FacialHair,
                HumanoidVisualLayers.Head => MarkingCategories.Head,
                HumanoidVisualLayers.HeadTop => MarkingCategories.HeadTop,
                HumanoidVisualLayers.HeadSide => MarkingCategories.HeadSide,
                HumanoidVisualLayers.Snout => MarkingCategories.Snout,
                HumanoidVisualLayers.Chest => MarkingCategories.Chest,
                HumanoidVisualLayers.NeckFluff => MarkingCategories.NeckFluff, // Palmtree/TheDen
                HumanoidVisualLayers.UndergarmentTop => MarkingCategories.UndergarmentTop,
                HumanoidVisualLayers.UndergarmentBottom => MarkingCategories.UndergarmentBottom,
                HumanoidVisualLayers.Genital => MarkingCategories.Genital, // Palmtree
                HumanoidVisualLayers.RArm => MarkingCategories.Arms,
                HumanoidVisualLayers.LArm => MarkingCategories.Arms,
                HumanoidVisualLayers.RHand => MarkingCategories.Arms,
                HumanoidVisualLayers.LHand => MarkingCategories.Arms,
                HumanoidVisualLayers.LLeg => MarkingCategories.Legs,
                HumanoidVisualLayers.LLegBehind => MarkingCategories.Legs,
                HumanoidVisualLayers.RLeg => MarkingCategories.Legs,
                HumanoidVisualLayers.RLegBehind => MarkingCategories.Legs,
                HumanoidVisualLayers.LFoot => MarkingCategories.Legs,
                HumanoidVisualLayers.LFootBehind => MarkingCategories.Legs,
                HumanoidVisualLayers.RFoot => MarkingCategories.Legs,
                HumanoidVisualLayers.RFootBehind => MarkingCategories.Legs,
                HumanoidVisualLayers.TailExtras => MarkingCategories.TailExtras, // Palmtree/Starlight
                HumanoidVisualLayers.Tail => MarkingCategories.Tail,
                HumanoidVisualLayers.RArmExtension => MarkingCategories.Arms, // Frontier: species-specific layer
                HumanoidVisualLayers.LArmExtension => MarkingCategories.Arms, // Frontier: species-specific layer
                _ => MarkingCategories.Overlay
            };
        }
    }
}
