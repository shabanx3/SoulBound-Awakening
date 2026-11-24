using System.Collections;
using AbyssalWarden;
using UnityEngine;
using UnityEngine.InputSystem;

// =========================================================
// PORTAL REVEAL CINEMATIC
// =========================================================
// Plays when the last ally task is submitted and the teleporter
// switches on. The camera flies to the portal, moves in while a
// title card shows, then returns behind the player looking at
// it, so the player knows where to go next.
// Player is frozen and protected (WardenCinematicRig).
// Hold SPACE to skip.
// =========================================================

public class PortalRevealCinematic : MonoBehaviour
{
    private const float PushInEnd = 4.5f;
    private const float OrbitEnd = 7f;
    private const float Duration = 9f;
    private const float PortalCentreHeight = 4.2f;
    private const float SkipHoldSeconds = .7f;

    private readonly WardenCinematicRig rig = new();

    private Transform player;
    private Vector3 centre, front, side;
    private bool playing, skipping;
    private float letterbox, titleAlpha, skipHeld;

    public static void Play(GameObject teleporter)
    {
        if (teleporter == null || WardenCinematicRig.Active) return;

        MenuManager menu = MenuManager.Instance;
        if (menu == null || menu.player == null) return;

        new GameObject("Portal Reveal Cinematic")
            .AddComponent<PortalRevealCinematic>()
            .Begin(teleporter.transform, menu.player.transform);
    }

    private void Begin(Transform teleporter, Transform playerTransform)
    {
        player = playerTransform;

        Transform gate = NearestGate(teleporter);
        centre = gate.position + Vector3.up * PortalCentreHeight;

        // The ring stands upright, facing along the gate's local right axis. Film the side facing the player.
        front = Vector3.ProjectOnPlane(gate.right, Vector3.up).normalized;
        if (Vector3.Dot(front, player.position - centre) < 0f) front = -front;
        side = Vector3.Cross(Vector3.up, front);

        StartCoroutine(Run());
    }

    // The teleporter holds two gates: the one by the player is the way in.
    private Transform NearestGate(Transform teleporter)
    {
        Transform best = teleporter;
        float bestDistance = float.MaxValue;

        foreach (Transform child in teleporter)
        {
            if (!child.name.StartsWith("Effect")) continue;

            float distance = (child.position - player.position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = child;
            }
        }

        return best;
    }

    private IEnumerator Run()
    {
        playing = true;

        Vector3 pushFrom = Above(centre + front * 20f + side * 5f + Vector3.up * 2f);
        Vector3 pushTo = Above(centre + front * 12f + side * 2f);

        rig.Begin(player, 1.2f, pushFrom, centre);
        CameraShake.Shake(.6f);

        float start = Time.time;

        while (!skipping && Elapsed(start) < PushInEnd)
        {
            float k = Smooth(Elapsed(start) / PushInEnd);
            titleAlpha = Mathf.Clamp01((Elapsed(start) - 1.4f) / .8f);
            rig.Shot(Vector3.Lerp(pushFrom, pushTo, k), centre, Mathf.Lerp(52f, 44f, k));
            yield return null;
        }

        while (!skipping && Elapsed(start) < OrbitEnd)
        {
            float k = (Elapsed(start) - PushInEnd) / (OrbitEnd - PushInEnd);
            titleAlpha = 1f - Mathf.Clamp01((k - .4f) * 2f);
            Vector3 orbit = Quaternion.AngleAxis(Mathf.Lerp(0f, -35f, Smooth(k)), Vector3.up) * (pushTo - centre);
            rig.Shot(Above(centre + orbit), centre, 44f);
            yield return null;
        }

        // Back behind the player, still looking at the portal.
        Vector3 orbitEnd = Above(centre + Quaternion.AngleAxis(-35f, Vector3.up) * (pushTo - centre));
        Vector3 toPortal = Vector3.ProjectOnPlane(centre - player.position, Vector3.up).normalized;
        Vector3 behindPlayer = Above(player.position - toPortal * 6f + Vector3.up * 3f);

        while (!skipping && Elapsed(start) < Duration)
        {
            float k = Smooth((Elapsed(start) - OrbitEnd) / (Duration - OrbitEnd));
            titleAlpha = 0f;
            rig.Shot(Vector3.Lerp(orbitEnd, behindPlayer, k), centre, Mathf.Lerp(44f, 55f, k));
            yield return null;
        }

        if (skipping) yield return WardenCinematicRig.WaitForSkipKeyRelease();

        Finish();
        Destroy(gameObject, 2f); // the rig restores the camera blend from this object
    }

    private void Finish()
    {
        if (!playing) return;

        playing = false;
        titleAlpha = 0f;
        rig.End(this);
    }

    private void OnDisable() => Finish();

    private void LateUpdate()
    {
        letterbox = Mathf.MoveTowards(letterbox, playing ? 1f : 0f, Time.unscaledDeltaTime * 2f);
        if (!playing) return;

        rig.KeepPlayerLocked();

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.isPressed && Time.timeScale > 0f) skipHeld += Time.unscaledDeltaTime;
        else skipHeld = 0f;

        if (skipHeld >= SkipHoldSeconds) skipping = true;
    }

    private void OnGUI()
    {
        WardenCinematicRig.DrawLetterbox(letterbox);
        if (!playing) return;

        WardenCinematicRig.DrawTitle("THE PORTAL HAS OPENED", "The way to the Warden lies before you", titleAlpha, new Color(.55f, .8f, 1f));
        WardenCinematicRig.DrawSkipHint(skipHeld, SkipHoldSeconds);
    }

    private static float Elapsed(float start) => Time.time - start;

    private static float Smooth(float k) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k));

    // Keeps the camera above uneven ground.
    private static Vector3 Above(Vector3 position)
    {
        if (Physics.Raycast(position + Vector3.up * 30f, Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore))
            position.y = Mathf.Max(position.y, hit.point.y + 1.5f);

        return position;
    }
}
