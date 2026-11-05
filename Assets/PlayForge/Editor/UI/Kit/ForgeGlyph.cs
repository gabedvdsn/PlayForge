using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>The glyphs PlayForge draws, from the style mockups' Kit.</summary>
    internal enum ForgeGlyphShape
    {
        None,

        // A foldout's arrows, on a 10 px grid
        ArrowDown,
        ArrowRight,

        // Actions, on Unity's 16 px grid. Add and Remove, a list footer's, are on the 10 px grid
        Import,
        Clear,
        Help,
        Menu,
        Search,
        Trash,
        Add,
        Remove,
        Check,
        Link,

        // Messages
        Info,
        Warning,

        // The asset types: placeholders until the commissioned set exists. They differ in shape as well as color
        Ability,
        Effect,
        Entity,
        Item,
        Attribute,
        AttributeSet,

        // The Debugger's: a tag, a process, Play Mode, following the selection, and a terminate that's at once
        Tag,
        Process,
        Play,
        Pointer,
        Terminate,
    }

    /// <summary>
    /// An inline vector glyph, drawn in its resolved color, which the style sheet sets (Decision 20). The mark cut
    /// into a filled glyph, such as the warning triangle's, takes the style sheet's --pf-glyph-cutout. PlayForge drew
    /// its icons with emoji and text characters (ED-20).
    /// </summary>
    internal class ForgeGlyph : VisualElement
    {
        public const string GlyphClass = "pf-glyph";
        private static readonly CustomStyleProperty<Color> CutoutProperty = new("--pf-glyph-cutout");

        private ForgeGlyphShape shape;
        private Color cutout = Color.clear;

        public ForgeGlyph(ForgeGlyphShape shape)
        {
            this.shape = shape;
            AddToClassList(GlyphClass);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(_ =>
            {
                cutout = customStyle.TryGetValue(CutoutProperty, out var color) ? color : Color.clear;
                MarkDirtyRepaint();
            });
        }

        public ForgeGlyphShape Shape
        {
            get => shape;
            set
            {
                if (shape == value) return;
                shape = value;
                MarkDirtyRepaint();
            }
        }

        private void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            if (shape == ForgeGlyphShape.None || rect.width <= 0f || rect.height <= 0f) return;

            var ink = resolvedStyle.color;
            var onTenGrid = shape is ForgeGlyphShape.ArrowDown or ForgeGlyphShape.ArrowRight or ForgeGlyphShape.Add
                or ForgeGlyphShape.Remove;
            var pen = new Pen(context.painter2D, rect, onTenGrid ? 10f : 16f);

            switch (shape)
            {
                case ForgeGlyphShape.ArrowDown:
                    pen.Fill(ink, 1, 3, 9, 3, 5, 8);
                    break;
                case ForgeGlyphShape.ArrowRight:
                    pen.Fill(ink, 3, 1, 3, 9, 8, 5);
                    break;
                case ForgeGlyphShape.Add:
                    pen.Line(ink, 1.4f, 5, 1, 5, 9);
                    pen.Line(ink, 1.4f, 1, 5, 9, 5);
                    break;
                case ForgeGlyphShape.Remove:
                    pen.Line(ink, 1.4f, 1, 5, 9, 5);
                    break;
                case ForgeGlyphShape.Import:
                    pen.Line(ink, 1.4f, 8, 2, 8, 10);
                    pen.Line(ink, 1.4f, 5, 7, 8, 10, 11, 7);
                    pen.Line(ink, 1.4f, 3, 11.5f, 3, 14, 13, 14, 13, 11.5f);
                    break;
                case ForgeGlyphShape.Clear:
                    pen.Outline(ink, 1.4f, 9.5f, 2.5f, 13.5f, 6.5f, 7.5f, 12.5f, 4.5f, 12.5f, 2.5f, 10.5f);
                    pen.Line(ink, 1.4f, 6.5f, 5.5f, 10.5f, 9.5f);
                    pen.Line(ink, 1.4f, 7, 14.5f, 14, 14.5f);
                    break;
                case ForgeGlyphShape.Help:
                    pen.Ring(ink, 1.4f, 8, 8, 6.2f);
                    pen.QuestionMark(ink, 1.4f);
                    pen.Disc(ink, 8, 11.8f, 1f);
                    break;
                case ForgeGlyphShape.Menu:
                    pen.Disc(ink, 8, 3, 1.3f);
                    pen.Disc(ink, 8, 8, 1.3f);
                    pen.Disc(ink, 8, 13, 1.3f);
                    break;
                case ForgeGlyphShape.Search:
                    pen.Ring(ink, 1.6f, 7, 7, 4.5f);
                    pen.Line(ink, 1.6f, 10.5f, 10.5f, 14, 14);
                    break;
                case ForgeGlyphShape.Trash:
                    pen.Line(ink, 1.5f, 3, 4.5f, 13, 4.5f);
                    pen.Line(ink, 1.5f, 6, 4.5f, 6, 3, 10, 3, 10, 4.5f);
                    pen.Line(ink, 1.5f, 4.5f, 4.5f, 5.2f, 13.5f, 10.8f, 13.5f, 11.5f, 4.5f);
                    break;
                case ForgeGlyphShape.Check:
                    pen.Line(ink, 2.2f, 2.5f, 8.5f, 6, 12, 13.5f, 4);
                    break;
                case ForgeGlyphShape.Link:
                    pen.Capsule(ink, 1.5f, 5.6f, 10.4f);
                    pen.Capsule(ink, 1.5f, 10.4f, 5.6f);
                    break;
                case ForgeGlyphShape.Info:
                    pen.Ring(ink, 1.3f, 8, 8, 6.4f);
                    pen.Line(ink, 1.3f, 8, 7.2f, 8, 11.2f);
                    pen.Disc(ink, 8, 4.9f, 1f);
                    break;
                case ForgeGlyphShape.Warning:
                    pen.Fill(ink, 8, 1.5f, 15, 14, 1, 14);
                    pen.Line(cutout, 1.6f, 8, 6, 8, 10);
                    pen.Disc(cutout, 8, 12, 0.9f);
                    break;
                case ForgeGlyphShape.Ability:
                    pen.Fill(ink, 8, 1, 9.9f, 6.1f, 15, 8, 9.9f, 9.9f, 8, 15, 6.1f, 9.9f, 1, 8, 6.1f, 6.1f);
                    break;
                case ForgeGlyphShape.Effect:
                    pen.Ring(ink, 1.6f, 8, 8, 6.1f);
                    pen.Disc(ink, 8, 8, 2.7f);
                    break;
                case ForgeGlyphShape.Entity:
                    pen.Disc(ink, 8, 5, 3);
                    pen.Shoulders(ink);
                    break;
                case ForgeGlyphShape.Item:
                    pen.Fill(ink, 4.5f, 2, 11.5f, 2, 14.5f, 6, 8, 14.5f, 1.5f, 6);
                    break;
                case ForgeGlyphShape.Attribute:
                    pen.RoundedRect(ink, 1.5f, 4.5f, 13, 7, 1.5f, 1.4f);
                    pen.Fill(ink, 3.5f, 6.5f, 9.5f, 6.5f, 9.5f, 9.5f, 3.5f, 9.5f);
                    break;
                case ForgeGlyphShape.AttributeSet:
                    pen.RoundedRect(ink, 1.5f, 2, 13, 3, 1);
                    pen.RoundedRect(ink, 1.5f, 6.5f, 13, 3, 1);
                    pen.RoundedRect(ink, 1.5f, 11, 13, 3, 1);
                    break;
                case ForgeGlyphShape.Tag:
                    pen.Outline(ink, 1.2f, 2.5f, 2.5f, 7.7f, 2.5f, 13.7f, 8.5f, 8.5f, 13.7f, 2.5f, 7.7f);
                    pen.Ring(ink, 1.2f, 5.4f, 5.4f, 1.1f);
                    break;
                case ForgeGlyphShape.Process:
                    pen.RoundedRect(ink, 2, 3, 12, 10, 1, 1.2f);
                    pen.Line(ink, 1.2f, 5, 6.5f, 7, 8, 5, 9.5f);
                    pen.Line(ink, 1.2f, 8.5f, 10, 11, 10);
                    break;
                case ForgeGlyphShape.Play:
                    pen.Fill(ink, 4.5f, 3, 4.5f, 13, 13, 8);
                    break;
                case ForgeGlyphShape.Pointer:
                    pen.Outline(ink, 1.5f, 2, 2, 7, 14, 8.8f, 8.8f, 14, 7);
                    break;
                case ForgeGlyphShape.Terminate:
                    pen.Ring(ink, 1.6f, 8, 8, 6);
                    pen.Line(ink, 1.6f, 5.5f, 5.5f, 10.5f, 10.5f);
                    pen.Line(ink, 1.6f, 10.5f, 5.5f, 5.5f, 10.5f);
                    break;
            }
        }

        // Draws on a glyph's grid, scaled to fit the element and centered in it
        private readonly struct Pen
        {
            private static readonly Angle Start = new(0f, AngleUnit.Degree);
            private static readonly Angle Full = new(360f, AngleUnit.Degree);

            private readonly Painter2D painter;
            private readonly Vector2 origin;
            private readonly float scale;

            public Pen(Painter2D painter, Rect rect, float grid)
            {
                this.painter = painter;
                scale = Mathf.Min(rect.width, rect.height) / grid;
                origin = new Vector2(rect.x + (rect.width - grid * scale) / 2f, rect.y + (rect.height - grid * scale) / 2f);
            }

            private Vector2 At(float x, float y) => origin + new Vector2(x, y) * scale;

            private void Path(float[] points)
            {
                painter.BeginPath();
                painter.MoveTo(At(points[0], points[1]));
                for (int i = 2; i + 1 < points.Length; i += 2) painter.LineTo(At(points[i], points[i + 1]));
            }

            private void Pencil(Color color, float width)
            {
                painter.strokeColor = color;
                painter.lineWidth = width * scale;
                painter.lineJoin = LineJoin.Round;
                painter.lineCap = LineCap.Round;
            }

            // A filled polygon through the points, given as x, y pairs
            public void Fill(Color color, params float[] points)
            {
                Path(points);
                painter.ClosePath();
                painter.fillColor = color;
                painter.Fill();
            }

            // An open line through the points
            public void Line(Color color, float width, params float[] points)
            {
                Pencil(color, width);
                Path(points);
                painter.Stroke();
            }

            // A closed outline through the points
            public void Outline(Color color, float width, params float[] points)
            {
                Pencil(color, width);
                Path(points);
                painter.ClosePath();
                painter.Stroke();
            }

            public void Disc(Color color, float x, float y, float radius)
            {
                painter.BeginPath();
                painter.Arc(At(x, y), radius * scale, Start, Full);
                painter.ClosePath();
                painter.fillColor = color;
                painter.Fill();
            }

            public void Ring(Color color, float width, float x, float y, float radius)
            {
                Pencil(color, width);
                painter.BeginPath();
                painter.Arc(At(x, y), radius * scale, Start, Full);
                painter.ClosePath();
                painter.Stroke();
            }

            // A rounded rectangle, filled, or outlined when a width is given
            public void RoundedRect(Color color, float x, float y, float w, float h, float radius, float width = 0f)
            {
                painter.BeginPath();
                painter.MoveTo(At(x + radius, y));
                painter.ArcTo(At(x + w, y), At(x + w, y + h), radius * scale);
                painter.ArcTo(At(x + w, y + h), At(x, y + h), radius * scale);
                painter.ArcTo(At(x, y + h), At(x, y), radius * scale);
                painter.ArcTo(At(x, y), At(x + w, y), radius * scale);
                painter.ClosePath();
                if (width > 0f)
                {
                    Pencil(color, width);
                    painter.Stroke();
                }
                else
                {
                    painter.fillColor = color;
                    painter.Fill();
                }
            }

            // The help glyph's hook: an arc over the top, then down to the dot
            public void QuestionMark(Color color, float width)
            {
                Pencil(color, width);
                painter.BeginPath();
                painter.Arc(At(8.1f, 6.28f), 1.9f * scale, new Angle(179.4f, AngleUnit.Degree), new Angle(425f, AngleUnit.Degree));
                painter.BezierCurveTo(At(8.3f, 8.3f), At(8f, 8.7f), At(8f, 9.3f));
                painter.LineTo(At(8f, 9.7f));
                painter.Stroke();
            }

            // One link of a chain: a capsule on the diagonal from the lower left to the upper right, centered at x, y
            public void Capsule(Color color, float width, float x, float y)
            {
                const float half = 1.9f, radius = 2.3f;
                var axis = new Vector2(0.7071f, -0.7071f);
                var side = new Vector2(0.7071f, 0.7071f);
                var center = new Vector2(x, y);
                var upper = center + axis * half;
                var lower = center - axis * half;
                Pencil(color, width);
                painter.BeginPath();
                painter.MoveTo(At(lower.x + side.x * radius, lower.y + side.y * radius));
                painter.LineTo(At(upper.x + side.x * radius, upper.y + side.y * radius));
                painter.Arc(At(upper.x, upper.y), radius * scale, new Angle(45f, AngleUnit.Degree), new Angle(-135f, AngleUnit.Degree),
                    ArcDirection.CounterClockwise);
                painter.LineTo(At(lower.x - side.x * radius, lower.y - side.y * radius));
                painter.Arc(At(lower.x, lower.y), radius * scale, new Angle(225f, AngleUnit.Degree), new Angle(45f, AngleUnit.Degree),
                    ArcDirection.CounterClockwise);
                painter.ClosePath();
                painter.Stroke();
            }

            // The entity glyph's shoulders, under its head
            public void Shoulders(Color color)
            {
                painter.BeginPath();
                painter.MoveTo(At(2.5f, 15f));
                painter.BezierCurveTo(At(2.5f, 11.6f), At(4.9f, 9.4f), At(8f, 9.4f));
                painter.BezierCurveTo(At(11.1f, 9.4f), At(13.5f, 11.6f), At(13.5f, 15f));
                painter.ClosePath();
                painter.fillColor = color;
                painter.Fill();
            }
        }
    }

    /// <summary>An asset type's icon, in the type's color, which the style sheet sets.</summary>
    internal class ForgeTypeIcon : ForgeGlyph
    {
        public const string TypeIconClass = "pf-type-icon";

        public ForgeTypeIcon(Type assetType) : base(ForgeAssetTypes.GlyphOf(assetType))
        {
            AddToClassList(TypeIconClass);
            var modifier = ForgeAssetTypes.ModifierOf(assetType);
            if (modifier != null) AddToClassList($"{TypeIconClass}--{modifier}");
        }
    }

    /// <summary>A flat button that shows a glyph, as a section's Import, Clear and Help do.</summary>
    internal class ForgeIconButton : Button
    {
        public const string IconButtonClass = "pf-icon-button";

        public ForgeIconButton(ForgeGlyphShape glyph, string tooltip, Action onClick) : base(onClick)
        {
            AddToClassList(IconButtonClass);
            this.tooltip = tooltip;
            Glyph = new ForgeGlyph(glyph);
            Add(Glyph);
        }

        public ForgeGlyph Glyph { get; }
    }

    /// <summary>Each asset type's glyph, its class modifier in the style sheet, and its name.</summary>
    internal static class ForgeAssetTypes
    {
        public static ForgeGlyphShape GlyphOf(Type type) => Describe(type).Glyph;
        public static string ModifierOf(Type type) => Describe(type).Modifier;
        public static string NameOf(Type type) => Describe(type).Name;

        private static (ForgeGlyphShape Glyph, string Modifier, string Name) Describe(Type type)
        {
            if (type == null) return (ForgeGlyphShape.None, null, "");
            if (typeof(Ability).IsAssignableFrom(type)) return (ForgeGlyphShape.Ability, "ability", "Ability");
            if (typeof(GameplayEffect).IsAssignableFrom(type)) return (ForgeGlyphShape.Effect, "effect", "Gameplay Effect");
            if (typeof(EntityIdentity).IsAssignableFrom(type)) return (ForgeGlyphShape.Entity, "entity", "Entity");
            if (typeof(Item).IsAssignableFrom(type)) return (ForgeGlyphShape.Item, "item", "Item");
            if (typeof(AttributeSet).IsAssignableFrom(type)) return (ForgeGlyphShape.AttributeSet, "attribute-set", "Attribute Set");
            if (typeof(Attribute).IsAssignableFrom(type)) return (ForgeGlyphShape.Attribute, "attribute", "Attribute");
            return (ForgeGlyphShape.None, null, ObjectNames.NicifyVariableName(type.Name));
        }
    }
}
