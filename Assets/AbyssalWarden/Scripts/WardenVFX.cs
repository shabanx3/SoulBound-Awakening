using System.Collections.Generic;
using UnityEngine;

namespace AbyssalWarden
{
    // Spawning / cleanup for the third-party VFX prefabs the Warden uses, plus shared area damage.
    public static class WardenVFX
    {
        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float lifetime, Transform parent = null, float scale = 1f)
        {
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, position, rotation, parent);
            if (!Mathf.Approximately(scale, 1f)) go.transform.localScale *= scale;
            if (lifetime > 0f) Object.Destroy(go, lifetime);
            return go;
        }

        // Lets looping particles and trails fade out instead of popping.
        public static void StopAndDestroy(GameObject go, float linger = 1.5f)
        {
            if (go == null) return;
            go.transform.SetParent(null, true);
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>()) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            foreach (var trail in go.GetComponentsInChildren<TrailRenderer>()) trail.emitting = false;
            foreach (var light in go.GetComponentsInChildren<Light>()) light.enabled = false;
            Object.Destroy(go, linger);
        }

        // Projectile prefabs from the packs move themselves with a Rigidbody; the Warden drives them instead.
        public static void StripMovers(GameObject go)
        {
            foreach (var behaviour in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour is WardenProjectile) continue;
                string type = behaviour.GetType().Name;
                if (type == "HS_ProjectileMover" || type == "SFB_MoveOverTime") Object.DestroyImmediate(behaviour);
            }
            foreach (var body in go.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.detectCollisions = false; }
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) col.enabled = false;
        }

        // Damages every IWardenDamageable once inside the sphere. Optional filter (e.g. a cone check).
        public static int AreaDamage(Transform owner, Vector3 center, float radius, float damage, LayerMask layers, Collider[] buffer, System.Func<Collider, bool> filter = null)
        {
            int count = Physics.OverlapSphereNonAlloc(center, radius, buffer, layers, QueryTriggerInteraction.Ignore);
            var done = new HashSet<IWardenDamageable>();
            for (int i = 0; i < count; i++)
            {
                var col = buffer[i];
                if (col == null || (owner != null && col.transform.IsChildOf(owner))) continue;
                var receiver = col.GetComponentInParent<IWardenDamageable>();
                if (receiver == null || (filter != null && !filter(col))) continue;
                if (!done.Add(receiver)) continue;
                // A failure in the target's damage or death code must not abort the attacker's action.
                try { receiver.ReceiveDamage(damage, owner); }
                catch (System.Exception e) { Debug.LogException(e, col); }
            }
            return done.Count;
        }
    }
}
