using UnityEngine;

// =========================================================
// FRAME RATE CAP
// =========================================================
// The PC quality level has VSync off and nothing set a target
// frame rate, so builds rendered as fast as the GPU allowed.
// Light scenes (cinematic shots, the time-scale-0 end panels)
// then ran at hundreds/thousands of FPS, pinning the GPU and
// freezing the whole PC. Runs before the first scene loads.
// =========================================================
public static class FrameRateCap
{
    public const int TargetFps = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Apply()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFps;
    }
}
