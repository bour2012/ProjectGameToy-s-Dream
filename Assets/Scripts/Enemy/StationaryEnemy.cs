using UnityEngine;

public class StationaryEnemy : Enemy
{
    public LayerMask fakeDollLayer;
    public float possessRange = 0.5f;

    [SerializeField] private float wallCheckDistance = 0.5f;
    [SerializeField] private float groundCheckDistance = 1f;
    public float chaseCooldown = 2f;

    private float chaseTimer;
    private bool isReturningBlocked = false;

    private Enemy originalEnemy;
    public GameObject possessedDollPrefab;
    private GameObject currentPossessedDoll;
    private CraftedObject originalDoll;

    protected override void Start()
    {
        base.Start(); // สำคัญ! จะได้ค่า initialPosition จาก Enemy
    }

    protected override void Update()
    {
        // ตรวจจับตุ๊กตาปลอม
        Collider2D dollCol = Physics2D.OverlapCircle(transform.position, possessRange, fakeDollLayer);
        if (dollCol != null)
        {
            var doll = dollCol.GetComponent<CraftedObject>();
            if (doll != null && !doll.isPossessed)
            {
                Possess(doll);
                return;
            }
        }

        // หาเป้าหมาย
        GameObject target = DetectAndLockTarget();
        if (target != null)
        {
            //  เช็กว่ามีกำแพงบังหรือไม่
            if (HasLineOfSight(target))
            {
                isChasing = true;
                isReturningBlocked = false;
                Chase(target);
                chaseTimer = 0f;
            }
            else
            {
                // ผู้เล่นอยู่หลังกำแพง → เหมือนตรวจไม่เจอ
                target = null;
            }
        }

        if (target == null)
        {
            if (isChasing)
            {
                chaseTimer += Time.deltaTime;
                if (chaseTimer >= chaseCooldown)
                {
                    isChasing = false;
                    //currentTarget = null; // ปลดล็อกเป้าหมายเมื่อหยุดไล่ล่า
                }
            }
            else
            {
                if (isReturningBlocked)
                {
                    Vector3 direction = (initialPosition - transform.position).normalized;
                    float distance = Vector3.Distance(transform.position, initialPosition);
                    RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, distance, obstacleLayers);
                    if (hit.collider == null)
                    {
                        isReturningBlocked = false;
                    }
                }

                if (!isReturningBlocked && Vector3.Distance(transform.position, initialPosition) > 0.1f)
                {
                    Patrol();
                }
            }
        }
    }

    //private bool HasLineOfSight(GameObject target)
    //{
    //    Vector2 direction = (target.transform.position - transform.position).normalized;
    //    float distance = Vector2.Distance(transform.position, target.transform.position);

    //    // Raycast ตรวจหาสิ่งกีดขวางระหว่าง Enemy และ Player
    //    RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, distance, obstacleLayers);

    //    if (hit.collider != null)
    //    {
    //        // เจอกำแพงหรือสิ่งกีดขวาง → มองไม่เห็น Player
    //        return false;
    //    }

    //    return true; // ไม่มีสิ่งกีดขวาง → มองเห็น
    //}

    protected override void Patrol()
    {
        Vector3 direction = (initialPosition - transform.position).normalized;
        float step = moveSpeed * Time.deltaTime;

        Vector2[] origins = {
    transform.position + Vector3.up * 0.5f,
    transform.position,
    transform.position + Vector3.down * 0.5f
};

        bool blocked = false;
        foreach (var origin in origins)
        {
            RaycastHit2D hit = Physics2D.Raycast(origin, direction, Vector2.Distance(transform.position, initialPosition), obstacleLayers);
            if (hit.collider != null)
            {
                blocked = true;
                break;
            }
        }

        if (!blocked)
        {
            transform.position = Vector3.MoveTowards(transform.position, initialPosition, step);
            isReturningBlocked = false;
        }
        else
        {
            isReturningBlocked = true;
        }


        //// ตรวจจับสิ่งกีดขวางข้างหน้า
        //RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, wallCheckDistance, obstacleLayers);

        //if (hit.collider == null)
        //{
        //    // ไม่มีสิ่งกีดขวาง เดินกลับไปที่เดิม
        //    transform.position = Vector3.MoveTowards(transform.position, initialPosition, step);
        //    isReturningBlocked = false;
        //}
        //else
        //{
        //    // เจอกำแพงหรือสิ่งกีดขวาง ให้หยุดอยู่กับที่
        //    isReturningBlocked = true;
        //    return;
        //}

        //// ถึงตำแหน่งเดิมแล้ว ให้ยืนนิ่ง
        //if (Vector3.Distance(transform.position, initialPosition) <= 0.01f)
        //{
        //    transform.position = initialPosition;
        //    isReturningBlocked = false;
        //}
    }

    //private void MergeWithFakeDoll(GameObject fakeDoll)
    //{
    //    Debug.Log($"{enemyName} merged with {fakeDoll.name}!");
    //    Destroy(fakeDoll);
    //    gameObject.SetActive(false);
    //}

    //private bool IsInLayerMask(GameObject obj, LayerMask layerMask)
    //{
    //    return (layerMask.value & (1 << obj.layer)) > 0;
    //}

    public void Possess(CraftedObject doll)
    {
        if (currentPossessedDoll != null) return;

        // ซ่อน Enemy และตุ๊กตาเดิม
        originalEnemy = GetComponent<Enemy>();
        originalDoll = doll;
        originalEnemy.gameObject.SetActive(false);
        originalDoll.gameObject.SetActive(false); // แค่ซ่อน ยังไม่ ReturnToTrash


        // สร้างตุ๊กตาที่ถูกสิง
        currentPossessedDoll = Instantiate(possessedDollPrefab, doll.transform.position, Quaternion.identity);
        var possessedScript = currentPossessedDoll.GetComponent<CraftedObject>();
        possessedScript.SetupPossession(originalEnemy, originalDoll, this);
    }

    // เรียกจาก PossessedDollObject เมื่อถูกทำลาย
    public void ReleasePossession(Vector3 releasePosition)
    {

        if (originalEnemy != null)
        {
            originalEnemy.transform.position = releasePosition;
            originalEnemy.gameObject.SetActive(true);
            originalEnemy.transform.root.gameObject.SetActive(true);
            //originalEnemy.ResetToInitialState();
        }
        else
        {
            Debug.LogWarning("No originalEnemy assigned.");
        }

        if (originalDoll != null)
        {
            // เรียก ReturnToTrash ที่นี่
            originalDoll.ReturnToTrash();
        }
        else
        {
            Debug.LogWarning("No originalDoll assigned.");
        }

        if (currentPossessedDoll != null)
        {
            Debug.Log($"Destroying possessed doll: {currentPossessedDoll.name}");
            Destroy(currentPossessedDoll);
        }
        else
        {
            Debug.LogWarning("No possessed doll to destroy.");
        }
        currentPossessedDoll = null;
        originalDoll = null;
        originalEnemy = null;
    }
}
