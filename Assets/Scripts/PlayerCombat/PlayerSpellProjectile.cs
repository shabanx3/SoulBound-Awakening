using AbyssalWarden;
using UnityEngine;

// Player bolt: flies toward its target (gently homing for aim assist), damages what it hits plus a small splash.
public class PlayerSpellProjectile : MonoBehaviour
{
    private Transform owner, target;
    private float speed, homing, splashRadius;
    private int damage;
    private GameObject impactEffect;
    private AudioClip impactSound;
    private bool flying;

    public void Launch(Transform owner, Transform target, Vector3 direction, float speed, float homing, int damage,
        float splashRadius, float lifetime, GameObject impactEffect, AudioClip impactSound)
    {
        this.owner = owner; this.target = target; this.speed = speed; this.homing = homing;
        this.damage = damage; this.splashRadius = splashRadius;
        this.impactEffect = impactEffect; this.impactSound = impactSound;
        transform.rotation = Quaternion.LookRotation(direction);
        WardenVFX.StripMovers(gameObject); // pack projectiles move themselves; this script drives them
        flying = true;
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        if (!flying) return;

        if (target != null && target.gameObject.activeInHierarchy)
        {
            Vector3 to = PlayerTargeting.AimPoint(target) - transform.position;
            if (to.sqrMagnitude > .01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), homing * Time.deltaTime);
        }

        float step = speed * Time.deltaTime;
        // Layer 20 (Animal) is the player and the horses.
        RaycastHit[] hits = Physics.SphereCastAll(transform.position, .25f, transform.forward, step, ~(1 << 20), QueryTriggerInteraction.Ignore);
        RaycastHit? first = null;
        foreach (RaycastHit h in hits)
        {
            if (owner != null && h.transform.IsChildOf(owner)) continue;
            if (first == null || h.distance < first.Value.distance) first = h;
        }

        if (first != null) { Explode(first.Value); return; }
        transform.position += transform.forward * step;
    }

    private void Explode(RaycastHit hit)
    {
        flying = false;
        Vector3 point = hit.point == Vector3.zero ? transform.position : hit.point;
        GameObject direct = SpellDamage.Apply(hit.collider, damage, point, owner);
        if (splashRadius > 0f) SpellDamage.Area(point, splashRadius, Mathf.RoundToInt(damage * .4f), owner, direct);

        WardenVFX.Spawn(impactEffect, point, Quaternion.LookRotation(hit.normal.sqrMagnitude > .01f ? hit.normal : -transform.forward), 2.5f);
        if (impactSound != null) AudioSource.PlayClipAtPoint(impactSound, point);
        WardenVFX.StopAndDestroy(gameObject, .5f);
    }
}
