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
    private bool hasPlayedEffects = false;
    private void Awake()
    {
        if (string.IsNullOrEmpty(checkpointID))
        {
            checkpointID = $"{gameObject.scene.name}_{gameObject.name}_{transform.GetSiblingIndex()}";
        }
        if (PlayerPrefs.GetInt("Visited_" + checkpointID, 0) == 1)
        {
            hasPlayedEffects = true;
        }
       
    }
    private void Start()
    {
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
        if (isActivated) return;
        switch (mode)
        {
            case CheckpointMode.OverrideInventory:
                ApplyOverrideItems();
                break;
            case CheckpointMode.AddToInventory:
                ApplyBonusItems();
                break;
        }
        GameManager.Instance.SetActiveCheckpoint(this);
        GameManager.Instance.SaveItemSnapshot();
        if (checkpointAnimator != null) checkpointAnimator.SetTrigger("Activate");
        if (!hasPlayedEffects)
        {
            if (activationEffect != null) activationEffect.Play();
            if (audioSource != null && activationSound != null) audioSource.PlayOneShot(activationSound);

            hasPlayedEffects = true; // จำไว้ว่าเล่นแล้ว
            PlayerPrefs.SetInt("Visited_" + checkpointID, 1); // บันทึกลงเครื่องว่าจุดนี้เคยมาแล้ว
            PlayerPrefs.Save();
        }
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
