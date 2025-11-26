using System.Collections;
using UnityEngine;

public class CraftedObject : MonoBehaviour
{
    [Header("Object Status")]
    public bool isPossessed = false;
    public int currentHitPoints = 3;
    public int maxHitPoints = 3;

    [Header("Possession")]
    private StationaryEnemy possessionController;
    private Enemy originalEnemy;
    private CraftedObject originalDoll;

    [Header("Debug")]
    public bool showDebugInfo = true;

    private TrashCraftingSystem.CraftableItem itemData;
    private TrashCraftingSystem parentCraftingSystem;
    private Coroutine lifetimeCoroutine;
    private Coroutine blinkWarningCoroutine;

    // Components
    private Rigidbody2D rb;
    private Collider2D col;
    private SpriteRenderer spriteRenderer;
    private Animator animator;

    // Visual effects
    private Color originalColor;
    private bool isBlinking = false;

    private Vector3 initialPosition; // ���˹��������
    private Quaternion initialRotation; // �����ع�������

    void Awake()
    {
        // Get components
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
        // �ѹ�֡���˹���С����ع�������
        initialPosition = transform.position;
        initialRotation = transform.rotation;
    }

    public void Initialize(TrashCraftingSystem.CraftableItem data, TrashCraftingSystem craftingSystem)
    {
        itemData = data;
        parentCraftingSystem = craftingSystem;
        currentHitPoints = data.maxHitPoints;

        //// ��駤�Ҥس���ѵԢͧ�ѵ��
        //SetupObjectProperties();

        // ������Ѻ�������ء����ҹ (���������ѵ�ط������ö�١�ԧ��)
        if (!itemData.canBePossessed)
        {
            StartLifetimeCountdown();
        }
        else if (itemData.canBePossessed)
        {
            // ����Ѻ��꡵ҷ������ö�١�ԧ�� - ������Ѻ���һ���
            StartLifetimeCountdown();
        }

        if (showDebugInfo)
        {
            Debug.Log($"CraftedObject initialized: {itemData.itemName}");
        }
    }

    //void SetupObjectProperties()
    //{
    //    if (itemData == null) return;

    //    // ��駤�� Rigidbody2D ����Ѻ��ü�ѡ
    //    if (rb != null)
    //    {
    //        if (itemData.canBePushed)
    //        {
    //            rb.bodyType = RigidbodyType2D.Dynamic;
    //            //rb.freezeRotation = true;

    //            // ���ͧ = ˹ѡ����, ��꡵� = �ҡ���
    //            rb.mass = itemData.itemName.ToLower().Contains("box") ? 2f : 0.5f;
    //            rb.linearDamping = 5f; // ���� drag ���������ش������
    //        }
    //        else
    //        {
    //            rb.bodyType = RigidbodyType2D.Kinematic;
    //        }
    //    }

    //    // ��駤�� Collider ����Ѻ��ê�
    //    if (col != null)
    //    {
    //        col.isTrigger = false; // ��ͧ�� solid ��������ѡ��
    //    }

    //    //// ��駤�� Tag �����кػ�����
    //    //if (itemData.canBePushed && !itemData.canDistractEnemies)
    //    //{
    //    //    gameObject.tag = "PushableBox";
    //    //}
    //    //else if (itemData.canDistractEnemies)
    //    //{
    //    //    gameObject.tag = "DistractableDoll";
    //    //}

    //    // ���� Layer ����Ѻ Enemy detection ����繵�꡵�
    //    if (itemData.canDistractEnemies)
    //    {
    //        gameObject.layer = LayerMask.NameToLayer("DistractableObject");
    //    }
    //}

    void StartLifetimeCountdown()
    {
        if (lifetimeCoroutine != null)
        {
            StopCoroutine(lifetimeCoroutine);
        }

        lifetimeCoroutine = StartCoroutine(LifetimeCountdown());
    }

    IEnumerator LifetimeCountdown()
    {
        float remainingTime = itemData.lifetime;

        // �ͨ����Ҩ���������� 1 �Թҷ� �����������о�Ժ��͹
        float warningTime = Mathf.Min(1f, itemData.lifetime * 0.3f);
        yield return new WaitForSeconds(remainingTime - warningTime);

        // �������о�Ժ��͹
        StartBlinkWarning();

        // �����ҷ�������
        yield return new WaitForSeconds(warningTime);

        // ������� - ��Ѻ�繡ͧ���
        ReturnToTrash();
    }

    void StartBlinkWarning()
    {
        if (blinkWarningCoroutine != null)
        {
            StopCoroutine(blinkWarningCoroutine);
        }

        blinkWarningCoroutine = StartCoroutine(BlinkWarning());
    }

