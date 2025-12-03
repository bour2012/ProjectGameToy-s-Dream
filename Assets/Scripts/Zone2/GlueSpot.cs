using UnityEngine;

public class GlueSpot : MonoBehaviour, ISlowable
{
    public MovingSlidePlatform targetPlatform;

    // Forward slow calls to the platform so the platform manages glue counting.
    // This avoids double-counting if both GlueSpot and platform previously tracked hits.
    public void ApplySlow(float slowAmount, float duration)
    {
        if (targetPlatform == null) return;
        targetPlatform.ApplySlow(slowAmount, duration);
    }

    public void ApplyGradualSlow(float targetSlowAmount, float duration, float lerpTime)
    {
        if (targetPlatform == null) return;
        targetPlatform.ApplyGradualSlow(targetSlowAmount, duration, lerpTime);
    }
}
