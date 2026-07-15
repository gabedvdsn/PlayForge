using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended
{
    /// <summary>
    /// Read-only audit of every GameplayEffect asset under the impact-entry model.
    /// Entries are DERIVED from the authored impact + duration flags on every
    /// Generate() (never serialized), so there is nothing to migrate — this tool
    /// reports what each asset derives to, and flags authoring shapes worth reviewing
    /// (the TickOnApplication trap, ReverseImpactOnRemoval on periodic effects).
    /// </summary>
    public static class ImpactModelMigrationTool
    {
        [MenuItem("PlayForge/Audit Effect Impact Deliveries")]
        public static void AuditAll()
        {
            var guids = AssetDatabase.FindAssets("t:GameplayEffect");
            int flagged = 0, impactless = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var effect = AssetDatabase.LoadAssetAtPath<GameplayEffect>(path);
                if (effect == null || effect.ImpactSpecification == null) continue;

                effect.ImpactSpecification.RebuildEntries(effect.DurationSpecification);

                if (!effect.ImpactSpecification.HasEntries)
                {
                    impactless++;
                    continue;
                }

                var delivery = effect.ImpactSpecification.ResolveDelivery(effect.DurationSpecification, out string warning);

                if (!string.IsNullOrEmpty(warning))
                {
                    flagged++;
                    Debug.LogWarning($"[PlayForge audit] {path} → {delivery}: {warning}", effect);
                }
                else
                {
                    Debug.Log($"[PlayForge audit] {path} → {delivery}", effect);
                }
            }

            Debug.Log($"[PlayForge audit] Complete: {guids.Length} effects, {flagged} flagged for review, {impactless} impact-less (tag/worker-only).");
        }
    }
}
