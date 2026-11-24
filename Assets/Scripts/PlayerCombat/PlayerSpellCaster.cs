using System.Collections;
using AbyssalWarden;
using MalbersAnimations;
using MalbersAnimations.Controller;
using MalbersAnimations.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;

// =========================================================
// PLAYER SPELL CASTER
// =========================================================
// Z  Arcane Fire Bolt  - homing projectile (aim assist / lock-on)
// X  Thunder Strike    - lightning on a locked / nearby enemy
// G  Frost Nova        - burst around the player
// T  Shockwave         - damages and stuns enemies around the player
// A sword in hand is stored (Malbers store mode) before the cast
// and drawn again afterwards. Cast gestures go through Malbers
// modes so the controller stays in charge of the character.
// Values, keys, VFX and SFX: Resources/PlayerSpellConfig.
// =========================================================

[RequireComponent(typeof(PlayerTargeting))]
public class PlayerSpellCaster : MonoBehaviour
{
    public PlayerSpellConfig config;

    [Tooltip("Shockwave stun length on the Warden, relative to normal enemies.")]
    [SerializeField] private float bossStunMultiplier = .5f;

    private PlayerTargeting targeting;
    private MAnimal animal;
    private MWeaponManager weapons;
    private Animator animator;
    private float boltReady, thunderReady, novaReady, shockReady;
    private bool casting, leftHand;
    private string message;
    private float messageUntil;

    // Weapon stored for the current cast (drawn again afterwards).
    private MWeapon storedWeapon;
    private int storedHolster;

    private const int LayerPlayer = 20;

    private void Awake()
    {
        targeting = GetComponent<PlayerTargeting>();
        animal = GetComponent<MAnimal>();
        if (animal == null) animal = GetComponentInChildren<MAnimal>();
        weapons = GetComponent<MWeaponManager>();
        if (weapons == null) weapons = GetComponentInChildren<MWeaponManager>();
        animator = animal != null && animal.Anim != null ? animal.Anim : GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (!CanCast() || Keyboard.current == null) return;

        if (Pressed(config.fireBolt) && Time.time >= boltReady) StartCoroutine(CastFireBolt());
        else if (Pressed(config.thunder) && Time.time >= thunderReady) StartCoroutine(CastThunder());
        else if (Pressed(config.frostNova) && Time.time >= novaReady) StartCoroutine(CastFrostNova());
        else if (Pressed(config.shockwave) && Time.time >= shockReady) StartCoroutine(CastShockwave());
    }

    private static bool Pressed(PlayerSpellConfig.Spell spell)
    {
        if (spell.key == Key.None) return false;
        var control = Keyboard.current[spell.key];
        return control != null && control.wasPressedThisFrame;
    }

    private bool CanCast()
    {
        if (casting || Time.timeScale <= 0f) return false;
        if (MenuManager.Instance != null && MenuManager.Instance.gameState != GameState.Gameplay) return false;
        if (animal != null && (animal.Sleep || animal.LockInput || !animal.enabled)) return false;
        return true;
    }

