using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CollectibleItem : MonoBehaviour
{
    public enum CollectibleType { GenericItem, Key, BossPhaseTrigger }

    [Header("Item Settings")]
    public CollectibleType type = CollectibleType.GenericItem;

    [Tooltip("����Ѻ GenericItem: ���͡����������")]
    public ItemManager.ItemType itemType = ItemManager.ItemType.Glue;
    [Tooltip("����Ѻ GenericItem: �ӹǹ�������Ѻ")]
    public int itemAmount = 1;
    [Tooltip("����Ѻ Key: ID ੾�Тͧ�حᨴ͡��� (��ͧ�ç�Ѻ��е�)")]
    public string keyID;

    [Tooltip("ID ੾������Ѻ������鹹�� (��������ҧ�����ҧ����ѵ��ѵ�)")]
    public string itemID; // <-- ��Ҩ����ǹ������ѡ㹡�è���

    [Header("Visual Settings")]
    public GameObject itemVisual;
    public ParticleSystem collectEffect;
    public AudioSource collectSound;

    // ==========================================
    // [��Ѻ����] ��õ�駤���Ϳ࿡���������ʧ��ҹ��ѧ
    // ==========================================
    [Header("Hover & Background Glow Effects")]
    public bool enableHoverEffect = true;      // �Դ/�Դ ������
    public float hoverSpeed = 2f;              // ��������㹡����¢��ŧ
    public float hoverHeight = 0.2f;           // ���Ф����٧������

    public bool enableGlowEffect = true;       // �Դ/�Դ �ʧ��ҹ��ѧ
    public SpriteRenderer glowSpriteRenderer;  // **�Ӥѭ** �ҡ Sprite �ͧ�ʧ��ҹ��ѧ������ͧ���!
    public float minGlowOpacity = 0.2f;        // �������ҧ��鹵�� (Alpha 0-1)
    public float maxGlowOpacity = 1f;          // �������ҧ�٧�ش (Alpha 0-1)
    public float glowSpeed = 3f;               // �������ǡо�Ժ

    private Vector3 startPosition;
    // ==========================================

    private bool isCollected = false;

    void Awake()
    {
        // ���ҧ ID �ѵ��ѵԶ��������˹�
        if (string.IsNullOrEmpty(itemID))
        {
            itemID = $"{gameObject.scene.name}_{gameObject.name}_{transform.position.sqrMagnitude}";
        }

        // --- ��ǹ�Ӥѭ����ش ---
        // ��Ǩ�ͺ�Ѻ GameManager (����ѧ��������ѧ���) ��� itemID ����¶١������������ѧ
        if (GameManager.Instance != null && GameManager.Instance.HasItemBeenCollected(itemID))
        {
            // ����������� -> ����µ���ͧ������º� ��͹�������蹨����
            Destroy(gameObject);
            return; // �͡�ҡ�ѧ��ѹ�ѹ�� ����ͧ�����õ��
        }

        // ����������� Collider �� Trigger
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Start()
    {
        // �ӵ��˹����������� ���������¢��ŧ�ҡ�ش���
        startPosition = transform.position;

        // �ѡ�óռ���������ҡ Glow Sprite ����� ����ͧ���ѵ��ѵ�
        if (enableGlowEffect && glowSpriteRenderer == null)
        {
            // �� SpriteRenderer ��١�
            foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>())
            {
                // ���������ʧ��ҹ��ѧ�����������ѡ (Visual)
                if (itemVisual == null || sr.gameObject != itemVisual)
                {
                    glowSpriteRenderer = sr;
                    break;
                }
            }
        }
    }

    void Update()
    {
        if (isCollected) return;

        // 1. �Ϳ࿡����¢��ŧ (Hover)
        if (enableHoverEffect)
        {
            float newY = startPosition.y + (Mathf.Sin(Time.time * hoverSpeed) * hoverHeight);
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }

        // 2. �Ϳ࿡���ʧ��ҹ��ѧ (Background Glow Pulse)
        if (enableGlowEffect && glowSpriteRenderer != null)
        {
            // �� Mathf.Sin �ӹǳ��Ҥ��� (-1 �֧ 1) �ŧ�� (0 �֧ 1)
            float glowAlpha = (Mathf.Sin(Time.time * glowSpeed) + 1f) / 2f;

            // �� Mathf.Lerp ���͡о�Ժ Alpha �����ҧ��� Min �Ѻ Max
            float finalAlpha = Mathf.Lerp(minGlowOpacity, maxGlowOpacity, glowAlpha);

            // ��������������¹���� Alpha (A)
            Color currentColor = glowSpriteRenderer.color;
            currentColor.a = finalAlpha;
            glowSpriteRenderer.color = currentColor;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isCollected && other.CompareTag("Player"))
        {
            Collect();
        }
    }

    void Collect()
    {
        if (isCollected) return; // �Ѵ ItemManager.Instance == null �͡��������� Boss Item ���������� Manager
        isCollected = true;

        // �ѹ�֡��������� (¡��� Key)
        if (type != CollectibleType.Key)
        {
            GameManager.Instance?.MarkItemAsCollected(this.itemID);
        }

        switch (type)
        {
            case CollectibleType.GenericItem:
                if (ItemManager.Instance != null)
                    ItemManager.Instance.CollectItem(itemType, itemAmount);
                break;

            case CollectibleType.Key:
                if (ItemManager.Instance != null)
                    ItemManager.Instance.AddKey(keyID);
                break;

            case CollectibleType.BossPhaseTrigger:
                Debug.Log("�红ͧ�ú! 仴�ҹ����!");
                BossItemUI ui = FindFirstObjectByType<BossItemUI>();
                if (ui != null)
                {
                    ui.AddBossItem();
                }
                LevelManager levelMgr = FindFirstObjectByType<LevelManager>();
                if (levelMgr != null)
                {
                    // ����������¹��ҹ (����� LevelManager �����觺���ͧ)
                    // ����ѧ��診�� ���仴�ҹ�Ѵ�
                    levelMgr.NextLevel();

                }
                break;
        }

        if (collectEffect != null)
            Instantiate(collectEffect, transform.position, Quaternion.identity);

        if (collectSound != null)
            AudioSource.PlayClipAtPoint(collectSound.clip, transform.position);

        Destroy(gameObject);
    }
}