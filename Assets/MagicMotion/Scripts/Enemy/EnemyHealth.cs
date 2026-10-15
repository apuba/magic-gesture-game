using MagicMotion.Combat;
using MagicMotion.Core;
using UnityEngine;

namespace MagicMotion.Enemy
{
    [DisallowMultipleComponent]
    public sealed class EnemyHealth : MonoBehaviour
    {
        [SerializeField] private string enemyId = "StoneDemon";
        [SerializeField, Min(1f)] private float maximumHealth = 100f;

        private HealthPool health;
        private bool subscribed;

        public string EnemyId => enemyId;
        public float CurrentHealth => health?.Current ?? maximumHealth;
        public float MaximumHealth => health?.Maximum ?? maximumHealth;
        public bool IsDefeated => health != null && health.IsDepleted;

        public void Configure(string id, float healthValue)
        {
            enemyId = string.IsNullOrWhiteSpace(id) ? "Enemy" : id;
            maximumHealth = Mathf.Max(1f, healthValue);
            health = new HealthPool(maximumHealth);
        }

        private void Start()
        {
            health ??= new HealthPool(maximumHealth);
            Subscribe();
        }

        private void HandleDamage(DamageEvent message)
        {
            if (message.TargetInstanceId != gameObject.GetInstanceID() || health.IsDepleted)
            {
                return;
            }

            var applied = health.ApplyDamage(message.Amount);
            if (applied <= 0f)
            {
                return;
            }

            GameManager.Instance.Events.Publish(new DamageApplied(
                gameObject.GetInstanceID(),
                message.SourceId,
                applied,
                health.Current));

            ApplyHitFeedback(message.Force);
            if (health.IsDepleted)
            {
                GameManager.Instance.Events.Publish(new EnemyDefeated(gameObject.GetInstanceID(), enemyId));
                gameObject.SetActive(false);
            }
        }

        private void ApplyHitFeedback(Vector3 force)
        {
            if (TryGetComponent<Rigidbody>(out var body) && !body.isKinematic)
            {
                body.AddForce(force, ForceMode.Impulse);
            }

            if (TryGetComponent<Renderer>(out var targetRenderer))
            {
                targetRenderer.material.color = new Color(1f, 0.35f, 0.08f);
            }
        }

        private void Subscribe()
        {
            if (subscribed || GameManager.Instance == null)
            {
                return;
            }

            GameManager.Instance.Events.Subscribe<DamageEvent>(HandleDamage);
            subscribed = true;
        }

        private void OnDestroy()
        {
            if (subscribed && GameManager.Instance != null)
            {
                GameManager.Instance.Events.Unsubscribe<DamageEvent>(HandleDamage);
            }
        }
    }
}

