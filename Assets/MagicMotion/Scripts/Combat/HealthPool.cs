using System;
using UnityEngine;

namespace MagicMotion.Combat
{
    public sealed class HealthPool
    {
        public HealthPool(float maximum)
        {
            if (maximum <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(maximum));
            }

            Maximum = maximum;
            Current = maximum;
        }

        public float Maximum { get; }
        public float Current { get; private set; }
        public bool IsDepleted => Current <= 0f;

        public float ApplyDamage(float amount)
        {
            if (amount <= 0f || IsDepleted)
            {
                return 0f;
            }

            var previous = Current;
            Current = Mathf.Max(0f, Current - amount);
            return previous - Current;
        }
    }
}

