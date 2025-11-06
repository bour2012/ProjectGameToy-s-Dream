using UnityEngine;

public class GlueSpot : MonoBehaviour, ISlowable
{
    public MovingSlidePlatform targetPlatform;

    public void ApplySlow(float slowAmount, float duration)
    {
        if (targetPlatform != null)
            targetPlatform.ApplySlow(slowAmount, duration);
    }

    public void ApplyGradualSlow(float targetSlowAmount, float duration, float lerpTime)
    {
        if (targetPlatform != null)
            targetPlatform.ApplyGradualSlow(targetSlowAmount, duration, lerpTime);
    }
}
