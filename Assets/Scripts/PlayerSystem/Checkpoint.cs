using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    public enum CheckpointMode
    {
        OverrideInventory, // ตั้งค่าไอเทมใหม่ทั้งหมด
        AddToInventory     // บวกเพิ่มจากของที่มีอยู่
    }

    [Header("Checkpoint Settings")]
    public string checkpointID;
    public CheckpointMode mode = CheckpointMode.OverrideInventory;

    [Header("Item Settings")]
    [Tooltip("สำหรับโหมด Override: จะ 'ตั้งค่า' จำนวนไอเทมเป็นค่านี้")]
    public int overrideGlueCount = 3;
    [Tooltip("สำหรับโหมด Override: จะ 'ตั้งค่า' จำนวนไอเทมเป็นค่านี้")]
    public int overrideThreadCount = 2;
    [Tooltip("สำหรับโหมด Add: จะ 'บวกเพิ่ม' ไอเทมตามจำนวนนี้ (ให้ครั้งเดียว)")]
    public int bonusGlueAmount = 0;
    [Tooltip("สำหรับโหมด Add: จะ 'บวกเพิ่ม' ไอเทมตามจำนวนนี้ (ให้ครั้งเดียว)")]
    public int bonusThreadAmount = 0;

    [Header("Visual Settings")]
    public bool showGizmo = true;
    public Color inactiveColor = Color.gray;
    public Color activeColor = Color.green;

    [Header("Optional Components")]
    public Animator checkpointAnimator;
    public SpriteRenderer checkpointSprite;
    public ParticleSystem activationEffect;
    public AudioSource audioSource;
    public AudioClip activationSound;

    // เราไม่ต้องการ Header("Item Defaults") ที่ซ้ำซ้อนอีกต่อไป
    // public int glueCount = 3;
    // public int threadCount = 2;

    private bool isActivated = false;

    private void Start()
    {
        if (string.IsNullOrEmpty(checkpointID))
        {
            checkpointID = $"{gameObject.scene.name}_{gameObject.name}_{transform.GetSiblingIndex()}";
        }
        UpdateVisual();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // เราจะเรียก ActivateCheckpoint() ที่นี่ที่เดียว
        if (other.CompareTag("Player"))
        {
            ActivateCheckpoint();
        }
    }

    public void ActivateCheckpoint()
    {
        if (GameManager.Instance == null) return;

        // 1. "ด่านตรวจ" อยู่ตรงนี้: ถ้าเคยเปิดใช้งานแล้ว หรือหา GameManager ไม่เจอ ก็ไม่ต้องทำอะไรต่อ
        if (isActivated || GameManager.Instance == null) return;

        // 2. ตั้งค่าสถานะทันที เพื่อป้องกันการเรียกซ้ำ

        //Debug.Log($"Checkpoint '{checkpointID}' activated!");

        // 3. บอก GameManager ให้ตั้งค่า Checkpoint นี้เป็นตัวล่าสุด
        // การทำแบบนี้จะทำให้ GameManager "ถ่ายรูป" จำนวนไอเทมปัจจุบันไว้โดยอัตโนมัติ
        GameManager.Instance.SetActiveCheckpoint(this);

        // 4. แจกไอเทมตามโหมดที่เลือก
        switch (mode)
        {
            case CheckpointMode.OverrideInventory:
                ApplyOverrideItems();
                break;
            case CheckpointMode.AddToInventory:
                ApplyBonusItems();
                break;
        }
        GameManager.Instance.SaveItemSnapshot();
        // 5. เล่นเอฟเฟกต์ทั้งหมด
        if (checkpointAnimator != null) checkpointAnimator.SetTrigger("Activate");
        if (activationEffect != null) activationEffect.Play();
        if (audioSource != null && activationSound != null) audioSource.PlayOneShot(activationSound);
        isActivated = true;
        UpdateVisual();
    }

    private void ApplyOverrideItems()
    {
        if (ItemManager.Instance == null) return;
        Debug.Log($"Overriding inventory: Glue -> {overrideGlueCount}, Thread -> {overrideThreadCount}");
        ItemManager.Instance.SetItemCount(ItemManager.ItemType.Glue, overrideGlueCount);
        ItemManager.Instance.SetItemCount(ItemManager.ItemType.Thread, overrideThreadCount);
    }

    private void ApplyBonusItems()
    {
        if (ItemManager.Instance == null || GameManager.Instance == null) return;

        if (GameManager.Instance.HasBonusCheckpointBeenTriggered(checkpointID))
        {
            Debug.Log($"Checkpoint '{checkpointID}' bonus already claimed.");
            return;
        }

        ItemManager.Instance.CollectItem(ItemManager.ItemType.Glue, bonusGlueAmount);
        ItemManager.Instance.CollectItem(ItemManager.ItemType.Thread, bonusThreadAmount);
        GameManager.Instance.MarkBonusCheckpointTriggered(checkpointID);
        Debug.Log($"Checkpoint '{checkpointID}' granted bonus items.");
    }

    public void DeactivateCheckpoint()
    {
        isActivated = false;
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        if (checkpointSprite != null)
        {
            checkpointSprite.color = isActivated ? activeColor : inactiveColor;
        }
    }

    public Vector3 GetSpawnPosition() => transform.position;
    public bool IsActivated() => isActivated;
    public string GetCheckpointID() => checkpointID;

    // ... (ส่วน OnDrawGizmos เหมือนเดิม) ...

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
