using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// จัดการเสียงแจ้งเตือนสำหรับผู้เล่น (เช่น เสียงเตือนเมื่อไอเทมกาวหรือด้ายไม่พอ)
/// ติดตั้งคอมโพเนนต์นี้ไว้ที่ Player GameObject
/// </summary>
public class PlayerItemAudioFeedback : MonoBehaviour
{
    private static PlayerItemAudioFeedback instance;
    public static PlayerItemAudioFeedback Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<PlayerItemAudioFeedback>();
                if (instance == null)
                {
                    GameObject player = GameObject.FindGameObjectWithTag("Player");
                    if (player != null)
                    {
                        instance = player.GetComponent<PlayerItemAudioFeedback>();
                        if (instance == null)
                        {
                            instance = player.AddComponent<PlayerItemAudioFeedback>();
                        }
                    }
                }
            }
            return instance;
        }
    }

    [Header("Audio Clips")]
    [Tooltip("เสียงแจ้งเตือนทั่วไปเมื่อไอเทมไม่พอ (เช่น ตอนคราฟต์ของไม่ได้ หรือเป็นเสียงสำรองถ้าไม่ได้แยกเสียงกาว/ด้าย)")]
    public AudioClip outOfItemSound;

    [Tooltip("เสียงเมื่อกาวไม่พอ เช่น กดเล็งหรือยิงกาวตอนกาวหมด (ถ้าเว้นว่างไว้จะใช้เสียงทั่วไปด้านบน)")]
    public AudioClip outOfGlueSound;

    [Tooltip("เสียงเมื่อด้ายไม่พอ เช่น กดยิงเชือกตอนด้ายหมด (ถ้าเว้นว่างไว้จะใช้เสียงทั่วไปด้านบน)")]
    public AudioClip outOfThreadSound;

    [Header("Audio Settings")]
    [Tooltip("AudioSource บนตัว Player (ถ้าเว้นว่างไว้ ระบบจะค้นหาหรือสร้างให้อัตโนมัติ)")]
    public AudioSource audioSource;
    public AudioMixerGroup audioMixerGroup;

    [Tooltip("ระยะห่างขั้นต่ำระหว่างเสียง (วินาที) ป้องกันเสียงดังซ้อนเวลาผู้เล่นกดปุ่มรัวๆ")]
    public float soundCooldown = 0.25f;

    private float lastSoundTime = -1f;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }

        SetupAudioSource();
    }

    private void SetupAudioSource()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f; // 2D Sound
            }
        }

        if (audioMixerGroup == null)
        {
            GlueShooting glueShooting = GetComponent<GlueShooting>();
            if (glueShooting != null && glueShooting.audioMixerGroup != null)
            {
                audioMixerGroup = glueShooting.audioMixerGroup;
            }
        }

        if (audioMixerGroup != null && audioSource != null)
        {
            audioSource.outputAudioMixerGroup = audioMixerGroup;
        }
    }

    /// <summary>
    /// เล่นเสียงเตือนไอเทมไม่พอตามประเภทของไอเทม
    /// </summary>
    public void PlayOutOfItemSound(ItemManager.ItemType itemType)
    {
        if (Time.time - lastSoundTime < soundCooldown) return;

        AudioClip clipToPlay = null;

        if (itemType == ItemManager.ItemType.Glue)
        {
            clipToPlay = outOfGlueSound != null ? outOfGlueSound : (outOfItemSound != null ? outOfItemSound : outOfThreadSound);
        }
        else if (itemType == ItemManager.ItemType.Thread)
        {
            clipToPlay = outOfThreadSound != null ? outOfThreadSound : (outOfItemSound != null ? outOfItemSound : outOfGlueSound);
        }
        else
        {
            clipToPlay = outOfItemSound != null ? outOfItemSound : (outOfGlueSound != null ? outOfGlueSound : outOfThreadSound);
        }

        if (clipToPlay != null)
        {
            lastSoundTime = Time.time;
            PlayClip(clipToPlay);
        }
    }

    /// <summary>
    /// เล่นเสียงเตือนกาวไม่พอ
    /// </summary>
    public void PlayOutOfGlueSound()
    {
        PlayOutOfItemSound(ItemManager.ItemType.Glue);
    }

    /// <summary>
    /// เล่นเสียงเตือนด้ายไม่พอ
    /// </summary>
    public void PlayOutOfThreadSound()
    {
        PlayOutOfItemSound(ItemManager.ItemType.Thread);
    }

    /// <summary>
    /// เล่นเสียงเตือนทั่วไป (เช่น คราฟต์ไม่สำเร็จ / ของไม่พอ)
    /// </summary>
    public void PlayGenericOutOfItemSound()
    {
        if (Time.time - lastSoundTime < soundCooldown) return;

        if (outOfItemSound != null)
        {
            lastSoundTime = Time.time;
            PlayClip(outOfItemSound);
        }
    }

    private void PlayClip(AudioClip clip)
    {
        if (audioSource == null)
        {
            SetupAudioSource();
        }

        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}

