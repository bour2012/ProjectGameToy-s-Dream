using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events; 

/// <summary>
/// ระบบการตายของผู้เล่นแบบรวมทุกอย่างไว้ในที่เดียว
/// จัดการทั้งการตาย, เอฟเฟกต์, Respawn, และการจัดการสถานะเกม
/// </summary>
public class PlayerDeathSystem : MonoBehaviour
{
    [System.Serializable]
    public enum DeathType
    {
        InstantDeath,       // ตายทันที
        Spikes,            // หนาม
        Enemy,             // ศัตรู
        Lava,              // ลาวา
        Void,              // เหว
        Trap               // กับดัก
    }

    [Header("Death Settings")]
    [Tooltip("เวลาเฟดเอาท์เมื่อตาย (วินาที)")]
    public float deathFadeTime = 0.5f;

    [Tooltip("เวลาหน่วงก่อน Respawn (วินาที)")]
    public float respawnDelay = 0.2f;

    [Header("Visual Effects")]
    [Tooltip("Effect ที่เล่นเมื่อตาย")]
    public ParticleSystem deathEffect;

    [Tooltip("SpriteRenderer ของ Player")]
    public SpriteRenderer playerSprite;

    [Tooltip("CanvasGroup สำหรับเฟดหน้าจอ (ถ้ามี)")]
    public CanvasGroup fadeCanvasGroup;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip deathSound;

    [Header("Game State Management")]
    [Tooltip("สถานะที่จะบังคับเมื่อผู้เล่นตาย")]
    public GameState deathState = GameState.Normal;

    [Tooltip("หยุดระบบต่างๆ ระหว่างตาย")]
    public bool pauseSystemsDuringDeath = true;

    public DeathDialogManager deathDialogManager;
    public UnityEvent onPlayerDies; // UnityEvent สำหรับเหตุการณ์อื่นๆ

    [Header("Debug")]
    public bool showDeathStateDebug = true;

    // Private variables
    private PlayerMovement playerMovement;
    private Rigidbody2D playerRigidbody;
    //private Collider2D playerCollider;
   private GameManager checkpointManager;
    private PlayerController playerController;
    private GameState previousGameState;
    private bool isDead = false;
    //private bool wasPlayerDeadLastFrame = false;
    private Vector3 originalScale;
    private Color originalColor;
    // instance of the death particle prefab spawned on death (if any)
    private ParticleSystem currentDeathEffectInstance = null;

    private void Awake()
    {
        // หา Components
        playerMovement = GetComponent<PlayerMovement>();
        playerRigidbody = GetComponent<Rigidbody2D>();
        //playerCollider = GetComponent<Collider2D>();
        playerController = GetComponent<PlayerController>();

        // หา CheckpointManager
        checkpointManager = FindFirstObjectByType<GameManager>();

        // เก็บค่าเริ่มต้น
        originalScale = transform.localScale;
        if (playerSprite != null)
        {
            originalColor = playerSprite.color;
        }

        // ตั้งค่า AudioSource อัตโนมัติถ้าไม่มี
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
    }

    private void Update()
    {
        //HandleDeathStateTransitions();
        HandleDebugInput();
    }

    //#region Death State Management
    ////private void HandleDeathStateTransitions()
    ////{
    ////    bool isPlayerDead = isDead;

    ////    // ตรวจสอบการเปลี่ยนแปลงสถานะการตาย
    ////    if (isPlayerDead && !wasPlayerDeadLastFrame)
    ////    {
    ////        // ผู้เล่นเพิ่งตาย
    ////        OnPlayerDied();
    ////    }
    ////    else if (!isPlayerDead && wasPlayerDeadLastFrame)
    ////    {
    ////        // ผู้เล่นเพิ่ง Respawn
    ////        OnPlayerRespawned();
    ////    }

    ////    wasPlayerDeadLastFrame = isPlayerDead;
    ////}

    ////private void OnPlayerDied()
    ////{
    ////    if (showDeathStateDebug)
    ////    {
    ////        Debug.Log("PlayerDeathSystem: Player died - managing game state");
    ////    }