    IEnumerator BlinkWarning()
    {
        isBlinking = true;

        while (isBlinking)
        {
            // ��о�Ժ�ء 0.2 �Թҷ�
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.red;
            }
            yield return new WaitForSeconds(0.1f);

            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    void StopBlinkWarning()
    {
        isBlinking = false;

        if (blinkWarningCoroutine != null)
        {
            StopCoroutine(blinkWarningCoroutine);
            blinkWarningCoroutine = null;
        }

        // �׹�����
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }
    }

    public void ResetToTrashState()
    {
        isPossessed = false;
        currentHitPoints = 3;
        // ������/�Ϳ࿡��/ʶҹ���� � �����ͧ���
        if (spriteRenderer != null)
            spriteRenderer.color = originalColor;
        // ��� � ...
    }



    #region Possession System (����Ѻ��꡵�)

    /// <summary>
    /// ���¡�ҡ���Ҩ����͵�ͧ����ԧ��꡵�
    /// </summary>
    public bool TryPossess(GameObject possessor)
    {
        if (!itemData.canBePossessed || isPossessed) return false;

        isPossessed = true;

        // ��ش�Ѻ�������ء����ҹ
        if (lifetimeCoroutine != null)
        {
            StopCoroutine(lifetimeCoroutine);
            lifetimeCoroutine = null;
        }

        // ��ش��о�Ժ��͹
        StopBlinkWarning();

      

        // ����¹�س���ѵ�����Ͷ١�ԧ
        //EnablePossessedProperties();

        // �Ϳ࿡�����ԧ
        if (itemData.spawnEffect)
        {
            Instantiate(itemData.spawnEffect, transform.position, transform.rotation);
        }

        // ����¹ Animation state �����
        if (animator != null)
        {
            animator.SetBool("IsPossessed", true);
        }

        // ����¹��/�Ϳ࿡�������ʴ���Ҷ١�ԧ
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.red * 0.8f; // ��ᴧ������
        }

        if (showDebugInfo)
        {
            Debug.Log($"{itemData.itemName} has been possessed by {possessor.name}!");
        }

