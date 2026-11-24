using UnityEngine;

namespace AbyssalWarden
{
    // Receives the animation events baked into the Demons DEMO.fbx clips (ToggleSwipe, CastStart, ThrowCast).
    // PlayAudio / StartLoop / StopLoop events go to SFB_AudioManager on the same object.
    // Must sit on the same GameObject as the Animator that plays those clips.
    public class WardenDemonFX : MonoBehaviour
    {
        public GameObject[] swipeTrails;
        public Transform spawnPos;
        public GameObject castPrefab;
        [Tooltip("True when SFB_AudioManager plays the clip sounds, so the boss skips its own one-shot sounds.")]
        public bool handlesAudio = true;
        [Tooltip("Emissive glow multiplier when the boss is hit.")]
        public float hitFlashPower = 4f;

        GameObject heldCast;
        SFB_AudioManager audioManager;
        InfinityPBR.Emissive emissive;
        Coroutine flash;

        public bool CastReleased { get; private set; }

        void Awake()
        {
            audioManager = GetComponent<SFB_AudioManager>();
            emissive = GetComponent<InfinityPBR.Emissive>();
            if (audioManager == null) handlesAudio = false;
            HideTrails();
        }

        // Animation event (attack clips): flips a weapon/claw trail on or off.
        public void ToggleSwipe(int index)
        {
            if (swipeTrails == null || index < 0 || index >= swipeTrails.Length || swipeTrails[index] == null) return;
            swipeTrails[index].SetActive(!swipeTrails[index].activeSelf);
        }

        // Animation event (cast start): spawns the charging ball in the demon's hand.
        public void CastStart()
        {
            CastReleased = false;
            if (heldCast != null || castPrefab == null || spawnPos == null) return;
            heldCast = Instantiate(castPrefab, spawnPos.position, spawnPos.rotation, spawnPos);
            var move = heldCast.GetComponent<SFB_MoveOverTime>();
            if (move != null) move.enabled = false;
        }

        // Animation event (cast end): the moment the ball leaves the hand.
        public void ThrowCast() => CastReleased = true;

        // Detaches the held ball so the boss can fire it. Spawns one if the CastStart event was skipped by a crossfade.
        public GameObject TakeCast()
        {
            if (heldCast == null) CastStart();
            var cast = heldCast;
            heldCast = null;
            if (cast != null) cast.transform.SetParent(null, true);
            return cast;
        }

        // Plays one of the SFB_AudioManager groups (e.g. "VocalizationGotHit") outside an animation event.
        public void Voice(string group)
        {
            if (audioManager != null) audioManager.SendMessage("PlayAudio", group, SendMessageOptions.DontRequireReceiver);
        }

        // Brief surge of the demon's emissive glow when it takes a hit.
        public void HitFlash()
        {
            if (emissive == null || !emissive.enabled) return;
            if (emissive.pulse) { emissive.FadeTo(emissive.maxPower, emissive.maxPower * hitFlashPower); return; }
            if (flash != null) StopCoroutine(flash);
            flash = StartCoroutine(FlashRoutine());
        }

        System.Collections.IEnumerator FlashRoutine()
        {
            for (float t = 0; t < .25f; t += Time.deltaTime)
            {
                emissive.SetEmissive(Mathf.Lerp(emissive.maxPower * hitFlashPower, emissive.maxPower, t / .25f));
                yield return null;
            }
            emissive.SetEmissive(emissive.maxPower);
            flash = null;
        }

        public void HideTrails()
        {
            if (swipeTrails == null) return;
            foreach (var trail in swipeTrails) if (trail != null) trail.SetActive(false);
        }

        // Called when an action is interrupted (stagger, phase change, death).
        public void ResetFX()
        {
            HideTrails();
            if (heldCast != null) Destroy(heldCast);
            heldCast = null;
            CastReleased = false;
            if (audioManager != null) audioManager.StopLoop();
        }
    }
}
