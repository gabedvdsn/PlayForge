namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Moves a float saved before its field became a number into the number, as the asset loads (FEAT-29). Unity can't
    /// read a saved float into a class, so a field that converts keeps a hidden legacy float under a new name, renamed
    /// from the old one, and its owner's <c>OnAfterDeserialize</c> moves it:
    /// <code>
    /// public ScalableFloat Threshold = 0f;
    ///
    /// [FormerlySerializedAs(nameof(Threshold)), SerializeField, HideInInspector]
    /// private float legacyThreshold = LegacyNumber.None;
    ///
    /// public void OnAfterDeserialize() => LegacyNumber.Move(ref legacyThreshold, ref Threshold);
    /// </code>
    /// It works in the editor and in a build. Window/PlayForge/Upgrade Assets re-saves a project's assets, so each is
    /// written in the new form; the legacy fields go a version after.
    /// </summary>
    public static class LegacyNumber
    {
        /// <summary>What a legacy float holds once it's moved, or when nothing was saved in it.</summary>
        public const float None = float.NaN;

        /// <summary>What a legacy whole number holds once it's moved, or when nothing was saved in it.</summary>
        public const int NoneInt = int.MinValue;

        /// <summary>Moves a saved float into the number, as a constant, and empties it. True when there was one to move.</summary>
        public static bool Move(ref float legacy, ref ScalableFloat number)
        {
            if (float.IsNaN(legacy)) return false;
            number = new ScalableFloat(legacy);
            legacy = None;
            return true;
        }

        /// <summary>Moves a saved whole number into the number, as a constant, and empties it. True when there was one to move.</summary>
        public static bool Move(ref int legacy, ref ScalableInt number)
        {
            if (legacy == NoneInt) return false;
            number = new ScalableInt(legacy);
            legacy = NoneInt;
            return true;
        }

        /// <summary>
        /// A scaler that was saved reading its own values per level, as a Clamped with nothing to clamp did, and reads a
        /// number in their place: its values, its level range and its behaviours move into a number of the By Level kind,
        /// which reads them as it did, and its own are left as a new scaler's (FEAT-29).
        /// </summary>
        public static ScalableFloat TakeTable(AbstractScaler scaler)
        {
            var table = new SimpleScaler
            {
                Configuration = scaler.Configuration,
                MaxLevel = scaler.MaxLevel,
                LevelValues = scaler.LevelValues,
                Interpolation = scaler.Interpolation,
                StretchOverLevels = scaler.StretchOverLevels,
                Scaling = scaler.Scaling,
                Behaviours = scaler.Behaviours ?? new System.Collections.Generic.List<AbstractScalerBehaviour>(),
            };
            scaler.LevelValues = new[] { 1f };
            scaler.Scaling = UnityEngine.AnimationCurve.Linear(0f, 1f, 1f, 1f);
            scaler.Behaviours = new System.Collections.Generic.List<AbstractScalerBehaviour>();
            return new ScalableFloat(table, 1f, EMagnitudeOperation.UseScaler);
        }
    }
}