        return true;
    }

    public void SetupPossession(Enemy enemy, CraftedObject doll, StationaryEnemy controller)
    {
        originalEnemy = enemy;
        originalDoll = doll;
        possessionController = controller;
        isPossessed = true;
        currentHitPoints = maxHitPoints;
        //EnablePossessedProperties();
        if (animator != null)
            animator.SetBool("IsPossessed", true);
        if (spriteRenderer != null)
            spriteRenderer.color = Color.red * 0.8f;
    }

    void EnablePossessedProperties()
    {
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.freezeRotation = true;
            rb.mass = 1f;
            rb.linearDamping = 5f;
        }
        gameObject.tag = "PossessedDoll";
    }

    /// <summary>
    /// ���¡����͵�꡵ҷ��١�ԧⴹ����
    /// </summary>
    public void TakeDamage(int damage = 1)
    {
        if (!isPossessed) return;
        currentHitPoints -= damage;
        if (showDebugInfo)
        {
            Debug.Log($" took {damage} damage. HP: {currentHitPoints}/{maxHitPoints}");
        }
        if (currentHitPoints <= 0)
        {
            ReleasePossession();
        }
    }

    IEnumerator HitFlash()
    {
        if (spriteRenderer == null) yield break;

        Color originalPossessedColor = spriteRenderer.color;
        spriteRenderer.color = Color.white;
        yield return new WaitForSeconds(0.1f);
        spriteRenderer.color = originalPossessedColor;
    }

    void ReleasePossession()
    {
        if (!isPossessed) return;
        isPossessed = false;
        if (possessionController != null)
        {
            possessionController.ReleasePossession(transform.position);
        }
        ReturnToTrash();
    }

    void SpawnReleasedSpirit()
    {
        // TODO: ���ҧ���Ҩ�����ش�͡��
        // �Ҩ���� prefab �ͧ���Ҩ��� spawn 㹵��˹觹��

        if (itemData.destroyEffect)
        {
            Instantiate(itemData.destroyEffect, transform.position, transform.rotation);
        }

        Debug.Log("Spirit released from possessed doll!");
    }

    #endregion

    #region Destruction and Return

    public void ReturnToTrash()
    {
        if (itemData.destroyEffect)
        {
            Instantiate(itemData.destroyEffect, transform.position, transform.rotation);
        }
        if (itemData.destroySound && parentCraftingSystem != null)
        {
            AudioSource.PlayClipAtPoint(itemData.destroySound, transform.position);
        }
        if (parentCraftingSystem != null)
        {
            parentCraftingSystem.OnCraftedObjectDestroyed(initialPosition);
        }
        if (showDebugInfo)
        {
            Debug.Log($"{itemData.itemName} returned to trash at {transform.position}");
        }
        Destroy(gameObject);
    }

    /// <summary>
    /// �ѧ�Ѻ����Ѻ�繡ͧ��зѹ�� (����Ѻ debug ����ʶҹ��ó�����)
    /// </summary>
    public void ForceReturnToTrash()
    {
        // ��ش Coroutines ������
        StopAllCoroutines();
        ReturnToTrash();
    }

    #endregion

    #region Collision Detection

    // � OnTriggerEnter2D �ͧ CraftedObject.cs
    void OnTriggerEnter2D(Collider2D other)
    {
        if (itemData != null && itemData.canBePossessed && !isPossessed && other.CompareTag("Enemy"))
        {
            var enemyPossession = other.GetComponent<StationaryEnemy>();
            if (enemyPossession != null)
            {
                enemyPossession.Possess(this);
            }
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // ��Ǩ�ͺ������ըҡ�����������ѵ������
        if (isPossessed && (collision.gameObject.CompareTag("Player") ||
                           collision.gameObject.CompareTag("PlayerWeapon")))
        {
            // ��Ǩ�ͺ����繡��������������������
           // float impactForce = collision.relativeVelocity.magnitude;
          //  if (impactForce > 1f) // threshold ����Ѻ�������
           // {
                TakeDamage(1);
           // }
        }
        else
        {
            return;
        }
    }

    #endregion

    #region Public Methods

    public bool IsPossessed()
    {
        return isPossessed;
    }

    public bool CanBePossessed()
    {
        return itemData.canBePossessed && !isPossessed;
    }

    public bool CanDistractEnemies()
    {
        return itemData.canDistractEnemies;
    }

    public bool CanBePushed()
    {
        return itemData.canBePushed || isPossessed; // �١�ԧ���ǡ��ѡ��
    }

    public float GetRemainingLifetime()
    {
        if (isPossessed) return float.MaxValue; // �١�ԧ��������բմ�ӡѴ����

        // �ӹǳ���ҷ������� (��ͧ�Դ��� timer �������)
        return 0f; // placeholder
    }

    public TrashCraftingSystem.CraftableItem GetItemData()
    {
        return itemData;
    }

    /// <summary>
    /// �������ҡ����ҹ (����Ѻ power-ups ���� mechanics �����)
    /// </summary>
    public void ExtendLifetime(float additionalTime)
    {
        if (isPossessed) return; // �١�ԧ��������ͧ��������

        // ��ش coroutine ���������������������ҷ���������
        if (lifetimeCoroutine != null)
        {
            StopCoroutine(lifetimeCoroutine);
        }

        // ��������� itemData ���Ǥ���
        itemData.lifetime += additionalTime;
        StartLifetimeCountdown();

        if (showDebugInfo)
        {
            Debug.Log($"{itemData.itemName} lifetime extended by {additionalTime} seconds");
        }
    }

    #endregion

    #region Debug

    void OnDrawGizmosSelected()
    {
        if (isPossessed)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 1f);
        }
        else if (itemData != null && itemData.canBePossessed)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.8f);
        }
        else
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
        }

        // �ʴ��ӹǹ HP ��Ҷ١�ԧ
        if (isPossessed && Application.isPlaying)
        {
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 1.5f,
                $"HP: {currentHitPoints}/{itemData.maxHitPoints}");
#endif
        }
    }

    void OnDestroy()
    {
        // �Ӥ������Ҵ coroutines
        StopAllCoroutines();
    }

    #endregion
}

//// ========================================
//// Enemy Possession Component (Example)
//// ========================================

///// <summary>
///// Component ����Ѻ���Ҩ�������ö�ԧ��꡵���
///// </summary>
//public class EnemyPossession : MonoBehaviour
//{
//    [Header("Possession Settings")]
//    public bool canPossessObjects = true;
//    public float possessionRange = 2f;
//    public float possessionCooldown = 5f;

//    private float lastPossessionTime = 0f;
//    private CraftedObject currentPossessedObject = null;

//    public bool CanPossess()
//    {
//        return canPossessObjects &&
//               currentPossessedObject == null &&
//               Time.time - lastPossessionTime >= possessionCooldown;
//    }

//    public void PossessObject(CraftedObject craftedObject)
//    {
//        currentPossessedObject = craftedObject;
//        lastPossessionTime = Time.time;

//        // ����¹�ĵԡ����ͧ���Ҩ (��ҵ�ͧ���)
//        // �� ��ش����͹���, ����¹ AI state, etc.

//        Debug.Log($"Enemy {gameObject.name} possessed {craftedObject.GetItemData().itemName}");
//    }

//    public void OnPossessedObjectDestroyed()
//    {
//        currentPossessedObject = null;

//        // ��Ѻ���վĵԡ�������
//        Debug.Log($"Enemy {gameObject.name} lost possessed object");
//    }


//    void OnDrawGizmosSelected()
//    {
//        if (canPossessObjects)
//        {
//            Gizmos.color = Color.magenta;
//            Gizmos.DrawWireSphere(transform.position, possessionRange);
//        }
//    }
//}