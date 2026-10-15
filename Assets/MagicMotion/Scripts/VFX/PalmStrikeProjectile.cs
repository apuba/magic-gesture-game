using MagicMotion.Combat;
using MagicMotion.Core;
using MagicMotion.Enemy;
using UnityEngine;

namespace MagicMotion.VFX
{
    [DisallowMultipleComponent]
    public sealed class PalmStrikeProjectile : MonoBehaviour
    {
        private const string SourceId = "SK001";

        private Vector3 direction;
        private float speed;
        private float damage;
        private float force;
        private float expiresAt;
        private bool consumed;

        public void Initialize(Vector3 travelDirection, float travelSpeed, float damageAmount, float forceAmount, float lifetime)
        {
            direction = travelDirection.normalized;
            speed = travelSpeed;
            damage = damageAmount;
            force = forceAmount;
            expiresAt = Time.time + lifetime;
        }

        private void Update()
        {
            transform.position += direction * (speed * Time.deltaTime);
            transform.Rotate(0f, 0f, 240f * Time.deltaTime, Space.Self);
            if (Time.time >= expiresAt)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (consumed)
            {
                return;
            }

            var target = other.GetComponentInParent<EnemyHealth>();
            if (target == null || target.IsDefeated)
            {
                return;
            }

            consumed = true;
            GameManager.Instance.Events.Publish(new DamageEvent(
                target.gameObject.GetInstanceID(),
                SourceId,
                damage,
                transform.position,
                direction * force));
            Destroy(gameObject);
        }
    }
}

