using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    public enum CheckpointMode
    {
        OverrideInventory, // ��駤���������������
        AddToInventory     // �ǡ�����ҡ�ͧ���������
    }

    [Header("Checkpoint Settings")]
    public string checkpointID;
    public CheckpointMode mode = CheckpointMode.OverrideInventory;

    [Header("Item Settings")]
    [Tooltip("����Ѻ���� Override: �� '��駤��' �ӹǹ�����繤�ҹ��")]
    public int overrideGlueCount = 3;
    [Tooltip("����Ѻ���� Override: �� '��駤��' �ӹǹ�����繤�ҹ��")]
    public int overrideThreadCount = 2;
    [Tooltip("����Ѻ���� Add: �� '�ǡ����' ��������ӹǹ��� (����������)")]
    public int bonusGlueAmount = 0;
    [Tooltip("����Ѻ���� Add: �� '�ǡ����' ��������ӹǹ��� (����������)")]
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

    // �������ͧ��� Header("Item Defaults") ����ӫ�͹�ա����
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
        // ��Ҩ����¡ ActivateCheckpoint() �����������
        if (other.CompareTag("Player"))
        {
            ActivateCheckpoint();
        }
    }

    public void ActivateCheckpoint()
    {
        if (GameManager.Instance == null) return;

        // 1. ตรวจสอบว่าเคยทำงานไปแล้วหรือยัง
        if (isActivated || GameManager.Instance == null) return;

        // ---------------------------------------------------------
        // ★ แก้ตรงนี้: ย้ายการแจกไอเทมมาทำเป็นสิ่งแรกสุด! ★
        // เพื่อให้ Inventory ของผู้เล่นมีของครบ ก่อนที่จะทำการ Save
        // ---------------------------------------------------------
        switch (mode)
        {
            case CheckpointMode.OverrideInventory:
                ApplyOverrideItems();
                break;
            case CheckpointMode.AddToInventory:
                ApplyBonusItems();
                break;
        }

        // 2. พอแจกของเสร็จ ค่อยบอก GameManager ให้ตั้งค่า Checkpoint ล่าสุด
        // GameManager จะทำการ Snapshot (ถ่ายรูป) ข้อมูลที่มีของครบแล้วเก็บไว้
        GameManager.Instance.SetActiveCheckpoint(this);
        
        // (บรรทัดนี้ SaveItemSnapshot อาจจะไม่จำเป็นแล้วถ้า SetActiveCheckpoint ทำงานถูก
        // แต่ใส่ไว้กันเหนียวก็ได้ครับ แต่ต้องอยู่หลังแจกของเสมอ)
        GameManager.Instance.SaveItemSnapshot();
        // 5. ����Ϳ࿡�������
        if (checkpointAnimator != null) checkpointAnimator.SetTrigger("Activate");
        if (activationEffect != null) activationEffect.Play();
        if (audioSource != null && activationSound != null) audioSource.PlayOneShot(activationSound);
        isActivated = true;
        UpdateVisual();
    }

    private void ApplyOverrideItems()
    {
        if (ItemManager.Instance == null) return;
        //Debug.Log($"Overriding inventory: Glue -> {overrideGlueCount}, Thread -> {overrideThreadCount}");
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

    // ... (��ǹ OnDrawGizmos ����͹���) ...

// �ʴ� Gizmo � Scene View
private void OnDrawGizmos()
    {
        if (showGizmo)
        {
            Color gizmoColor = isActivated ? activeColor : inactiveColor;
            Gizmos.color = gizmoColor;

            // �Ҵ�ͤ͹ Checkpoint
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.3f);
            Gizmos.DrawSphere(transform.position, 0.5f);

            // �Ҵ�١�ê����
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
        // �ʴ������� Checkpoint ��������͡
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, Vector3.one);
    }
}
