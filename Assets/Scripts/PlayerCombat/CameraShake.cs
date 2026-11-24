using Unity.Cinemachine;
using UnityEngine;

// =========================================================
// CAMERA SHAKE
// =========================================================
// Cinemachine Impulse shake. The Malbers "Cameras CM3" rig has
// no impulse listeners, so one is added to every Cinemachine
// camera the first time a shake is requested (and again if new
// cameras appear).
// =========================================================

public static class CameraShake
{
    private static CinemachineImpulseSource source;
    private static int listenedCameras = -1;

    /// <summary> strength ~0.3 = light bump, ~1.5 = heavy hit. </summary>
    public static void Shake(float strength)
    {
        if (strength <= 0f || Time.timeScale <= 0f) return;
        EnsureListeners();
        EnsureSource();
        source.GenerateImpulseWithVelocity(Random.onUnitSphere * strength * .5f + Vector3.down * strength * .5f);
    }

    /// <summary> Shake scaled by damage, using the limits from PlayerSpellConfig. </summary>
    public static void ShakeForDamage(float damage, PlayerSpellConfig config)
    {
        float min = config != null ? config.shakeMin : .35f;
        float max = config != null ? config.shakeMax : 1.6f;
        float full = config != null ? Mathf.Max(1f, config.shakeDamageForMax) : 60f;
        Shake(Mathf.Lerp(min, max, Mathf.Clamp01(damage / full)));
    }

    private static void EnsureSource()
    {
        if (source != null) return;
        var go = new GameObject("Camera Shake Source");
        Object.DontDestroyOnLoad(go);
        source = go.AddComponent<CinemachineImpulseSource>();
        source.ImpulseDefinition.ImpulseChannel = 1;
        source.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
        source.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Explosion;
        source.ImpulseDefinition.ImpulseDuration = .35f;
    }

    private static void EnsureListeners()
    {
        var cameras = Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (cameras.Length == listenedCameras) return;
        listenedCameras = cameras.Length;
        foreach (CinemachineCamera cam in cameras)
        {
            if (cam.GetComponent<CinemachineImpulseListener>() != null) continue;
            // Added at runtime, so Reset() never ran: set the defaults it would have set.
            var listener = cam.gameObject.AddComponent<CinemachineImpulseListener>();
            listener.ApplyAfter = CinemachineCore.Stage.Noise;
            listener.ChannelMask = 1;
            listener.Gain = 1f;
            listener.UseCameraSpace = true;
            listener.ReactionSettings = new CinemachineImpulseListener.ImpulseReaction { AmplitudeGain = 1f, FrequencyGain = 1f, Duration = .6f };
        }
    }
}