    // ---------------------------------------------------------------- spells
    private IEnumerator CastFireBolt()
    {
        PlayerSpellConfig.Spell spell = config.fireBolt;
        boltReady = Time.time + spell.cooldown;
        casting = true;
        yield return StoreWeapon();

        Transform target = targeting.AimAssistTarget();
        FaceTowards(target != null ? target.position : targeting.CrosshairPoint(60f));
        bool left = leftHand; leftHand = !leftHand;
        yield return PlayAnimation(spell.modeID, left ? config.fireBoltAltAbility : spell.ability);
        Play(spell.castSound, transform.position);

        yield return new WaitForSeconds(spell.releaseDelay);

        Vector3 origin = HandPosition(left);
        Vector3 aim = target != null ? PlayerTargeting.AimPoint(target) : targeting.CrosshairPoint(60f);
        Vector3 dir = (aim - origin).sqrMagnitude > .01f ? (aim - origin).normalized : transform.forward;
        WardenVFX.Spawn(spell.castEffect, origin, Quaternion.LookRotation(dir), 1.5f, null, spell.effectScale);

        GameObject bolt = spell.mainEffect != null
            ? Instantiate(spell.mainEffect, origin, Quaternion.LookRotation(dir))
            : CreateFallbackBolt(origin);
        if (!Mathf.Approximately(spell.effectScale, 1f)) bolt.transform.localScale *= spell.effectScale;
        bolt.AddComponent<PlayerSpellProjectile>().Launch(transform, target, dir, config.boltSpeed, target != null ? config.boltHoming : 0f,
            spell.damage, config.boltSplashRadius, config.boltLifetime, spell.impactEffect, spell.impactSound);

        yield return new WaitForSeconds(.2f);
        yield return DrawWeapon();
        casting = false;
    }

    private IEnumerator CastThunder()
    {
        PlayerSpellConfig.Spell spell = config.thunder;

        // Thunder needs something to strike: the lock, else the best enemy in view, else the nearest one in range.
        Transform target = targeting.LockedTarget;
        if (target == null) target = targeting.FindTarget(config.thunderRange, config.thunderSearchAngle);
        if (target == null) target = targeting.FindTarget(config.thunderRange, 180f);
        if (target == null) { ShowMessage("No enemy in range"); yield break; }

        thunderReady = Time.time + spell.cooldown;
        casting = true;
        yield return StoreWeapon();

        FaceTowards(target.position);
        yield return PlayAnimation(spell.modeID, spell.ability);
        Play(spell.castSound, transform.position);
        GameObject mark = WardenVFX.Spawn(spell.castEffect, Ground(target.position) + Vector3.up * .05f, Quaternion.identity, 0f);
        StartCoroutine(ThunderLands(spell, target, mark));

        yield return new WaitForSeconds(spell.releaseDelay + .2f);
        yield return DrawWeapon();
        casting = false;
    }

    // The mark follows the target until the lightning lands.
    private IEnumerator ThunderLands(PlayerSpellConfig.Spell spell, Transform target, GameObject mark)
    {
        Vector3 spot = Ground(target.position);
        for (float t = 0; t < spell.releaseDelay + config.thunderDelay; t += Time.deltaTime)
        {
            if (target != null && target.gameObject.activeInHierarchy) spot = Ground(target.position);
            if (mark != null) mark.transform.position = spot + Vector3.up * .05f;
            yield return null;
        }

        WardenVFX.StopAndDestroy(mark, .5f);
        WardenVFX.Spawn(spell.mainEffect, spot, Quaternion.identity, 3f, null, spell.effectScale);
        WardenVFX.Spawn(spell.impactEffect, spot + Vector3.up * .3f, Quaternion.identity, 3f);
        Play(spell.impactSound, spot);
        SpellDamage.Area(spot + Vector3.up, config.thunderRadius, spell.damage, transform);
        if (Vector3.Distance(spot, transform.position) < 12f) CameraShake.Shake(.3f);
    }

    private IEnumerator CastFrostNova()
    {
        PlayerSpellConfig.Spell spell = config.frostNova;
        novaReady = Time.time + spell.cooldown;
        casting = true;
        yield return StoreWeapon();

        yield return PlayAnimation(spell.modeID, spell.ability);
        Play(spell.castSound, transform.position);
        GameObject charge = WardenVFX.Spawn(spell.castEffect, transform.position, transform.rotation, 0f, transform);

        yield return new WaitForSeconds(spell.releaseDelay);

        WardenVFX.StopAndDestroy(charge, .8f);
        Vector3 center = transform.position;
        WardenVFX.Spawn(spell.mainEffect, center + Vector3.up * .2f, Quaternion.identity, 4f, null, spell.effectScale);
        Play(spell.impactSound, center);
        SpellDamage.Area(center + Vector3.up, config.novaRadius, spell.damage, transform);
        CameraShake.Shake(.4f);

        yield return new WaitForSeconds(.4f);
        yield return DrawWeapon();
        casting = false;
    }