    ////    // จำสถานะปัจจุบันของเกม
    ////    if (GameManager.Instance != null)
    ////    {
    ////        previousGameState = GameManager.Instance.currentState;

    ////        if (pauseSystemsDuringDeath)
    ////        {
    ////            // บังคับให้เกมเข้าสู่สถานะที่กำหนด
    ////            GameManager.Instance.ChangeState(deathState, "Player died - forcing safe state");

    ////            if (showDeathStateDebug)
    ////            {
    ////                Debug.Log($"Forced game state to {deathState} during player death");
    ////            }
    ////        }
    ////    }

    ////    // หยุดระบบอื่นๆ ที่อาจรบกวนการตาย
    ////    PauseGameSystems();
    ////}

    //private void OnPlayerRespawned()
    //{
    //    if (showDeathStateDebug)
    //    {
    //        Debug.Log("PlayerDeathSystem: Player respawned - restoring game state");
    //    }

    //    // คืนค่าระบบต่างๆ
    //    ResumeGameSystems();

    //    // คืนค่าสถานะเกม
    //    if (GameManager.Instance != null)
    //    {
    //        GameManager.Instance.ChangeState(GameState.Normal, "Player respawned - returning to normal");
    //    }
    //}

    //private void PauseGameSystems()
    //{
    //    if (!pauseSystemsDuringDeath) return;

    //    // ใช้ GameManager เปลี่ยนสถานะเกมเพื่อหยุดระบบทั้งหมด
    //    if (GameManager.Instance != null)
    //    {
    //        GameManager.Instance.ChangeState(deathState, "PauseGameSystems called by PlayerDeathSystem");
    //        if (showDeathStateDebug)
    //        {
    //            Debug.Log($"PauseGameSystems: Changed GameState to {deathState}");
    //        }
    //    }

    //    // (ถ้าต้องการหยุดเสียงอื่นๆ เพิ่มเติม สามารถคงโค้ด AudioSource ไว้)
    //    var audioSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);

    //    foreach (var audio in audioSources)
    //    {
    //        if (audio.gameObject != gameObject && audio.isPlaying)
    //        {
    //            audio.Pause();
    //        }
    //    }
    //}

    //private void ResumeGameSystems()
    //{
    //    if (!pauseSystemsDuringDeath) return;

    //    // เปิด Audio Sources กลับ
    //    var audioSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);

    //    foreach (var audio in audioSources)
    //    {
    //        if (audio.gameObject != gameObject)
    //        {
    //            audio.UnPause();
    //        }
    //    }

    //    if (showDeathStateDebug)
    //    {
    //        Debug.Log("Resumed game systems after player respawn");
    //    }
    //}
    //#endregion

    #region Death & Spawn System
    public void Die(DeathType deathType)
    {
        if (isDead) return;

        isDead = true;

        //enabled = false;

        // แจ้ง GameManager เปลี่ยน state
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ChangeState(GameState.Dead, "Player died");
            onPlayerDies.Invoke();

            if (deathDialogManager != null && GameManager.Instance != null)
            {
                DialogTrigger dialogToPlay = deathDialogManager.GetCurrentDeathDialog();
                if (dialogToPlay != null)
                {
                    GameManager.Instance.dialogIDToPlayOnRespawn = dialogToPlay.dialogID;
                    Debug.Log("<color=orange>DIALOG ID SENT TO GAMEMANAGER: </color>" + dialogToPlay.dialogID);
                }
                else
                {
                    Debug.Log("<color=red>FAILED TO GET DIALOG! dialogToPlay is null or has no ID.</color>");
                }
            }

            // DO NOT call ManualReset immediately here - we want to play the death particle/sound and fade
            // before performing the respawn/reset. The DeathSequence coroutine will handle the reset.
        }


