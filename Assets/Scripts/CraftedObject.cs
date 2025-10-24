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

    private Vector3 initialPosition; // ตำแหน่งเริ่มต้น
    private Quaternion initialRotation; // การหมุนเริ่มต้น

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
        // บันทึกตำแหน่งและการหมุนเริ่มต้น
        initialPosition = transform.position;
        initialRotation = transform.rotation;
    }

    public void Initialize(TrashCraftingSystem.CraftableItem data, TrashCraftingSystem craftingSystem)
    {
        itemData = data;
        parentCraftingSystem = craftingSystem;
        currentHitPoints = data.maxHitPoints;

        //// ตั้งค่าคุณสมบัติของวัตถุ
        //SetupObjectProperties();

        // เริ่มจับเวลาอายุการใช้งาน (ถ้าไม่ใช่วัตถุที่สามารถถูกสิงได้)
        if (!itemData.canBePossessed)
        {
            StartLifetimeCountdown();
        }
        else if (itemData.canBePossessed)
        {
            // สำหรับตุ๊กตาที่สามารถถูกสิงได้ - เริ่มจับเวลาปกติ
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

    //    // ตั้งค่า Rigidbody2D สำหรับการผลัก
    //    if (rb != null)
    //    {
    //        if (itemData.canBePushed)
    //        {
    //            rb.bodyType = RigidbodyType2D.Dynamic;
    //            //rb.freezeRotation = true;

    //            // กล่อง = หนักกว่า, ตุ๊กตา = เบากว่า
    //            rb.mass = itemData.itemName.ToLower().Contains("box") ? 2f : 0.5f;
    //            rb.linearDamping = 5f; // เพิ่ม drag เพื่อให้หยุดได้เร็ว
    //        }
    //        else
    //        {
    //            rb.bodyType = RigidbodyType2D.Kinematic;
    //        }
    //    }

    //    // ตั้งค่า Collider สำหรับการชน
    //    if (col != null)
    //    {
    //        col.isTrigger = false; // ต้องเป็น solid เพื่อให้ผลักได้
    //    }

    //    //// ตั้งค่า Tag เพื่อระบุประเภท
    //    //if (itemData.canBePushed && !itemData.canDistractEnemies)
    //    //{
    //    //    gameObject.tag = "PushableBox";
    //    //}
    //    //else if (itemData.canDistractEnemies)
    //    //{
    //    //    gameObject.tag = "DistractableDoll";
    //    //}

    //    // เพิ่ม Layer สำหรับ Enemy detection ถ้าเป็นตุ๊กตา
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

        // รอจนกว่าจะเหลือเวลา 1 วินาที แล้วเริ่มกระพริบเตือน
        float warningTime = Mathf.Min(1f, itemData.lifetime * 0.3f);
        yield return new WaitForSeconds(remainingTime - warningTime);

        // เริ่มกระพริบเตือน
        StartBlinkWarning();

        // รอเวลาที่เหลือ
        yield return new WaitForSeconds(warningTime);

        // หมดเวลา - กลับเป็นกองขยะ
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
            // กระพริบทุก 0.2 วินาที
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

        // คืนสีเดิม
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }
    }

    public void ResetToTrashState()
    {
        isPossessed = false;
        currentHitPoints = 3;
        // รีเซ็ตสี/เอฟเฟกต์/สถานะอื่น ๆ ตามต้องการ
        if (spriteRenderer != null)
            spriteRenderer.color = originalColor;
        // อื่น ๆ ...
    }



    #region Possession System (สำหรับตุ๊กตา)

    /// <summary>
    /// เรียกจากปีศาจเมื่อต้องการสิงตุ๊กตา
    /// </summary>
    public bool TryPossess(GameObject possessor)
    {
        if (!itemData.canBePossessed || isPossessed) return false;

        isPossessed = true;

        // หยุดจับเวลาอายุการใช้งาน
        if (lifetimeCoroutine != null)
        {
            StopCoroutine(lifetimeCoroutine);
            lifetimeCoroutine = null;
        }

        // หยุดกระพริบเตือน
        StopBlinkWarning();

      

        // เปลี่ยนคุณสมบัติเมื่อถูกสิง
        //EnablePossessedProperties();

        // เอฟเฟกต์การสิง
        if (itemData.spawnEffect)
        {
            Instantiate(itemData.spawnEffect, transform.position, transform.rotation);
        }

        // เปลี่ยน Animation state ถ้ามี
        if (animator != null)
        {
            animator.SetBool("IsPossessed", true);
        }

        // เปลี่ยนสี/เอฟเฟกต์เพื่อแสดงว่าถูกสิง
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.red * 0.8f; // สีแดงอมชมพู
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
    /// เรียกเมื่อตุ๊กตาที่ถูกสิงโดนโจมตี
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
        // TODO: สร้างปีศาจที่หลุดออกมา
        // อาจจะเป็น prefab ของปีศาจที่ spawn ในตำแหน่งนี้

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
    /// บังคับให้กลับเป็นกองขยะทันที (สำหรับ debug หรือสถานการณ์พิเศษ)
    /// </summary>
    public void ForceReturnToTrash()
    {
        // หยุด Coroutines ทั้งหมด
        StopAllCoroutines();
        ReturnToTrash();
    }

    #endregion

    #region Collision Detection

    // ใน OnTriggerEnter2D ของ CraftedObject.cs
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
        // ตรวจสอบการโจมตีจากผู้เล่นหรือวัตถุอื่นๆ
        if (isPossessed && (collision.gameObject.CompareTag("Player") ||
                           collision.gameObject.CompareTag("PlayerWeapon")))
        {
            // ตรวจสอบว่าเป็นการโจมตีหรือแค่การสัมผัส
           // float impactForce = collision.relativeVelocity.magnitude;
          //  if (impactForce > 1f) // threshold สำหรับการโจมตี
           // {
                TakeDamage(1);
           // }
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
        return itemData.canBePushed || isPossessed; // ถูกสิงแล้วก็ผลักได้
    }

    public float GetRemainingLifetime()
    {
        if (isPossessed) return float.MaxValue; // ถูกสิงแล้วไม่มีขีดจำกัดเวลา

        // คำนวณเวลาที่เหลือ (ต้องติดตาม timer เพิ่มเติม)
        return 0f; // placeholder
    }

    public TrashCraftingSystem.CraftableItem GetItemData()
    {
        return itemData;
    }

    /// <summary>
    /// เพิ่มเวลาการใช้งาน (สำหรับ power-ups หรือ mechanics พิเศษ)
    /// </summary>
    public void ExtendLifetime(float additionalTime)
    {
        if (isPossessed) return; // ถูกสิงแล้วไม่ต้องขยายเวลา

        // หยุด coroutine เดิมและเริ่มใหม่ด้วยเวลาที่เพิ่มขึ้น
        if (lifetimeCoroutine != null)
        {
            StopCoroutine(lifetimeCoroutine);
        }

        // ขยายเวลาใน itemData ชั่วคราว
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

        // แสดงจำนวน HP ถ้าถูกสิง
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
        // ทำความสะอาด coroutines
        StopAllCoroutines();
    }

    #endregion
}

//// ========================================
//// Enemy Possession Component (Example)
//// ========================================

///// <summary>
///// Component สำหรับปีศาจที่สามารถสิงตุ๊กตาได้
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

//        // เปลี่ยนพฤติกรรมของปีศาจ (ถ้าต้องการ)
//        // เช่น หยุดเคลื่อนไหว, เปลี่ยน AI state, etc.

//        Debug.Log($"Enemy {gameObject.name} possessed {craftedObject.GetItemData().itemName}");
//    }

//    public void OnPossessedObjectDestroyed()
//    {
//        currentPossessedObject = null;

//        // กลับมามีพฤติกรรมปกติ
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