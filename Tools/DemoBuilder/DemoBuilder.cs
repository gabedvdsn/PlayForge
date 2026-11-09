using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FarEmerald.PlayForge.Extended.Editor;
using UnityEditor;
using UnityEngine;
using ForgeAttribute = FarEmerald.PlayForge.Attribute;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Samples.Demo.Building
{
    /// <summary>
    /// Builds the Demo sample's assets and scene, in a project that has the sample copied in at <see cref="Root"/>: its
    /// tags, attributes, attribute sets, effects, abilities, items, entities, prefabs, roster, UI and scene, each made the
    /// way its inspector fills it in. It updates each asset in place where it exists, so a rebuild keeps every GUID. Run
    /// it with -executeMethod FarEmerald.PlayForge.Samples.Demo.Building.DemoBuilder.Build, then copy the folder back.
    /// A tool for the demo's authors, not part of the sample.
    /// </summary>
    public static partial class DemoBuilder
    {
        public const string Root = "Assets/PlayForgeDemo";

        public static void Build()
        {
            try
            {
                BuildAll();
                AssetDatabase.SaveAssets();
                Debug.Log($"[DemoBuilder] Built the demo: {made} assets.");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[DemoBuilder] Failed: {e}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        [MenuItem("Window/PlayForge/Demo/Rebuild the Demo (Authors)")]
        private static void BuildFromMenu() => Build();

        private static int made;

        private static void BuildAll()
        {
            made = 0;
            MakeAttributes();
            MakeScalers();
            MakePrefabsForCues();
            MakeTemplates();
            MakeAttributeSets();
            MakeSharedEffects();
            MakeVanguard();
            MakeRanger();
            MakePyromancer();
            MakeShade();
            MakeOracle();
            MakePassive();
            MakeItems();
            MakeEntities();
            MakeRoster();
            MakeUI();
            MakeScene();
            AssetDatabase.SaveAssets();
            Editor.DemoIcons.MakeAll(Root);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Saving, in place
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Saves an asset at a path under the root, or, where one is there, copies the new one's fields into it, so its
        /// GUID and every reference to it hold. Returns the asset that's saved.
        /// </summary>
        private static T Save<T>(string relative, T fresh) where T : ScriptableObject
        {
            string path = $"{Root}/{relative}.asset";
            Folder(Path.GetDirectoryName(path));
            fresh.name = Path.GetFileNameWithoutExtension(path);
            made++;
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(fresh, path);
                return fresh;
            }
            EditorUtility.CopySerialized(fresh, existing);
            existing.name = fresh.name;
            Object.DestroyImmediate(fresh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        /// <summary>Marks an asset changed after its fields were set on the saved asset itself.</summary>
        private static T Dirty<T>(T asset) where T : Object
        {
            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>A child of a template: a copy of it, which takes it as its template.</summary>
        private static T ChildOf<T>(T template) where T : BaseForgeAsset
        {
            var child = Object.Instantiate(template);
            child.SetTemplate(template);
            return child;
        }

        /// <summary>After a child is saved, its overrides are what differs from its template, as its inspector records them.</summary>
        private static T Settle<T>(T child) where T : BaseForgeAsset
        {
            if (!child.HasTemplate) return child;
            ForgeTemplatePropagator.CaptureInitialListSnapshots(child);
            ForgeTemplatePropagator.UpdateOverrides(child);
            return Dirty(child);
        }

        private static void Folder(string path)
        {
            path = path.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) Folder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>A prefab under the root's Prefabs folder, made from an object built here, replacing one there, so its GUID holds.</summary>
        private static T Prefab<T>(string name, Action<GameObject, T> fill) where T : Component
        {
            string path = $"{Root}/Prefabs/{name}.prefab";
            Folder(Path.GetDirectoryName(path));
            var host = new GameObject(name);
            var component = host.AddComponent<T>();
            fill(host, component);
            var saved = PrefabUtility.SaveAsPrefabAsset(host, path);
            Object.DestroyImmediate(host);
            made++;
            return saved.GetComponent<T>();
        }

        /// <summary>A look made of a primitive, as a child of an object being built.</summary>
        private static DemoShape Look(GameObject host, PrimitiveType shape, Vector3 size, Vector3 offset, Color color, float pulse = 0f)
        {
            var look = new GameObject("Look");
            look.transform.SetParent(host.transform, false);
            var made = look.AddComponent<DemoShape>();
            made.Shape = shape;
            made.Size = size;
            made.Offset = offset;
            made.Color = color;
            made.Pulse = pulse;
            return made;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Tags
        // ═══════════════════════════════════════════════════════════════════════════

        private static Tag T(string name) => Tag.Generate(name);

        private static string Key(string name) => new string(name.Where(char.IsLetterOrDigit).ToArray());

        private static readonly Tag PlayerSide = T("Demo.Side.Player"), OpponentSide = T("Demo.Side.Opponent");
        private static readonly Tag Physical = T("Damage.Physical"), Magic = T("Damage.Magic"), Pure = T("Damage.Pure");
        private static readonly Tag AttackHit = T("Damage.Attack"), AbilityHit = T("Damage.Ability"), DrainHit = T("Damage.Drain");
        private static readonly Tag Stunned = T("State.Stunned"), Silenced = T("State.Silenced"), Invisible = T("State.Invisible");
        private static readonly Tag Untargetable = T("State.Untargetable"), Channeling = T("State.Channeling");
        private static readonly Tag Empowered = T("State.Empowered"), Attacking = T("State.Attacking");
        private static readonly Tag Burning = T("Status.Burning"), Marked = T("Status.Marked"), Hexed = T("Status.Hexed"), Slowed = T("Status.Slowed");
        private static readonly Tag Buff = T("Kind.Buff"), Debuff = T("Kind.Debuff");
        private static readonly Tag Spell = T("Ability.Spell"), AttackKind = T("Ability.Attack"), ItemKind = T("Ability.Item");
        private static readonly Tag StatItemTag = T("Item.Stat");
        private static readonly Tag AttackRange = T("Demo.AttackRange"), CastOrder = T("Demo.CastOrder");
        private static readonly Tag Reach = T("Demo.Reach"), CastWhen = T("Demo.CastWhen"), Difficulty = T("Demo.Difficulty");
        private static readonly Tag FireRadius = T("Demo.Fire.Radius"), FireLifetime = T("Demo.Fire.Lifetime"), FireInterval = T("Demo.Fire.Interval"), FireHeat = T("Demo.Fire.Heat");

        private static AvoidRequireTagGroup Avoid(params Tag[] tags) => new()
        {
            AvoidTags = tags.Select(tag => new TagQuery { Tag = tag, Operator = EComparisonOperator.GreaterThan, Magnitude = 0 }).ToList(),
            RequireTags = new List<TagQuery>(),
        };

        private static AvoidRequireTagGroup Require(params Tag[] tags) => new()
        {
            AvoidTags = new List<TagQuery>(),
            RequireTags = tags.Select(tag => new TagQuery { Tag = tag, Operator = EComparisonOperator.GreaterThan, Magnitude = 0 }).ToList(),
        };

        private static EffectTagRequirements NoRequirements() => new()
        {
            ApplicationRequirements = Avoid(),
            OngoingRequirements = Avoid(),
            RemovalRequirements = Avoid(),
        };

        private static DataWrapper Local(Tag key, float value) => new() { Key = key, Type = EDataWrapperType.Float, floatValue = value };
        private static DataWrapper Local(Tag key, int value) => new() { Key = key, Type = EDataWrapperType.Int, intValue = value };
        private static DataWrapper Local(Tag key, string value) => new() { Key = key, Type = EDataWrapperType.String, stringValue = value };
        private static DataWrapper Local(Tag key, MonoBehaviour value) => new() { Key = key, Type = EDataWrapperType.MonoBehaviour, monoBehaviourValue = value };

        // ═══════════════════════════════════════════════════════════════════════════
        // Scalers
        // ═══════════════════════════════════════════════════════════════════════════

        private static T Named<T>(T scaler, string name) where T : AbstractScaler
        {
            scaler.Name = name;
            return scaler;
        }

        /// <summary>A scaler's values per level, read exactly: level 1 the first, past the end the last.</summary>
        private static T Table<T>(T scaler, params float[] values) where T : AbstractScaler
        {
            scaler.LevelValues = values;
            scaler.MaxLevel = values.Length;
            scaler.StretchOverLevels = false;
            scaler.Interpolation = EScalerInterpolation.Linear;
            scaler.Configuration = ELevelConfig.LockToLevelProvider;
            scaler.Behaviours ??= new List<AbstractScalerBehaviour>();
            scaler.RegenerateCurve();
            return scaler;
        }

        private static SimpleScaler Levels(params float[] values) => Table(new SimpleScaler(), values);

        private static ConstantScaler Constant(float value) => new() { Value = value, Behaviours = new List<AbstractScalerBehaviour>() };

        private static ScalerGroup Group(params (AbstractScaler scaler, ECalculationOperation operation)[] members) => new()
        {
            Calculations = members.Select(member => new MagnitudeModifierGroupMember { Calculation = member.scaler, RelativeOperation = member.operation }).ToArray(),
            Behaviours = new List<AbstractScalerBehaviour>(),
        };

        private const ECalculationOperation Add = ECalculationOperation.Add;
        private const ECalculationOperation Multiply = ECalculationOperation.Multiply;

        /// <summary>A scaler times a number.</summary>
        private static ScalerGroup Times(AbstractScaler scaler, float by) => Group((scaler, Add), (Constant(by), Multiply));

        /// <summary>An attribute's value, on the caster or the target, as the effect lands.</summary>
        private static AttributeBackedScaler Read(ForgeAttribute attribute, ESourceTarget from = ESourceTarget.Source,
            EAttributeTargetBinary what = EAttributeTargetBinary.Current, EMathApplicationPolicy policy = EMathApplicationPolicy.AsIs)
        {
            var scaler = new AttributeBackedScaler
            {
                Name = $"{(from == ESourceTarget.Source ? "Caster's" : "Target's")} {attribute.Name}{(what == EAttributeTargetBinary.Base ? " (base)" : "")}",
                CaptureAttribute = attribute,
                CaptureFrom = from,
                CaptureWhen = ECaptureAttributeWhen.OnApplication,
                CaptureWhat = what,
                Behaviours = new List<AbstractScalerBehaviour>(),
            };
            if (policy != EMathApplicationPolicy.AsIs) scaler.Behaviours.Add(new AttenuateScalerBehaviour { Policy = policy });
            return scaler;
        }

        private static SetByCallerScaler DifficultyScaler() => new() { Name = "The AI's difficulty", Key = Difficulty, Default = 1f, Behaviours = new List<AbstractScalerBehaviour>() };

        private static ScalableFloat Fixed(float value) => new() { Magnitude = value, RealMagnitude = EMagnitudeOperation.UseMagnitude };

        private static ScalableFloat Scaled(AbstractScaler scaler) => new() { Magnitude = 0f, RealMagnitude = EMagnitudeOperation.UseScaler, Scaler = scaler };

        private static ScalableFloat Negated(AbstractScaler scaler) => new() { Magnitude = -1f, RealMagnitude = EMagnitudeOperation.MultiplyWithScaler, Scaler = scaler };

        private static ScalableInt Count(int value) => new() { Magnitude = value, RealMagnitude = EMagnitudeOperation.UseMagnitude };

        /// <summary>
        /// A blow's size, as a negative change to Health: its base by level, plus a coefficient times one of the caster's
        /// attributes; for an attack, times the attack growth every hero's attack shares; for a physical blow, more
        /// against a marked target; and times the AI's difficulty, 1 where nothing set it.
        /// </summary>
        private static ScalableFloat Damage(float[] byLevel, ForgeAttribute stat, float coefficient, Tag kind, bool attack = false,
            AbstractScaler extra = null)
        {
            var parts = new List<(AbstractScaler, ECalculationOperation)> { (Named(Levels(byLevel), "Base, by level"), Add) };
            if (stat != null && coefficient != 0f) parts.Add((Named(Times(Read(stat), coefficient), $"{coefficient:0.##} × {stat.Name}"), Add));
            if (extra != null) parts.Add((extra, Multiply));
            if (attack) parts.Add((Named(new SharedScaler { Asset = AttackGrowth, Behaviours = new List<AbstractScalerBehaviour>() }, "Attack growth"), Multiply));
            if (kind == Physical) parts.Add((MarkedBonus(), Multiply));
            parts.Add((DifficultyScaler(), Multiply));
            return Negated(Named(Group(parts.ToArray()), "Damage"));
        }

        /// <summary>A heal's size: its base by level, plus a coefficient times one of the caster's attributes.</summary>
        private static ScalableFloat Healing(float[] byLevel, ForgeAttribute stat = null, float coefficient = 0f)
        {
            if (stat == null) return Scaled(Named(Levels(byLevel), "Healing, by level"));
            return Scaled(Named(Group((Named(Levels(byLevel), "Base, by level"), Add),
                (Named(Times(Read(stat), coefficient), $"{coefficient:0.##} × {stat.Name}"), Add)), "Healing"));
        }

        // Hunter's Mark's bonus, on every physical blow: a fifth more against a marked target
        private static ConditionalScaler MarkedBonus() => new()
        {
            Name = "Marked: a fifth more",
            Condition = EScalerCondition.TargetTagCondition,
            TagCondition = new TagQuery { Tag = Marked, Operator = EComparisonOperator.GreaterThan, Magnitude = 0, MatchMode = ETagMatchMode.Exact },
            TrueScaler = Constant(1.2f),
            FalseScaler = Constant(1f),
            Behaviours = new List<AbstractScalerBehaviour>(),
        };
    }
}
