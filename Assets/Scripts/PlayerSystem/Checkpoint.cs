using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    [Header("Checkpoint Settings")]
    [Tooltip("ID ของ Checkpoint นี้ (ไม่ซ้ำกัน)")]
    public string checkpointID;

    [Header("Visual Settings")]
    public bool showGizmo = true;
    public Color inactiveColor = Color.gray;
    public Color activeColor = Color.green;

    [Header("Optional Components")]
    [Tooltip("Animator สำหรับ Animation (ถ้ามี)")]
    public Animator checkpointAnimator;

    [Tooltip("SpriteRenderer สำหรับเปลี่ยนสี (ถ้ามี)")]
    public SpriteRenderer checkpointSprite;

    [Tooltip("ParticleSystem สำหรับ Effect (ถ้ามี)")]
    public ParticleSystem activationEffect;

    [Tooltip("AudioSource สำหรับเสียง (ถ้ามี)")]
    public AudioSource audioSource;
    public AudioClip activationSound;

    [Header("Item Defaults")]
    public int glueCount = 3;
    public int threadCount = 2;

    private bool isActivated = false;
    private GameManager checkpointManager;

    private void Start()
    {
        // หา CheckpointManager ใน Scene
        checkpointManager = FindFirstObjectByType<GameManager>();
        if (checkpointManager == null)
        {
            Debug.LogError("ไม่พบ CheckpointManager ใน Scene!");
        }

        // ตั้งค่า ID อัตโนมัติถ้าไม่ได้กำหนด
        if (string.IsNullOrEmpty(checkpointID))
        {
            checkpointID = gameObject.name + "_" + transform.GetSiblingIndex();
        }

        UpdateVisual();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !isActivated)
        {
            ActivateCheckpoint();
        }
    }
    public void ApplyItemDefaults()
    {
        if (ItemManager.Instance != null)
        {
            ItemManager.Instance.SetItemCount(ItemManager.ItemType.Glue, glueCount);
            ItemManager.Instance.SetItemCount(ItemManager.ItemType.Thread, threadCount);
            ItemManager.Instance.UpdateUI();
        }
    }
    public void ActivateCheckpoint()
    {
        if (isActivated) return;

        isActivated = true;

        // บันทึก Checkpoint
        if (checkpointManager != null)
        {
            checkpointManager.SetActiveCheckpoint(this);
            Debug.Log($"Checkpoint '{checkpointID}' ถูกเปิดใช้งาน!");
        }

        // เล่น Animation
        if (checkpointAnimator != null)
        {
            checkpointAnimator.SetBool("IsActivated", true);
            checkpointAnimator.SetTrigger("Activate");
        }

        // เล่น Effect
        if (activationEffect != null)
        {
            activationEffect.Play();
        }

        // เล่นเสียง
        if (audioSource != null && activationSound != null)
        {
            audioSource.PlayOneShot(activationSound);
        }

        UpdateVisual();

       
    }

    public void DeactivateCheckpoint()
    {
        isActivated = false;

        if (checkpointAnimator != null)
        {
            checkpointAnimator.SetBool("IsActivated", false);
        }

        UpdateVisual();
    }

    private void UpdateVisual()
    {
        if (checkpointSprite != null)
        {
            checkpointSprite.color = isActivated ? activeColor : inactiveColor;
        }
    }

    public Vector3 GetSpawnPosition()
    {
        return transform.position;
    }

    public bool IsActivated()
    {
        return isActivated;
    }

    public string GetCheckpointID()
    {
        return checkpointID;
    }

    // แสดง Gizmo ใน Scene View
    private void OnDrawGizmos()
    {
        if (showGizmo)
        {
            Color gizmoColor = isActivated ? activeColor : inactiveColor;
            Gizmos.color = gizmoColor;

            // วาดไอคอน Checkpoint
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.3f);
            Gizmos.DrawSphere(transform.position, 0.5f);

            // วาดลูกศรชี้ขึ้น
            Vector3 arrowTop = transform.position + Vector3.up * 0.8f;
            Vector3 arrowLeft = transform.position + new Vector3(-0.2f, 0.4f, 0);
            Vector3 arrowRight = transform.position + new Vector3(0.2f, 0.4f, 0);

            Gizmos.color = gizmoColor;
            Gizmos.DrawLine(transform.position, arrowTop);
            Gizmos.DrawLine(arrowTop, arrowLeft);
            Gizmos.DrawLine(arrowTop, arrowRight);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // แสดงข้อมูล Checkpoint เมื่อเลือก
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, Vector3.one);
    }
}
