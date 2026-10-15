using MagicMotion.Vision;
using MagicMotion.Core;
using UnityEngine;

namespace MagicMotion.VFX
{
    public static class PalmStrikeEffectFactory
    {
        public static GameObject Create(Handedness hand)
        {
            var viewCamera = UnityEngine.Camera.main;
            if (viewCamera == null)
            {
                Debug.LogError("[MagicMotion.VFX] Palm Strike requires a MainCamera.");
                return null;
            }

            var side = hand == Handedness.Left ? -0.25f : 0.25f;
            var origin = viewCamera.transform.position +
                         viewCamera.transform.forward * 0.8f +
                         viewCamera.transform.right * side -
                         viewCamera.transform.up * 0.1f;

            var projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = "PalmStrike.Placeholder";
            projectile.transform.position = origin;
            projectile.transform.localScale = new Vector3(0.45f, 0.45f, 0.18f);

            var renderer = projectile.GetComponent<Renderer>();
            renderer.material = RuntimeMaterialFactory.CreateLit(
                new Color(1f, 0.72f, 0.12f),
                new Color(1f, 0.45f, 0.03f) * 2f);

            var collider = projectile.GetComponent<SphereCollider>();
            collider.isTrigger = true;
            var body = projectile.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            var trail = projectile.AddComponent<TrailRenderer>();
            trail.time = 0.22f;
            trail.startWidth = 0.28f;
            trail.endWidth = 0f;
            trail.startColor = new Color(1f, 0.72f, 0.12f, 0.9f);
            trail.endColor = new Color(1f, 0.25f, 0.02f, 0f);
            trail.material = RuntimeMaterialFactory.CreateTrail(Color.white);

            var controller = projectile.AddComponent<PalmStrikeProjectile>();
            controller.Initialize(viewCamera.transform.forward, 12f, 35f, 4f, 2.5f);
            return projectile;
        }
    }
}

