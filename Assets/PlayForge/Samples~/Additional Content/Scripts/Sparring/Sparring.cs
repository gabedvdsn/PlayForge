using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge.AdditionalContent
{
    /// <summary>
    /// A dummy that spars, as the Sparring scene's two do (AC-2): it uses its abilities at its partner as each is ready,
    /// lands its statuses in turn, each on its partner or itself, and mends to full as its attribute falls low, so the
    /// bout goes on. It's given each of its abilities it hasn't. Its entity is the hub on the same object.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GameplayAbilitySystem))]
    [AddComponentMenu("PlayForge/Additional Content/Sparring")]
    public class Sparring : MonoBehaviour
    {
        /// <summary>An effect the dummy lands now and again: on its partner, as a slow, or itself, as a shield.</summary>
        [Serializable]
        public class Status
        {
            [Tooltip("The effect it lands")]
            public GameplayEffect Effect;

            [Tooltip("Lands on the dummy itself; off, on its partner")]
            public bool OnSelf;

            [Tooltip("When it first lands, in seconds from the bout's start")]
            [Min(0f)] public float First = 1f;

            [Tooltip("How often it lands after, in seconds")]
            [Min(0.1f)] public float Every = 4f;
        }

        [Tooltip("The one it spars with")]
        public GameplayAbilitySystem Partner;

        [Tooltip("The abilities it uses at its partner, the first that's ready each time")]
        public List<Ability> Abilities = new();

        [Tooltip("The least time between two abilities, in seconds")]
        [Min(0f)] public float Interval = 0.4f;

        [Tooltip("The statuses it lands")]
        public List<Status> Statuses = new();

        [Tooltip("The attribute it mends; with none, the one playing the project's Health role")]
        public Attribute Attribute;

        [Tooltip("It mends to full as its attribute falls below this share of its base")]
        [Range(0f, 1f)] public float MendBelow = 0.3f;

        private GameplayAbilitySystem own;
        private readonly Dictionary<Status, float> next = new();
        private float started = -1f;
        private float lastCast = float.NegativeInfinity;
        private bool given;

        /// <summary>How many abilities it has started.</summary>
        public int Casts { get; private set; }

        /// <summary>How many statuses it has landed.</summary>
        public int Landed { get; private set; }

        /// <summary>How many times it has mended.</summary>
        public int Mends { get; private set; }

        private void Awake() => own = GetComponent<GameplayAbilitySystem>();

        private void Update()
        {
            if (own == null || !own.IsInitialized || Partner == null || !Partner.IsInitialized) return;
            if (started < 0f) started = Time.time;
            if (!given) Give();
            Mend();
            Use();
            Land();
        }

        private void Give()
        {
            given = true;
            var abilities = own.GetAbilitySystem();
            foreach (var ability in Abilities)
                if (ability != null && !abilities.TryGetAbilityContainer(ability, out _))
                    abilities.GiveAbility(ability, 1, out _);
        }

        private void Use()
        {
            if (Time.time - lastCast < Interval) return;
            var abilities = own.GetAbilitySystem();
            foreach (var ability in Abilities)
            {
                if (ability == null || !abilities.TryGetAbilityContainer(ability, out var container)) continue;
                if (!abilities.CanActivateAbility(container.Index)) continue;
                var data = ProcessDataPacket.Default();
                data.SetPrimary(Tags.TARGET_REAL, (ITarget)Partner);
                data.SetPrimary(Tags.POSITION, Partner.transform.position);
                if (!abilities.TryActivateAbility(abilities.CreateActivationRequest(container.Index, data))) continue;
                Casts++;
                lastCast = Time.time;
                return;
            }
        }

        private void Land()
        {
            float now = Time.time - started;
            foreach (var status in Statuses)
            {
                if (status?.Effect == null) continue;
                if (!next.TryGetValue(status, out float at)) at = status.First;
                if (now < at)
                {
                    next[status] = at;
                    continue;
                }
                next[status] = at + status.Every;
                var onto = status.OnSelf ? own : Partner;
                var origin = IEffectOrigin.GenerateSourceDerivation(own);
                if (onto.ApplyGameplayEffect(onto.GenerateEffectSpec(origin, status.Effect))) Landed++;
            }
        }

        private void Mend()
        {
            var attribute = Attribute != null ? Attribute : ForgeRoles.AttributeFor(Tags.ROLE_HEALTH);
            if (attribute == null || !own.TryGetAttributeValue(attribute, out var value)) return;
            if (value.BaseValue <= 0f || value.CurrentValue >= value.BaseValue * MendBelow) return;
            var mend = SourcedModifiedAttributeValue.GenerateSimple(own, attribute, value.BaseValue - value.CurrentValue, 0f);
            if (own.TryModifyAttribute(attribute, mend)) Mends++;
        }
    }
}