        // 1. หยุดการเคลื่อนไหว
        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector2.zero;
            playerRigidbody.angularVelocity = 0f;
            // ปิดการใช้ Physics ชั่วคราวแทนการซ่อนตัว Object
            playerRigidbody.simulated = false;
        }

        // 2. ปิดการควบคุม
        if (playerMovement != null) playerMovement.enabled = false;
        if (playerController != null) playerController.SetControlEnabled(false);

        // 3. ปิด Collider ไม่ให้ชนกับอะไรอีก
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // 4. ซ่อนเฉพาะภาพ (Sprite) ของ Player แทนการปิด GameObject ทั้งตัว
        if (playerSprite != null)
        {
            playerSprite.enabled = false;
        }

        // หากมีลูกที่เป็น Camera ให้ปลดออกชั่วคราว
        Camera childCam = GetComponentInChildren<Camera>(true);
        if (childCam != null && childCam.transform.IsChildOf(transform))
        {
            childCam.transform.SetParent(null);
            if (showDeathStateDebug) Debug.Log("PlayerDeathSystem: Detached child camera to preserve view during death sequence");
        }

        // เล่นเสียงตาย
        if (audioSource != null && deathSound != null)
        {
            audioSource.PlayOneShot(deathSound);
        }

        // เล่น Effect ตาย
        if (deathEffect != null)
        {
            ParticleSystem ps = Instantiate(deathEffect, transform.position, Quaternion.identity);
            ps.Play();
            currentDeathEffectInstance = ps;

            var main = ps.main;
            float maxLifetime = main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants ? main.startLifetime.constantMax : main.startLifetime.constant;
            float destroyAfter = main.duration + maxLifetime + 0.25f;
            Destroy(ps.gameObject, destroyAfter);
        }

        Debug.Log($"Player ตายจาก: {deathType}");

        // เริ่มกระบวนการ Respawn
        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        yield return StartCoroutine(FadeOut());
        // wait a short respawn delay first (timing for UI/fade)
        yield return new WaitForSeconds(respawnDelay);

        // If we spawned a death particle instance, wait for it to finish playing
        if (currentDeathEffectInstance != null)
        {
            // Wait until particle system is no longer alive (all particles finished)
            yield return new WaitUntil(() => currentDeathEffectInstance == null || !currentDeathEffectInstance.IsAlive(true));
            // give a tiny buffer
            yield return new WaitForSeconds(0.05f);
            currentDeathEffectInstance = null;
        }

        // บันทึก checkpoint ID และข้อมูลสำคัญลง PlayerPrefs ก่อน reload
        if (checkpointManager != null)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ChangeState(GameState.Normal, "Player Respawn");
            }

            var checkpoint = checkpointManager.GetCurrentCheckpoint();
            if (checkpoint != null)
            {
                PlayerPrefs.SetString("LastCheckpoint", checkpoint.GetCheckpointID());
            }
            // เซฟจำนวนไอเทม
            PlayerPrefs.SetInt("GlueCount", ItemManager.Instance.GetItemCount(ItemManager.ItemType.Glue));
            PlayerPrefs.SetInt("ThreadCount", ItemManager.Instance.GetItemCount(ItemManager.ItemType.Thread));
            PlayerPrefs.Save();
        }

        // Reload Scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private IEnumerator FadeOut()
    {
        float timer = 0f;

        while (timer < deathFadeTime)
        {
            timer += Time.deltaTime;
            float progress = timer / deathFadeTime;

            // เฟด Sprite
            if (playerSprite != null)
            {
                Color color = originalColor;
                color.a = Mathf.Lerp(1f, 0f, progress);
                playerSprite.color = color;
            }

            // ลดขนาด
            transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, progress);

            // เฟดหน้าจอ (ถ้ามี)
            if (fadeCanvasGroup != null)
            {
                fadeCanvasGroup.alpha = Mathf.Lerp(0f, 1f, progress);
            }

            yield return null;
        }
    }

    private IEnumerator FadeIn()
    {
        float timer = 0f;

        while (timer < deathFadeTime)
        {
            timer += Time.deltaTime;
            float progress = timer / deathFadeTime;

            // เฟดกลับ Sprite
            if (playerSprite != null)
            {
                Color color = originalColor;
                color.a = Mathf.Lerp(0f, 1f, progress);
                playerSprite.color = color;
            }

            // คืนขนาด
            transform.localScale = Vector3.Lerp(Vector3.zero, originalScale, progress);

            // เฟดหน้าจอกลับ (ถ้ามี)
            if (fadeCanvasGroup != null)
            {
                fadeCanvasGroup.alpha = Mathf.Lerp(1f, 0f, progress);
            }

            yield return null;
        }
    }

    public void Respawn(Vector3 spawnPosition)
    {
        // ย้ายไปยังตำแหน่งใหม่
        transform.position = spawnPosition;

        // คืนค่าต่างๆ
        isDead = false; 

        // แจ้ง GameManager กลับสู่สถานะปกติ
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ChangeState(GameState.Normal, "Player respawned - returning to normal");
        }
        transform.localScale = originalScale;

        if (playerSprite != null)
        {
            playerSprite.enabled = true;
            playerSprite.color = originalColor;
        }

        // รีเซ็ต Physics
        if (playerRigidbody != null)
        {
            playerRigidbody.simulated = true;
            playerRigidbody.linearVelocity = Vector2.zero;
            playerRigidbody.angularVelocity = 0f;
        }
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = true; // [เพิ่ม] เปิด Collider กลับมา
        // เปิดการควบคุมกลับ
        if (playerMovement != null)
        {
            playerMovement.enabled = true;
        }

        if (playerController != null)
        {
            playerController.SetControlEnabled(true);
        }

        // รีเซ็ตสถานะของ PlayerMovement (ถ้าจำเป็น)
        ResetPlayerMovementState();

        // เฟดอิน
        StartCoroutine(FadeIn());

        //Debug.Log($"Player Respawn ที่ตำแหน่ง: {spawnPosition}");
    }

    private void ResetPlayerMovementState()
    {
        if (playerMovement != null)
        {
            // เรียกใช้ method ที่เพิ่งสร้างใน PlayerMovement
            playerMovement.ResetMovementState();
        }
    }
    #endregion

    #region Death Zone Integration
    // ฟังก์ชันสำหรับ Death Zone เรียกใช้โดยตรง
    private void OnTriggerEnter2D(Collider2D other)
    {
        // สำหรับกรณีที่ติด component นี้ใน Death Zone
        DeathZoneComponent deathZone = GetComponent<DeathZoneComponent>();
        if (deathZone != null && other.CompareTag("Player"))
        {
            PlayerDeathSystem playerDeath = other.GetComponent<PlayerDeathSystem>();
            if (playerDeath != null)
            {
                playerDeath.Die(deathZone.deathType);
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // สำหรับกรณีที่ติด component นี้ใน Death Zone
        DeathZoneComponent deathZone = GetComponent<DeathZoneComponent>();
        if (deathZone != null && collision.gameObject.CompareTag("Player"))
        {
            PlayerDeathSystem playerDeath = collision.gameObject.GetComponent<PlayerDeathSystem>();
            if (playerDeath != null)
            {
                playerDeath.Die(deathZone.deathType);
            }
        }
    }
    #endregion

    #region Public Interface
    public bool IsDead()
    {
        return isDead;
    }

    public void InstantKill()
    {
        Die(DeathType.InstantDeath);
    }

    public bool IsPlayerCurrentlyDead()
    {
        return isDead;
    }

    public GameState GetPreviousGameState()
    {
        return previousGameState;
    }
    #endregion

    #region Debug
    private void HandleDebugInput()
    {
        // ปุ่มดีบัก - ฆ่าตัวเองทันที (เฉพาะ Development)
        if (Input.GetKeyDown(KeyCode.K) && Debug.isDebugBuild)
        {
            Die(DeathType.InstantDeath);
        }
    }

    //private void OnGUI()
    //{
    //    if (!showDeathStateDebug) return;

    //    GUILayout.BeginArea(new Rect(10, 270, 300, 100));

    //    string deathStatus = isDead ? "DEAD" : "ALIVE";
    //    Color oldColor = GUI.color;
    //    GUI.color = isDead ? Color.red : Color.green;
    //    GUILayout.Label($"Player Status: {deathStatus}", GUI.skin.box);
    //    GUI.color = oldColor;

    //    if (GameManager.Instance != null)
    //    {
    //        GUILayout.Label($"Game State: {GameManager.Instance.currentState}", GUI.skin.box);
    //        GUILayout.Label($"Previous State: {previousGameState}", GUI.skin.box);
    //    }

    //    GUILayout.EndArea();
    //}
    #endregion
}