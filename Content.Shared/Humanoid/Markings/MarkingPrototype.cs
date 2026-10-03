using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Humanoid.Markings
{
    [Prototype]
    public sealed partial class MarkingPrototype : IPrototype
    {
        [IdDataField]
        public string ID { get; private set; } = "uwu";

        public string Name { get; private set; } = default!;

        [DataField("bodyPart", required: true)]
        public HumanoidVisualLayers BodyPart { get; private set; } = default!;

        [DataField("markingCategory", required: true)]
        public MarkingCategories MarkingCategory { get; private set; } = default!;

        [DataField("speciesRestriction")]
        public List<string>? SpeciesRestrictions { get; private set; }

        /// <summary>
        /// Palmtree/Floof: species "kinds" this marking is additionally allowed for.
        /// Lets a marking be shared across groups of species without listing each one.
        /// </summary>
        [DataField("kindAllowance")]
        public List<string>? KindAllowance { get; private set; }

        [DataField("sexRestriction")]
        public Sex? SexRestriction { get; private set; }

        [DataField("followSkinColor")]
        public bool FollowSkinColor { get; private set; } = false;

        [DataField("forcedColoring")]
        public bool ForcedColoring { get; private set; } = false;

        [DataField("coloring")]
        public MarkingColors Coloring { get; private set; } = new();

        /// <summary>
        /// Do we need to apply any displacement maps to this marking? Set to false if your marking is incompatible
        /// with a standard human doll, and is used for some special races with unusual shapes
        /// </summary>
        [DataField]
        public bool CanBeDisplaced { get; private set; } = true;

        [DataField("sprites", required: true)]
        public List<SpriteSpecifier> Sprites { get; private set; } = default!;

        /// <summary>
        /// Palmtree/Floof: allows specific marking sprites to be drawn into an arbitrary humanoid
        /// layer, e.g. breasts that render behind the body while facing north.
        /// Dictionary: sprite state -> humanoid visual layer name.
        /// </summary>
        [DataField("layering")]
        public Dictionary<string, string>? Layering { get; private set; }

        /// <summary>
        /// Palmtree/Floof: links one sprite's color to another (format: child -> parent).
        /// Linked sprites are hidden from the color picker and inherit the parent's color.
        /// </summary>
        [DataField("colorLinks")]
        public Dictionary<string, string>? ColorLinks { get; private set; }

        // impstation edit - allow markings to support shaders
		[DataField("shader")]
		public string? Shader { get; private set; } = null;
        // end impstation edit
        public Marking AsMarking()
        {
            return new Marking(ID, Sprites.Count);
        }
    }
}
