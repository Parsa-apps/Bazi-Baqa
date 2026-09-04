using System;
using System.Collections.Generic;
using UnityEngine;

namespace BaziBaqa
{
    public sealed class ResourceSystem : MonoBehaviour
    {
        public ResourceState Current { get; private set; } = new ResourceState();
        public event Action<ResourceType, int> ResourceChanged;

        private readonly Dictionary<ResourceType, int> _lastValues = new Dictionary<ResourceType, int>();

        public void Initialize(ResourceState initial)
        {
            Current = initial == null ? new ResourceState() : initial.Clone();
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                _lastValues[type] = Current.Get(type);
                ResourceChanged?.Invoke(type, Current.Get(type));
            }
        }

        public int Get(ResourceType type)
        {
            return Current.Get(type);
        }

        public void Add(ResourceType type, int amount, string reason = null)
        {
            if (amount == 0) return;
            Current.Add(type, amount);
            Publish(type);
        }

        public bool TrySpend(ResourceType type, int amount, string reason = null)
        {
            if (amount <= 0) return true;
            if (Current.Get(type) < amount) return false;
            Current.Add(type, -amount);
            Publish(type);
            return true;
        }

        public bool TrySpend(IReadOnlyList<ResourceCost> costs)
        {
            if (costs == null) return true;
            for (int i = 0; i < costs.Count; i++)
            {
                if (Current.Get(costs[i].type) < costs[i].amount) return false;
            }

            for (int i = 0; i < costs.Count; i++)
            {
                Current.Add(costs[i].type, -costs[i].amount);
                Publish(costs[i].type);
            }
            return true;
        }

        public ResourceState ToSave()
        {
            return Current.Clone();
        }

        private void Publish(ResourceType type)
        {
            int value = Current.Get(type);
            if (!_lastValues.ContainsKey(type) || _lastValues[type] != value)
            {
                _lastValues[type] = value;
                ResourceChanged?.Invoke(type, value);
            }
        }
    }

    [Serializable]
    public struct ResourceCost
    {
        public ResourceType type;
        public int amount;

        public ResourceCost(ResourceType resourceType, int resourceAmount)
        {
            type = resourceType;
            amount = resourceAmount;
        }

        public override string ToString()
        {
            return GameText.ResourceAmount(type, amount);
        }
    }
}