    private IEnumerator CastShockwave()
    {
        PlayerSpellConfig.Spell spell = config.shockwave;
        shockReady = Time.time + spell.cooldown;
        casting = true;
        yield return StoreWeapon();

        yield return PlayAnimation(spell.modeID, spell.ability);
        Play(spell.castSound, transform.position);
        WardenVFX.Spawn(spell.castEffect, transform.position + Vector3.up * .1f, Quaternion.identity, 1.5f, null, 1.5f);

        yield return new WaitForSeconds(spell.releaseDelay);

        Vector3 center = Ground(transform.position);
        WardenVFX.Spawn(spell.mainEffect, center + Vector3.up * .1f, Quaternion.identity, 3f, null, spell.effectScale);
        WardenVFX.Spawn(spell.impactEffect, center + Vector3.up * .5f, Quaternion.identity, 3f, null, 2f);
        Play(spell.impactSound, center);
        CameraShake.Shake(.6f);
        foreach (GameObject enemy in SpellDamage.Area(center + Vector3.up, config.shockRadius, spell.damage, transform))
            EnemyStun.Apply(enemy, config.stunSeconds, config.stunEffect, config.stunEffectScale, bossStunMultiplier);

        yield return new WaitForSeconds(.4f);
        yield return DrawWeapon();
        casting = false;
    }

    // ---------------------------------------------------------------- weapon
    // A weapon in hand blocks the unarmed cast gestures: put it away first (animated, like scrolling down)...
    private IEnumerator StoreWeapon()
    {
        storedWeapon = null;
        if (!config.storeWeaponForSpells || weapons == null || weapons.Weapon == null) yield break;

        storedWeapon = weapons.Weapon;
        storedHolster = storedWeapon.Holster != null ? storedWeapon.Holster.ID : 1;
        if (animal != null) animal.Mode_TryActivate(config.storeWeaponModeID, storedHolster);

        float t = 0;
        while (weapons.Weapon != null && t < config.storeWeaponTimeout) { t += Time.deltaTime; yield return null; }
        if (weapons.Weapon != null) weapons.UnEquip_Fast();

        // Let the store gesture finish so the cast mode can start.
        t = 0;
        while (animal != null && animal.IsPlayingMode && t < .5f) { t += Time.deltaTime; yield return null; }
    }

    // ...and draw it again once the spell is out.
    private IEnumerator DrawWeapon()
    {
        if (storedWeapon == null || weapons == null) yield break;
        MWeapon weapon = storedWeapon;
        storedWeapon = null;

        float t = 0;
        while (animal != null && animal.IsPlayingMode && t < 1.2f) { t += Time.deltaTime; yield return null; }
        if (weapons.Weapon != null) yield break; // the player already drew something

        if (animal != null) animal.Mode_TryActivate(config.drawWeaponModeID, storedHolster);
        t = 0;
        while (weapons.Weapon == null && t < 1f) { t += Time.deltaTime; yield return null; }
        if (weapons.Weapon == null && weapon != null) weapons.Equip_Fast(weapon);
    }

    private void OnDisable()
    {
        // Interrupted mid-cast (death, scene change): never leave the sword in the holster.
        if (storedWeapon != null && weapons != null && weapons.Weapon == null && weapons.isActiveAndEnabled) weapons.Equip_Fast(storedWeapon);
        storedWeapon = null;
        casting = false;
    }

    // ---------------------------------------------------------------- helpers
    private IEnumerator PlayAnimation(int modeID, int ability)
    {
        if (animal == null) yield break;
        // Retry briefly: the previous mode (e.g. weapon store) may still be exiting.
        for (float t = 0; t < .35f; t += Time.deltaTime)
        {
            if (animal.Mode_TryActivate(modeID, ability)) yield break;
            yield return null;
        }
    }

