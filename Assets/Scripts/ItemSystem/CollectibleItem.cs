using UnityEngine;

public class CollectibleItem : MonoBehaviour
{
    [Header("Item Settings")]
    public ItemManager.ItemType itemType = ItemManager.ItemType.Glue;
    public int itemAmount = 1;
    public float interactionDistance = 2f;

    [Header("Visual Settings")]
    public GameObject itemVisual;
    public ParticleSystem collectEffect;
    public AudioSource collectSound;

    [Header("UI")]
    public GameObject interactPrompt;

    private Transform player;
    private bool isPlayerNear = false;
    private bool isCollected = false;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (interactPrompt != null)
            interactPrompt.SetActive(false);
    }

    void Update()
    {
        if (isCollected || player == null) return;

        CheckPlayerDistance();

        if (isPlayerNear && Input.GetKeyDown(KeyCode.E))
        {
            CollectItem();
        }
    }

    void CheckPlayerDistance()
    {
        if (player == null) return;

        float distance = Vector2.Distance(transform.position, player.position);
        bool shouldShowPrompt = distance <= interactionDistance;

        if (shouldShowPrompt != isPlayerNear)
        {
            isPlayerNear = shouldShowPrompt;

            if (interactPrompt != null)
                interactPrompt.SetActive(isPlayerNear);
        }
    }

    void CollectItem()
    {
        if (isCollected || ItemManager.Instance == null) return;

        // à¡çºäÍà·Á
        ItemManager.Instance.CollectItem(itemType, itemAmount);

        // àÍ¿à¿¡µì
        if (collectEffect != null)
            collectEffect.Play();

        if (collectSound != null)
            collectSound.Play();

        // «èÍ¹äÍà·Á
        isCollected = true;
        if (itemVisual != null)
            itemVisual.SetActive(false);

        if (interactPrompt != null)
            interactPrompt.SetActive(false);

        // ·ÓÅÒÂËÅÑ§¨Ò¡àÍ¿à¿¡µìàÊÃç¨
        Destroy(gameObject, 0.2f);

        Debug.Log($"Collected {itemAmount} {itemType}");
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }

}
