using UnityEngine;
using System.Collections;

public class Lever : MonoBehaviour, IInteractable
{
    // ▼▼▼ เพิ่มตัวเลือกโหมดการทำงาน ▼▼▼
    public enum ControlMode { Rotation, Animation }
    [Header("Control Mode")]
    [Tooltip("เลือกว่าจะควบคุมคันโยกด้วยการหมุน (Rotation) หรือด้วย Animation")]
    public ControlMode mode = ControlMode.Rotation;
    // ▲▲▲ สิ้นสุดส่วนที่เพิ่ม ▲▲▲

    [Header("Lever Settings (Rotation Mode)")]
    [Tooltip("ส่วนคันโยกที่จะหมุน (ใช้ในโหมด Rotation)")]
    public Transform handle;
    public float activeRotation = -45f;
    public float inactiveRotation = 45f;
    public float rotationSpeed = 5f;

    [Header("Visuals & Animation (Animation Mode)")]
    [Tooltip("Animator ของคันโยก (ใช้ในโหมด Animation)")]
    public Animator leverAnimator;
    public string activateTriggerName = "Activate";
    public string deactivateTriggerName = "Deactivate";
    [Tooltip("ระยะเวลาของ Animation (วินาที)")]
    public float animationDuration = 0.5f;

    [Header("State")]
    public bool isActive = false;

    [Header("Controlled Object")]
    public PlatformController platform;

    [Header("Interaction Text")]
    public string activatePrompt = "[E] to Activate";
    public string deactivatePrompt = "[E] to Deactivate";

    private bool isOperating = false; // เปลี่ยนชื่อเป็น isOperating เพื่อความชัดเจน
    private Coroutine operationCoroutine;

    void Start()
    {
        // ตั้งค่าเริ่มต้นตามโหมดที่เลือก
        if (mode == ControlMode.Rotation && handle != null)
        {
            float initialRotation = isActive ? activeRotation : inactiveRotation;
            handle.localRotation = Quaternion.Euler(0, 0, initialRotation);
        }
        // ถ้าเป็นโหมด Animation, Animator จะจัดการ Sprite เริ่มต้นเองจาก Default State
    }

    #region IInteractable Implementation

    public string GetInteractText()
    {
        return isActive ? deactivatePrompt : activatePrompt;
    }

    public void Interact()
    {
        if (isOperating || (GameManager.Instance != null && GameManager.Instance.currentState != GameState.Normal))
        {
            return;
        }

        // เริ่ม Coroutine หลัก ซึ่งจะไปเลือกว่าจะทำงานแบบไหน
        operationCoroutine = StartCoroutine(ToggleLeverSequence());
    }

    #endregion

    private IEnumerator ToggleLeverSequence()
    {
        isOperating = true;
        if (GameManager.Instance != null)
            GameManager.Instance.ChangeState(GameState.UsingLever, "Using Lever");

        isActive = !isActive;

        if (platform != null)
        {
            platform.Toggle(isActive);
        }

        // ▼▼▼ ส่วนสำคัญ: เลือกการทำงานตามโหมด ▼▼▼
        switch (mode)
        {
            case ControlMode.Rotation:
                if (handle != null)
                {
                    float targetRotation = isActive ? activeRotation : inactiveRotation;
                    yield return StartCoroutine(RotateHandle(targetRotation));
                }
                break;

            case ControlMode.Animation:
                if (leverAnimator != null)
                {
                    leverAnimator.SetTrigger(isActive ? activateTriggerName : deactivateTriggerName);
                    yield return new WaitForSeconds(animationDuration);
                }
                break;
        }
        // ▲▲▲ สิ้นสุดส่วนที่เลือกการทำงาน ▲▲▲

        if (GameManager.Instance != null)
            GameManager.Instance.ChangeState(GameState.Normal, "Finished using Lever");

        isOperating = false;
        operationCoroutine = null;
    }

    // Coroutine สำหรับโหมด Rotation (เหมือนเดิม)
    private IEnumerator RotateHandle(float targetAngle)
    {
        Quaternion startRotation = handle.localRotation;
        Quaternion endRotation = Quaternion.Euler(0, 0, targetAngle);
        float time = 0;

        while (time < 1f)
        {
            handle.localRotation = Quaternion.Slerp(startRotation, endRotation, time);
            time += Time.deltaTime * rotationSpeed;
            yield return null;
        }
        handle.localRotation = endRotation;
    }
}