    private void FaceTowards(Vector3 point)
    {
        Vector3 dir = point - transform.position; dir.y = 0;
        if (dir.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(dir);
    }

    private Vector3 HandPosition(bool left)
    {
        if (animator != null && animator.isHuman)
        {
            Transform hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            if (hand != null) return hand.position + transform.forward * .3f;
        }
        return transform.position + Vector3.up * 1.4f + transform.forward * .8f + transform.right * (left ? -.3f : .3f);
    }

    // Ground under a point, ignoring the player/horses and the enemy's own body.
    private Vector3 Ground(Vector3 point)
    {
        RaycastHit[] hits = Physics.RaycastAll(point + Vector3.up * 3f, Vector3.down, 30f, ~(1 << LayerPlayer), QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Vector3 ground = point;
        foreach (RaycastHit h in hits)
        {
            if (h.transform.GetComponentInParent<AbyssalWardenBoss>() != null || h.transform.GetComponentInParent<EmeraldAI.EmeraldSystem>() != null) continue;
            if (h.distance < best) { best = h.distance; ground = h.point; }
        }
        return ground;
    }

    private static void Play(AudioClip clip, Vector3 position)
    {
        if (clip != null) AudioSource.PlayClipAtPoint(clip, position);
    }

    private static GameObject CreateFallbackBolt(Vector3 origin)
    {
        var bolt = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(bolt.GetComponent<Collider>());
        bolt.transform.position = origin;
        bolt.transform.localScale = Vector3.one * .3f;
        return bolt;
    }

    private void ShowMessage(string text)
    {
        message = text;
        messageUntil = Time.time + 1.5f;
    }

    // ---------------------------------------------------------------- HUD
    private void OnGUI()
    {
        if (MenuManager.Instance != null && MenuManager.Instance.gameState != GameState.Gameplay) return;
        if (WardenCinematicRig.Active) return;

        const float w = 110f, h = 44f, gap = 8f;
        float x = (Screen.width - (w * 4 + gap * 3)) * .5f, y = Screen.height - h - 18f;
        DrawSlot(new Rect(x, y, w, h), config.fireBolt, boltReady);
        DrawSlot(new Rect(x + (w + gap), y, w, h), config.thunder, thunderReady);
        DrawSlot(new Rect(x + (w + gap) * 2, y, w, h), config.frostNova, novaReady);
        DrawSlot(new Rect(x + (w + gap) * 3, y, w, h), config.shockwave, shockReady);

        Transform locked = targeting.LockedTarget;
        if (locked != null)
            GUI.Label(new Rect(Screen.width * .5f - 120f, y - 26f, 240f, 22f), $"<b>LOCKED:</b> {locked.name}", Centered());
        if (Time.time < messageUntil)
            GUI.Label(new Rect(Screen.width * .5f - 150f, y - 50f, 300f, 22f), $"<color=#FF8A7A><b>{message}</b></color>", Centered());
    }

    private static void DrawSlot(Rect r, PlayerSpellConfig.Spell spell, float readyAt)
    {
        float remaining = Mathf.Max(0f, readyAt - Time.time);
        GUI.color = new Color(0f, 0f, 0f, .6f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        if (remaining > 0f)
        {
            GUI.color = new Color(.2f, .45f, 1f, .45f);
            float k = Mathf.Clamp01(remaining / Mathf.Max(.01f, spell.cooldown));
            GUI.DrawTexture(new Rect(r.x, r.y + r.height * (1f - k), r.width, r.height * k), Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
        string label = $"<b>[{spell.key}]</b> {spell.name}" + (remaining > 0f ? $"\n{remaining:0.0}s" : "\nREADY");
        GUI.Label(r, label, Centered());
    }

    private static GUIStyle centered;
    private static GUIStyle Centered()
    {
        if (centered == null) centered = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, fontSize = 13 };
        return centered;
    }
}
