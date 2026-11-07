using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Demo.Editor
{
    /// <summary>
    /// Draws the demo's placeholder icons in code, saves each as a PNG under the sample's Textures folder, and makes it its
    /// asset's primary texture: a rounded square in the asset's color, a shape for its kind (a circle for an ability, a
    /// diamond for an item, a square for an attribute, a triangle for an effect, a ring for an entity) and its initials.
    /// The assets hold the textures, so art can replace a PNG without touching an asset. A hero and its abilities share
    /// the hero's color; the rest take a color from their names.
    /// </summary>
    public static class DemoIcons
    {
        private const int Size = 64;

        private enum Kind
        {
            Ability,
            Item,
            Attribute,
            Effect,
            Entity,
        }

        [MenuItem("Window/PlayForge/Demo/Make Placeholder Icons")]
        private static void MakeFromMenu()
        {
            var roster = AssetDatabase.FindAssets("t:DemoRoster").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
            if (roster == null)
            {
                EditorUtility.DisplayDialog("Make Placeholder Icons", "No Demo Roster asset was found, so there's no demo to draw icons for.", "OK");
                return;
            }
            int made = MakeAll(Path.GetDirectoryName(roster)?.Replace('\\', '/'));
            Debug.Log($"[PlayForge Demo] Drew {made} placeholder icons.");
        }

        /// <summary>Draws an icon for each of the demo's assets under a folder, and sets it as the asset's primary texture.</summary>
        public static int MakeAll(string root)
        {
            if (string.IsNullOrEmpty(root)) return 0;
            var colors = HeroColors(root);
            var assets = AssetDatabase.FindAssets("t:BaseForgeAsset", new[] { root })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<BaseForgeAsset>)
                .Where(asset => asset != null)
                .ToList();

            int made = 0;
            foreach (var asset in assets)
            {
                if (!Wants(asset, out var kind, out var textures)) continue;
                var color = colors.TryGetValue(asset, out var own) ? own : ColorFor(asset, kind);
                var path = $"{root}/Textures/{FolderOf(kind)}/{Safe(asset.name)}.png";
                var texture = Save(Draw(kind, color, Initials(asset.GetName() ?? asset.name)), path);
                if (texture == null) continue;

                textures.RemoveAll(item => item == null || item.Tag == Tags.PRIMARY);
                textures.Insert(0, new TextureItem { Tag = Tags.PRIMARY, Texture = texture });
                EditorUtility.SetDirty(asset);
                made++;
            }
            AssetDatabase.SaveAssets();
            return made;
        }

        // The assets that show an icon: each hero, ability, item and attribute, and each effect a player sees
        private static bool Wants(BaseForgeAsset asset, out Kind kind, out List<TextureItem> textures)
        {
            kind = default;
            textures = null;
            switch (asset)
            {
                case Ability ability:
                    kind = Kind.Ability;
                    textures = ability.Definition.Textures ??= new List<TextureItem>();
                    return true;
                case Item item:
                    kind = Kind.Item;
                    textures = item.Definition.Textures ??= new List<TextureItem>();
                    return true;
                case Attribute attribute:
                    kind = Kind.Attribute;
                    textures = attribute.Textures ??= new List<TextureItem>();
                    return true;
                case EntityIdentity entity:
                    kind = Kind.Entity;
                    textures = entity.Textures ??= new List<TextureItem>();
                    return true;
                case GameplayEffect effect when effect.Definition != null && effect.Definition.Visibility != Tags.NOT_VISIBLE:
                    kind = Kind.Effect;
                    textures = effect.Definition.Textures ??= new List<TextureItem>();
                    return true;
                default:
                    return false;
            }
        }

        // Each hero's color, from the roster, for the hero and each of its abilities
        private static Dictionary<BaseForgeAsset, Color> HeroColors(string root)
        {
            var colors = new Dictionary<BaseForgeAsset, Color>();
            foreach (var guid in AssetDatabase.FindAssets("t:DemoRoster", new[] { root }))
            {
                var roster = AssetDatabase.LoadAssetAtPath<DemoRoster>(AssetDatabase.GUIDToAssetPath(guid));
                if (roster == null) continue;
                foreach (var hero in roster.Heroes)
                {
                    if (hero?.Identity == null) continue;
                    colors[hero.Identity] = hero.Color;
                    foreach (var ability in hero.Identity.StartingAbilities)
                        if (ability != null) colors.TryAdd(ability, hero.Color);
                }
                if (roster.TrainingDummy != null) colors[roster.TrainingDummy] = roster.DummyColor;
            }
            return colors;
        }

        // A color from the asset's name, in a range for its kind: a buff green, a debuff red, an item gold
        private static Color ColorFor(BaseForgeAsset asset, Kind kind)
        {
            float hash = (Mathf.Abs((asset.GetName() ?? asset.name).GetHashCode()) % 1000) / 1000f;
            switch (kind)
            {
                case Kind.Item:
                    return Color.HSVToRGB(Mathf.Lerp(0.07f, 0.15f, hash), 0.65f, 0.85f);
                case Kind.Attribute:
                    return Color.HSVToRGB(Mathf.Lerp(0.55f, 0.72f, hash), 0.45f, 0.75f);
                case Kind.Effect when asset is GameplayEffect effect:
                    bool debuff = effect.Tags.ContextTags != null && effect.Tags.ContextTags.Any(tag => tag.Name != null && tag.Name.EndsWith("Debuff"));
                    return debuff
                        ? Color.HSVToRGB(Mathf.Lerp(0.97f, 1.03f, hash) % 1f, 0.7f, 0.8f)
                        : Color.HSVToRGB(Mathf.Lerp(0.28f, 0.42f, hash), 0.6f, 0.75f);
                default:
                    return Color.HSVToRGB(hash, 0.5f, 0.75f);
            }
        }

        private static string FolderOf(Kind kind) => kind switch
        {
            Kind.Ability => "Abilities",
            Kind.Item => "Items",
            Kind.Attribute => "Attributes",
            Kind.Effect => "Effects",
            _ => "Heroes",
        };

        private static string Initials(string name)
        {
            var words = name.Split(' ', '-', '\'').Where(word => word.Length > 0 && char.IsLetter(word[0])).ToArray();
            if (words.Length >= 2) return $"{char.ToUpperInvariant(words[0][0])}{char.ToUpperInvariant(words[1][0])}";
            var letters = new string(name.Where(char.IsLetter).ToArray()).ToUpperInvariant();
            return letters.Length >= 2 ? letters.Substring(0, 2) : letters;
        }

        private static string Safe(string name) => string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

        // ═══════════════════════════════════════════════════════════════════════════
        // Drawing
        // ═══════════════════════════════════════════════════════════════════════════

        private static Color[] Draw(Kind kind, Color color, string initials)
        {
            var pixels = new Color[Size * Size];
            var light = Color.Lerp(color, Color.white, 0.35f);
            var dark = Color.Lerp(color, Color.black, 0.45f);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    var p = new Vector2(x + 0.5f - Size / 2f, y + 0.5f - Size / 2f);
                    float tile = Cover(RoundedSquare(p, Size / 2f - 2f, 12f));
                    var c = Color.Lerp(dark, color, Mathf.Clamp01(0.55f + y / (float)Size * 0.6f));
                    c = Color.Lerp(c, light, Cover(Shape(kind, p)) * 0.55f);
                    c.a = tile;
                    pixels[y * Size + x] = c;
                }
            }
            Letters(pixels, initials);
            return pixels;
        }

        private static float Shape(Kind kind, Vector2 p) => kind switch
        {
            Kind.Ability => p.magnitude - 21f,
            Kind.Item => (Mathf.Abs(p.x) + Mathf.Abs(p.y) - 24f) * 0.7071f,
            Kind.Attribute => Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y)) - 18f,
            Kind.Effect => Triangle(p, 23f),
            _ => Mathf.Abs(p.magnitude - 20f) - 3.5f,
        };

        // Coverage from a signed distance in pixels, smoothed over one pixel
        private static float Cover(float distance) => Mathf.Clamp01(0.5f - distance);

        private static float RoundedSquare(Vector2 p, float half, float radius)
        {
            var q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - new Vector2(half - radius, half - radius);
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        // An upward triangle's signed distance, of the given radius
        private static float Triangle(Vector2 p, float radius)
        {
            const float k = 1.7320508f;
            p = new Vector2(Mathf.Abs(p.x) - radius * 0.866f, -p.y + radius * 0.5f);
            if (p.x + k * p.y > 0f) p = new Vector2(p.x - k * p.y, -k * p.x - p.y) / 2f;
            p.x -= Mathf.Clamp(p.x, -2f * radius * 0.866f, 0f);
            return -p.magnitude * Mathf.Sign(p.y);
        }

        // The initials in a 5 by 7 font, three pixels to a dot, white over a soft shadow, in the middle
        private static void Letters(Color[] pixels, string text)
        {
            const int scale = 3, gap = 3;
            int width = text.Length * 5 * scale + Mathf.Max(0, text.Length - 1) * gap;
            int left = (Size - width) / 2, top = (Size - 7 * scale) / 2;
            for (int pass = 0; pass < 2; pass++)
            {
                var ink = pass == 0 ? new Color(0f, 0f, 0f, 0.45f) : Color.white;
                int shift = pass == 0 ? 1 : 0;
                for (int i = 0; i < text.Length; i++)
                {
                    if (!Glyphs.TryGetValue(text[i], out var columns)) continue;
                    for (int column = 0; column < 5; column++)
                    for (int row = 0; row < 7; row++)
                    {
                        if ((columns[column] >> row & 1) == 0) continue;
                        for (int dy = 0; dy < scale; dy++)
                        for (int dx = 0; dx < scale; dx++)
                        {
                            int x = left + i * (5 * scale + gap) + column * scale + dx + shift;
                            int y = Size - 1 - (top + row * scale + dy) - shift;
                            if (x < 0 || x >= Size || y < 0 || y >= Size) continue;
                            var under = pixels[y * Size + x];
                            var over = Color.Lerp(under, ink, ink.a);
                            over.a = under.a;
                            pixels[y * Size + x] = over;
                        }
                    }
                }
            }
        }

        // A classic 5 by 7 font: five columns a letter, the lowest bit the top row
        private static readonly Dictionary<char, byte[]> Glyphs = new()
        {
            ['A'] = new byte[] { 0x7E, 0x11, 0x11, 0x11, 0x7E }, ['B'] = new byte[] { 0x7F, 0x49, 0x49, 0x49, 0x36 },
            ['C'] = new byte[] { 0x3E, 0x41, 0x41, 0x41, 0x22 }, ['D'] = new byte[] { 0x7F, 0x41, 0x41, 0x22, 0x1C },
            ['E'] = new byte[] { 0x7F, 0x49, 0x49, 0x49, 0x41 }, ['F'] = new byte[] { 0x7F, 0x09, 0x09, 0x09, 0x01 },
            ['G'] = new byte[] { 0x3E, 0x41, 0x49, 0x49, 0x7A }, ['H'] = new byte[] { 0x7F, 0x08, 0x08, 0x08, 0x7F },
            ['I'] = new byte[] { 0x00, 0x41, 0x7F, 0x41, 0x00 }, ['J'] = new byte[] { 0x20, 0x40, 0x41, 0x3F, 0x01 },
            ['K'] = new byte[] { 0x7F, 0x08, 0x14, 0x22, 0x41 }, ['L'] = new byte[] { 0x7F, 0x40, 0x40, 0x40, 0x40 },
            ['M'] = new byte[] { 0x7F, 0x02, 0x0C, 0x02, 0x7F }, ['N'] = new byte[] { 0x7F, 0x04, 0x08, 0x10, 0x7F },
            ['O'] = new byte[] { 0x3E, 0x41, 0x41, 0x41, 0x3E }, ['P'] = new byte[] { 0x7F, 0x09, 0x09, 0x09, 0x06 },
            ['Q'] = new byte[] { 0x3E, 0x41, 0x51, 0x21, 0x5E }, ['R'] = new byte[] { 0x7F, 0x09, 0x19, 0x29, 0x46 },
            ['S'] = new byte[] { 0x46, 0x49, 0x49, 0x49, 0x31 }, ['T'] = new byte[] { 0x01, 0x01, 0x7F, 0x01, 0x01 },
            ['U'] = new byte[] { 0x3F, 0x40, 0x40, 0x40, 0x3F }, ['V'] = new byte[] { 0x1F, 0x20, 0x40, 0x20, 0x1F },
            ['W'] = new byte[] { 0x3F, 0x40, 0x38, 0x40, 0x3F }, ['X'] = new byte[] { 0x63, 0x14, 0x08, 0x14, 0x63 },
            ['Y'] = new byte[] { 0x07, 0x08, 0x70, 0x08, 0x07 }, ['Z'] = new byte[] { 0x61, 0x51, 0x49, 0x45, 0x43 },
        };

        // ═══════════════════════════════════════════════════════════════════════════
        // Saving
        // ═══════════════════════════════════════════════════════════════════════════

        private static Texture2D Save(Color[] pixels, string path)
        {
            var folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
