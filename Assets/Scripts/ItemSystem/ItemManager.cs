using System.Collections.Generic;
using UnityEngine;
using TMPro;


// ========================================
// Item Manager - �Ѵ�������������
// ========================================

public class ItemManager : MonoBehaviour
{
    [Header("Item Counts")]
    [SerializeField] private int glueCount = 3;
    [SerializeField] private int threadCount = 5; // ���������͡�������ѹ

    [Header("UI References")]
    public TextMeshProUGUI glueCountText;
    public TextMeshProUGUI threadCountText;
    public GameObject itemDisplayPanel;

    [Header("Debug")]
    public bool showDebugInfo = true;

    // Events
    public static System.Action<ItemType, int> OnItemCountChanged;
    public static System.Action<ItemType> OnItemUsed;
    public static System.Action<ItemType> OnItemCollected;

    // Singleton
    public static ItemManager Instance { get; private set; }

    private Dictionary<ItemType, int> itemCounts = new Dictionary<ItemType, int>();

    public enum ItemType
    {
        Glue,
        Thread // �������ѹ�����ҧ��������˹��͡
    }

    void Awake()
    {
        // Singleton Pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeItems();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        UpdateUI();
    }

    void InitializeItems()
    {
        itemCounts[ItemType.Glue] = glueCount;
        itemCounts[ItemType.Thread] = threadCount;

        if (showDebugInfo)
            Debug.Log($"ItemManager initialized - Glue: {glueCount}, Thread: {threadCount}");
    }

    #region Item Operations

    /// <summary>
    /// ��Ǩ�ͺ��������������������
    /// </summary>
    public bool HasItem(ItemType itemType, int amount = 1)
    {
        return itemCounts.ContainsKey(itemType) && itemCounts[itemType] >= amount;
    }

    /// <summary>
    /// ������ (Ŵ�ӹǹ)
    /// </summary>
    public bool UseItem(ItemType itemType, int amount = 1)
    {
        if (!HasItem(itemType, amount))
        {
            if (showDebugInfo)
                Debug.LogWarning($"Cannot use {itemType} - insufficient quantity. Has: {GetItemCount(itemType)}, Need: {amount}");
            return false;
        }

        itemCounts[itemType] -= amount;

        // ���˵ء�ó�
        OnItemUsed?.Invoke(itemType);
        OnItemCountChanged?.Invoke(itemType, itemCounts[itemType]);

        UpdateUI();

        if (showDebugInfo)
            Debug.Log($"Used {amount} {itemType}. Remaining: {itemCounts[itemType]}");

        return true;
    }

    /// <summary>
    /// ������ (�����ӹǹ)
    /// </summary>
    public void CollectItem(ItemType itemType, int amount = 1)
    {
        if (!itemCounts.ContainsKey(itemType))
            itemCounts[itemType] = 0;

        itemCounts[itemType] += amount;

        // ���˵ء�ó�
        OnItemCollected?.Invoke(itemType);
        OnItemCountChanged?.Invoke(itemType, itemCounts[itemType]);

        UpdateUI();

        if (showDebugInfo)
            Debug.Log($"Collected {amount} {itemType}. Total: {itemCounts[itemType]}");
    }

    /// <summary>
    /// �٨ӹǹ���������
    /// </summary>
    public int GetItemCount(ItemType itemType)
    {
        return itemCounts.ContainsKey(itemType) ? itemCounts[itemType] : 0;
    }

    /// <summary>
    /// �絨ӹǹ���� (����Ѻ Debug)
    /// </summary>
    public void SetItemCount(ItemType itemType, int count)
    {
        itemCounts[itemType] = Mathf.Max(0, count);
        OnItemCountChanged?.Invoke(itemType, itemCounts[itemType]);
        UpdateUI();

        if (showDebugInfo)
            Debug.Log($"Set {itemType} count to {count}");
    }

    #endregion

    #region UI Management

    void UpdateUI()
    {
        if (glueCountText != null)
            glueCountText.text = GetItemCount(ItemType.Glue).ToString();

        if (threadCountText != null)
            threadCountText.text = GetItemCount(ItemType.Thread).ToString();

        // �ʴ�/��͹ Panel ������������
        if (itemDisplayPanel != null)
        {
            bool hasAnyItems = GetItemCount(ItemType.Glue) > 0 || GetItemCount(ItemType.Thread) > 0;
            itemDisplayPanel.SetActive(hasAnyItems);
        }
    }

    #endregion

    #region Debug Methods

    [ContextMenu("Add 1 Glue")]
    public void Debug_AddGlue()
    {
        CollectItem(ItemType.Glue, 1);
    }

    [ContextMenu("Add 1 Thread")]
    public void Debug_AddThread()
    {
        CollectItem(ItemType.Thread, 1);
    }

    [ContextMenu("Use 1 Glue")]
    public void Debug_UseGlue()
    {
        UseItem(ItemType.Glue, 1);
    }

    [ContextMenu("Use 1 Thread")]
    public void Debug_UseThread()
    {
        UseItem(ItemType.Thread, 1);
    }

    #endregion
}
