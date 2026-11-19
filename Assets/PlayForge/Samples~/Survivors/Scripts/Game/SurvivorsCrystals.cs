using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Survivors
{
    /// <summary>
    /// The experience crystals the fallen drop: each lies where its enemy fell until the hero comes within its Pickup
    /// Radius, then flies to the hero and gives its worth. Their bodies are pooled and stepped together here, with no
    /// component of their own, so hundreds cost a list; past a limit, a new one folds the oldest into itself.
    /// </summary>
    public sealed class SurvivorsCrystals
    {
        private sealed class Crystal
        {
            public Transform Body;
            public SurvivorsShape Shape;
            public float Worth;
            public bool Drawn;
        }

        /// <summary>The most crystals lying at once.</summary>
        public int Most = 400;

        private const float FlySpeed = 16f;
        private readonly SurvivorsGame game;
        private readonly List<Crystal> lying = new();
        private readonly Stack<Crystal> spare = new();
        private Transform holder;

        public SurvivorsCrystals(SurvivorsGame game) => this.game = game;

        /// <summary>How many crystals lie.</summary>
        public int Count => lying.Count;

        /// <summary>A crystal of a worth where an enemy fell.</summary>
        public void Drop(Vector3 at, float worth)
        {
            if (lying.Count >= Most && lying.Count > 0)
            {
                // The oldest folds into the new one, so none is lost
                worth += lying[0].Worth;
                Recycle(0);
            }
            var crystal = spare.Count > 0 ? spare.Pop() : Make();
            crystal.Worth = worth;
            crystal.Drawn = false;
            crystal.Body.position = new Vector3(at.x, 0.45f, at.z);
            crystal.Shape.Recolor(ColorOf(worth));
            crystal.Body.localScale = Vector3.one * (worth >= 10f ? 1.6f : worth >= 4f ? 1.25f : 1f);
            crystal.Body.gameObject.SetActive(true);
            lying.Add(crystal);
        }

        /// <summary>A step: each within the hero's reach flies to it, and each that arrives gives its worth.</summary>
        public void Step(float delta)
        {
            var hero = game.Hero;
            if (!hero || !hero.Alive) return;
            var roster = game.Roster;
            float reach = roster && roster.PickupRadius ? Mathf.Max(0.5f, hero.Value(roster.PickupRadius)) : 2.5f;
            var heroAt = hero.transform.position + Vector3.up * 0.5f;
            for (int i = lying.Count - 1; i >= 0; i--)
            {
                var crystal = lying[i];
                var toward = heroAt - crystal.Body.position;
                float distance = new Vector2(toward.x, toward.z).magnitude;
                if (!crystal.Drawn && distance > reach) continue;
                crystal.Drawn = true;
                if (distance < 0.6f)
                {
                    game.Gain(crystal.Worth);
                    Recycle(i);
                    continue;
                }
                crystal.Body.position += toward.normalized * Mathf.Min(distance, FlySpeed * delta);
            }
        }

        /// <summary>Every crystal taken up, as a new run starts.</summary>
        public void Clear()
        {
            for (int i = lying.Count - 1; i >= 0; i--) Recycle(i);
        }

        private void Recycle(int index)
        {
            var crystal = lying[index];
            lying.RemoveAt(index);
            crystal.Body.gameObject.SetActive(false);
            spare.Push(crystal);
        }

        private Crystal Make()
        {
            if (!holder) holder = new GameObject("Crystals").transform;
            var shape = SurvivorsShape.Create("Crystal", holder, PrimitiveType.Cube, Vector3.one * 0.32f, Color.white,
                rotation: new Vector3(45f, 0f, 45f), spin: 140f);
            return new Crystal { Body = shape.transform, Shape = shape };
        }

        // Blue for the least, then green, gold and red as they're worth more
        private static Color ColorOf(float worth) =>
            worth >= 25f ? new Color(1f, 0.3f, 0.35f)
            : worth >= 10f ? new Color(1f, 0.82f, 0.25f)
            : worth >= 4f ? new Color(0.35f, 0.95f, 0.45f)
            : new Color(0.35f, 0.7f, 1f);
    }
}
