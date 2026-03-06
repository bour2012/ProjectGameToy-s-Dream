using UnityEngine;

public interface ISlowable
{
    void ApplySlow(float slowAmount, float duration);
    void ApplyGradualSlow(float targetSlowAmount, float duration, float lerpTime);
}
