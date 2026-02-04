using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
public class Lever : MonoBehaviour, IInteractable
{
    [Header("Multiple Objects Control")]
    [Tooltip("ใส่สิ่งที่อยากให้เกิดขึ้นตอนคันโยกทำงาน (Active)")]
    public UnityEvent onActivate;

    [Tooltip("ใส่สิ่งที่อยากให้เกิดขึ้นตอนคันโยกปิด (Inactive)")]
    public UnityEvent onDeactivate; 
    public enum ControlMode { Rotation, Animation }
    [Header("Control Mode")]
    [Tooltip("เลือกว่าจะควบคุมคันโยกด้วยการหมุน (Rotation) หรือด้วย Animation")]
    public ControlMode mode = ControlMode.Rotation;
   

    [Header("Lever Settings (Rotation Mode)")]
    [Tooltip("ส่วนคันโยกที่จะหมุน (ใช้ในโหมด Rotation)")]
    public Transform handle;
    public float activeRotation = -45f;
    public float inactiveRotation = 45f;
    public float rotationSpeed = 5f;

    [Header("Puzzle Settings (New)")]
    [Tooltip("ลาก Lever อื่นๆ ที่ต้องการให้สลับสถานะตามตัวนี้มาใส่")]
    public List<Lever> linkedLevers;
    [Tooltip("ผู้คุมกฎของ Puzzle (ถ้ามี)")]
    public LeverPuzzleManager puzzleManager;

    [Header("Boss Control")]
    [Tooltip("ลากตัวบอสที่มีสคริปต์ BossController มาใส่")]
    public BossController bossToControl;
    
    // ถ้ามีกำหนด Guard Point: เมื่อ Lever เปิด บอสจะไปเฝ้าจุดนี้
    [Tooltip("ถ้ามีค่านี้ใส่ไว้: เมื่อ Lever เปิด บอสจะบินไปเฝ้าที่จุดนี้ / ถ้าปิด บอสจะกลับไปลาดตระเวน")]
    public Transform guardPointTransform; 

    [Header("Visuals & Animation (Animation Mode)")]
    [Tooltip("Animator ของคันโยก (ใช้ในโหมด Animation)")]
    public Animator leverAnimator;
    public string activateTriggerName = "Activate";
    public string deactivateTriggerName = "Deactivate";
    [Tooltip("ระยะเวลาของ Animation (วินาที)")]
    public float animationDuration = 0.5f;

    [Header("State")]
    public bool isActive = false;

    [Header("Audio")]
    [Tooltip("เสียงที่จะเล่นเมื่อสลับคันโยก (ใช้เสียงเดียวกันสำหรับเปิด/ปิด)")]
    public AudioClip toggleSound;
    public AudioSource audioSource;

    [Header("Controlled Object")]
    public PlatformController platform;

    [Header("Interaction Text")]
    public string activatePrompt = "[E] to Activate";
    public string deactivatePrompt = "[E] to Deactivate";

    private bool isOperating = false; // เปลี่ยนชื่อเป็น isOperating เพื่อความชัดเจน
    private Coroutine operationCoroutine;

    // allow external (puzzle) toggles to call the same sequence but mark as non-player action

    void Start()
    {
        // ตั้งค่าเริ่มต้นตามโหมดที่เลือก
        if (mode == ControlMode.Rotation && handle != null)
        {
            float initialRotation = isActive ? activeRotation : inactiveRotation;
            handle.localRotation = Quaternion.Euler(0, 0, initialRotation);
        }
        // ถ้าเป็นโหมด Animation, Animator จะจัดการ Sprite เริ่มต้นเองจาก Default State
        // ตั้งค่าบอสตอนเริ่มเกม
        //UpdateBossState();

        //// ให้ Lever มี AudioSource ของตัวเอง (ถ้ายังไม่มี)
        //audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D sound
        }
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

        // ผู้เล่นกดเอง -> เริ่ม Sequence หลัก (isPlayerAction = true)
        if (operationCoroutine != null) StopCoroutine(operationCoroutine);
        operationCoroutine = StartCoroutine(ToggleLeverSequence(true));
    }

    #endregion

    // เพิ่ม parameter เพื่อแยกแยะว่าการสลับมาจากผู้เล่นหรือจากระบบ (Puzzle)
    private IEnumerator ToggleLeverSequence(bool isPlayerAction)
    {
        isOperating = true;
        // ถ้าผู้เล่นเป็นคนกด ให้เปลี่ยนสถานะเกมเป็น UsingLever
        if (isPlayerAction && GameManager.Instance != null)
            GameManager.Instance.ChangeState(GameState.UsingLever, "Using Lever");

        // 1) สลับสถานะตัวเอง
        isActive = !isActive;

        if (isActive)
        {
            onActivate.Invoke();
        }
        else
        {
            onDeactivate.Invoke();
        }

        // เล่นเสียงเดียวกันสำหรับทั้งเปิดและปิด (ถ้ามี)
        if (audioSource != null && toggleSound != null)
        {
            audioSource.PlayOneShot(toggleSound);
        }

        // 2) ถ้ามี platform ที่ผูกไว้โดยตรง ให้สั่งงาน
        if (platform != null)
        {
            platform.Toggle(isActive);
        }

        // สั่งงานบอส (ถ้ามี)
        //UpdateBossState();

        // 3) ถ้าเป็นการกดโดยผู้เล่น ให้สั่ง linked levers ให้ทำงานจากระบบ
        if (isPlayerAction && linkedLevers != null)
        {
            foreach (var lever in linkedLevers)
            {
                if (lever != null)
                {
                    lever.ToggleFromPuzzle();
                }
            }
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
        // 5) แจ้ง PuzzleManager ว่ามีการเปลี่ยนแปลง (เฉพาะเมื่อผู้เล่นกด)
        if (isPlayerAction && puzzleManager != null)
        {
            puzzleManager.CheckWinCondition();
        }

        if (isPlayerAction && GameManager.Instance != null)
            GameManager.Instance.ChangeState(GameState.Normal, "Finished using Lever");

        isOperating = false;
        operationCoroutine = null;
    }

    // ฟังก์ชันให้ Puzzle เรียกใช้ (ไม่ต้องผ่าน Interact)
    public void ToggleFromPuzzle()
    {
        if (operationCoroutine != null) StopCoroutine(operationCoroutine);
        operationCoroutine = StartCoroutine(ToggleLeverSequence(false));
    }

    //// ฟังก์ชันย่อยสำหรับสั่งบอส (เรียกใช้ทั้งตอน Start และตอนสับ)
    //private void UpdateBossState()
    //{
    //    if (bossToControl != null)
    //    {
    //        if (guardPointTransform != null)
    //        {
    //            // Lever Active -> ไปเฝ้าจุดนี้, Lever Inactive -> ยกเลิกเฝ้า
    //            if (isActive)
    //            {
    //                bossToControl.GoToGuardPoint(guardPointTransform.position);
    //            }
    //            else
    //            {
    //                bossToControl.CancelGuardPoint();
    //            }
    //        }
    //        else
    //        {
    //            // ถ้าไม่มี Guard Point ใส่ไว้ ให้ใช้ Logic เดิม (ถ้าต้องการ)
    //            // bossToControl.SetPatrolState(!isActive);
    //        }
    //    }
    //}

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