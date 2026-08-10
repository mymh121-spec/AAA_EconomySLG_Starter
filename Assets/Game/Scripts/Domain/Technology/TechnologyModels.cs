using System;
using System.Collections.Generic;

namespace Game.Domain.Technology
{
    public enum TechnologyEffectType
    {
        ProductionEfficiency,
        PowerEfficiency,
        TransportLossReduction,
        WarehouseCapacity,
        MissionSuccessChance,
        UnlockRecipe
    }

    public readonly struct TechnologyEffect
    {
        public TechnologyEffectType Type { get; }
        public decimal Value { get; }
        public string TargetId { get; }

        public TechnologyEffect(
            TechnologyEffectType type,
            decimal value,
            string targetId = null)
        {
            Type = type;
            Value = value;
            TargetId = targetId;
        }
    }

    public sealed class TechnologyDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public decimal ResearchCost { get; }
        public IReadOnlyList<string> Prerequisites { get; }
        public IReadOnlyList<TechnologyEffect> Effects { get; }

        public TechnologyDefinition(
            string id,
            string displayName,
            decimal researchCost,
            IReadOnlyList<string> prerequisites,
            IReadOnlyList<TechnologyEffect> effects)
        {
            Id = id;
            DisplayName = displayName;
            ResearchCost = Math.Max(1, researchCost);
            Prerequisites = prerequisites ?? Array.Empty<string>();
            Effects = effects ?? Array.Empty<TechnologyEffect>();
        }
    }

    public sealed class TechnologyState
    {
        private readonly HashSet<string> _completed =
            new HashSet<string>();

        private readonly Dictionary<string, decimal> _progress =
            new Dictionary<string, decimal>();

        public bool IsCompleted(string technologyId) =>
            _completed.Contains(technologyId);

        public decimal GetProgress(string technologyId)
        {
            return _progress.TryGetValue(technologyId, out var value)
                ? value
                : 0;
        }

        public bool CanResearch(TechnologyDefinition definition)
        {
            if (definition == null || IsCompleted(definition.Id))
                return false;

            for (int i = 0; i < definition.Prerequisites.Count; i++)
            {
                if (!IsCompleted(definition.Prerequisites[i]))
                    return false;
            }

            return true;
        }

        public bool AddResearch(
            TechnologyDefinition definition,
            decimal researchPoints)
        {
            if (!CanResearch(definition) || researchPoints <= 0)
                return false;

            decimal progress =
                GetProgress(definition.Id) + researchPoints;

            if (progress < definition.ResearchCost)
            {
                _progress[definition.Id] = progress;
                return false;
            }

            _progress.Remove(definition.Id);
            _completed.Add(definition.Id);
            return true;
        }

        public decimal GetEffectValue(
            IReadOnlyList<TechnologyDefinition> catalog,
            TechnologyEffectType effectType)
        {
            decimal total = 0;

            for (int i = 0; i < catalog.Count; i++)
            {
                var definition = catalog[i];

                if (!IsCompleted(definition.Id))
                    continue;

                for (int j = 0; j < definition.Effects.Count; j++)
                {
                    if (definition.Effects[j].Type == effectType)
                        total += definition.Effects[j].Value;
                }
            }

            return total;
        }
    }
}
