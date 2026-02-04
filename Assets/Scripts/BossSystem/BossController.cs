using UnityEngine;
using System.Collections;
using System.Collections.Generic; // จำเป็นสำหรับการใช้ List

public class BossController : MonoBehaviour
{
    [Header("Identity")]
    public string bossName = "Sky Terror";

    [Header("Phase Settings")]
    public BossPhaseData[] phases;
    public int currentPhaseIndex = 0;

    [Header("Visual Scale")]
    public Vector3 bossScale = new Vector3(1f, 1f, 1f);

    [Header("Vision Settings (Pro Light)")]
    public LayerMask obstacleMask;
    public float viewRadius = 10f;
    [Range(0, 360)] public float viewAngle = 90f;
    [Range(-180, 180)] public float viewDirectionOffset = -90f;

    [Header("Light Quality (แก้กระตุก)")]
    public float meshResolution = 1f;        // ความละเอียด (0.1 - 10) ค่า 1 คือมาตรฐาน
    public int edgeResolveIterations = 4;    // ยิ่งเยอะขอบยิ่งเนียน (4-8 กำลังดี)
    public float edgeDstThreshold = 0.5f;    // ระยะห่างที่ถือว่าเป็นขอบ

    [Header("Light Material")]
    public Material viewMeshMaterial; // ใส่ Material ที่มี Texture ฟุ้งๆ ตรงนี้

    [Header("Combat Interaction")]
    public float damageOnHit = 10f;
    public float knockbackForce = 20f;
    public float knockbackDuration = 0.5f;
    public float swoopKnockbackMultiplier = 1.5f;

    [Header("Final Defeat Settings")]
    public float fallGravityScale = 2f;
    public Rigidbody2D rb;
    public Animator animator;

    // State Variables
    private bool isDefeated = false;
    private Vector3 startPosition;
    private float phaseTime;
    private Transform playerTransform;

    // Action States
    private bool isSwooping = false;
    private bool isReturning = false;
    private float swoopTimer = 0f;
    private float swoopDurationTimer = 0f;
    private float returnTimer = 0f;
    private float maxReturnTime = 5f;

    // Mesh Variables
    private Mesh viewMesh;
    private MeshFilter viewMeshFilter;
    private MeshRenderer viewMeshRenderer;
    private GameObject viewConeObject;

    [System.Serializable]
    public class BossPhaseData
    {
        public string phaseName;
        [Header("Movement (Patrol)")]
        public float moveSpeed = 3f;
        public float patrolRangeX = 5f;
        public float patrolFrequency = 1f;
        public float hoverHeight = 0f;

        [Header("Swoop Attack")]
        public bool enableSwoop = false;
        public float swoopInterval = 5f;
        public float swoopSpeedMultiplier = 1.5f;

        [Header("Phase Trigger")]
        public float healthThreshold = 0.5f;
    }

    [Header("Status")]
    public float maxHealth = 100f;
    public float currentHealth;

    void Start()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (animator == null) animator = GetComponent<Animator>();
        if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;

        transform.localScale = bossScale;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) playerTransform = playerObj.transform;

        startPosition = transform.position;
        currentHealth = maxHealth;

        CreateViewCone();
        EnterPhase(0);
    }

    void Update()
    {
        if (isDefeated)
        {
            if (viewConeObject != null) viewConeObject.SetActive(false);
            return;
        }

        HandleMovement();
        CheckPhaseProgression();
    }

    // ใช้ LateUpdate เพื่อวาดแสงหลังจากบอสขยับเสร็จแล้ว (แก้แสงสั่นตอนเดิน)
    void LateUpdate()
    {
        if (!isDefeated) DrawFieldOfView();
    }

    void HandleMovement()
    {
        // ... (Logic การเคลื่อนที่คงเดิม) ...
        BossPhaseData phase = phases[currentPhaseIndex];
        Vector3 targetPos;
        float currentSpeed = phase.moveSpeed;

        if (phase.enableSwoop && playerTransform != null && !isSwooping && !isReturning)
        {
            swoopTimer += Time.deltaTime;
            if (swoopTimer >= phase.swoopInterval)
            {
                if (CanSeePlayer())
                {
                    isSwooping = true;
                    swoopDurationTimer = 0f;
                    Debug.Log("Boss Spot Player!");
                }
            }
        }

        if (isSwooping && playerTransform != null)
        {
            targetPos = playerTransform.position;
            currentSpeed *= phase.swoopSpeedMultiplier;
            swoopDurationTimer += Time.deltaTime;
            if (swoopDurationTimer > 10f) StopSwoopAndReturn();
        }
        else if (isReturning)
        {
            targetPos = startPosition;
            currentSpeed = phase.moveSpeed * 2f;
            returnTimer += Time.deltaTime;
            float dist = Vector2.Distance(transform.position, startPosition);
            if (dist < 0.2f || returnTimer > maxReturnTime)
            {
                isReturning = false;
                phaseTime = 0f;
                transform.position = startPosition;
            }
        }
        else
        {
            phaseTime += Time.deltaTime;
            float offsetX = Mathf.Sin(phaseTime * phase.patrolFrequency) * phase.patrolRangeX;
            targetPos = startPosition + new Vector3(offsetX, phase.hoverHeight, 0f);
        }

        transform.position = Vector3.MoveTowards(transform.position, targetPos, currentSpeed * Time.deltaTime);

        if (targetPos.x > transform.position.x)
            transform.localScale = new Vector3(Mathf.Abs(bossScale.x), bossScale.y, bossScale.z);
        else
            transform.localScale = new Vector3(-Mathf.Abs(bossScale.x), bossScale.y, bossScale.z);
    }

    // ================================================================
    // [PRO] ระบบสร้างแสงไฟขั้นสูง (Smooth Mesh Generation)
    // ================================================================
    void CreateViewCone()
    {
        viewConeObject = new GameObject("ViewCone");
        viewConeObject.transform.SetParent(transform);
        viewConeObject.transform.localPosition = Vector3.zero;
        viewConeObject.transform.localScale = Vector3.one;

        viewMeshFilter = viewConeObject.AddComponent<MeshFilter>();
        viewMeshRenderer = viewConeObject.AddComponent<MeshRenderer>();

        viewMesh = new Mesh();
        viewMesh.name = "View Mesh";
        viewMeshFilter.mesh = viewMesh;

        if (viewMeshMaterial != null)
            viewMeshRenderer.material = viewMeshMaterial;
    }

    // Struct เก็บข้อมูล Raycast
    public struct ViewCastInfo
    {
        public bool hit;
        public Vector3 point;
        public float dst;
        public float angle;

        public ViewCastInfo(bool _hit, Vector3 _point, float _dst, float _angle)
        {
            hit = _hit;
            point = _point;
            dst = _dst;
            angle = _angle;
        }
    }

    // Struct เก็บข้อมูลขอบ
    public struct EdgeInfo
    {
        public Vector3 pointA;
        public Vector3 pointB;

        public EdgeInfo(Vector3 _pointA, Vector3 _pointB)
        {
            pointA = _pointA;
            pointB = _pointB;
        }
    }

    ViewCastInfo ViewCast(float globalAngle)
    {
        Vector3 dir = DirFromAngle(globalAngle, true);
        RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, viewRadius, obstacleMask);

        if (hit.collider != null)
        {
            return new ViewCastInfo(true, hit.point, hit.distance, globalAngle);
        }
        else
        {
            return new ViewCastInfo(false, transform.position + dir * viewRadius, viewRadius, globalAngle);
        }
    }

    void DrawFieldOfView()
    {
        int stepCount = Mathf.RoundToInt(viewAngle * meshResolution);
        float stepAngleSize = viewAngle / stepCount;

        // คำนวณมุมเริ่มต้น (จัดการ Mirror ซ้ายขวา)
        float facingSign = Mathf.Sign(transform.localScale.x);
        float adjustedOffset = (facingSign > 0) ? viewDirectionOffset : (180f - viewDirectionOffset);
        float startingAngle = adjustedOffset + viewAngle / 2f;

        List<Vector3> viewPoints = new List<Vector3>();
        ViewCastInfo oldViewCast = new ViewCastInfo();

        for (int i = 0; i <= stepCount; i++)
        {
            float angle = startingAngle - stepAngleSize * i;
            ViewCastInfo newViewCast = ViewCast(angle);

            // --- Edge Resolving Logic (หัวใจสำคัญแก้แสงกระตุก) ---
            if (i > 0)
            {
                bool edgeDstThresholdExceeded = Mathf.Abs(oldViewCast.dst - newViewCast.dst) > edgeDstThreshold;
                if (oldViewCast.hit != newViewCast.hit || (oldViewCast.hit && newViewCast.hit && edgeDstThresholdExceeded))
                {
                    EdgeInfo edge = FindEdge(oldViewCast, newViewCast);
                    if (edge.pointA != Vector3.zero) viewPoints.Add(viewConeObject.transform.InverseTransformPoint(edge.pointA));
                    if (edge.pointB != Vector3.zero) viewPoints.Add(viewConeObject.transform.InverseTransformPoint(edge.pointB));
                }
            }

            viewPoints.Add(viewConeObject.transform.InverseTransformPoint(newViewCast.point));
            oldViewCast = newViewCast;
        }

        // สร้าง Mesh และ UVs
        int vertexCount = viewPoints.Count + 1;
        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uv = new Vector2[vertexCount]; // เพิ่ม UVs
        int[] triangles = new int[(vertexCount - 2) * 3];

        vertices[0] = Vector3.zero;
        uv[0] = new Vector2(0.5f, 0.5f); // จุดกึ่งกลาง Texture

        for (int i = 0; i < vertexCount - 1; i++)
        {
            vertices[i + 1] = viewPoints[i];

            // คำนวณ UV แบบง่ายๆ ตามตำแหน่ง (เพื่อให้ Texture ฟุ้งได้)
            // Map ตำแหน่งจาก World Space ลง Texture Space 0-1
            Vector3 pos = viewPoints[i];
            float u = (pos.x / (viewRadius * 2)) + 0.5f;
            float v = (pos.y / (viewRadius * 2)) + 0.5f;
            uv[i + 1] = new Vector2(u, v);

            if (i < vertexCount - 2)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }
        }

        viewMesh.Clear();
        viewMesh.vertices = vertices;
        viewMesh.uv = uv; // Assign UVs
        viewMesh.triangles = triangles;
        viewMesh.RecalculateBounds();
    }

    // ฟังก์ชันหาขอบมุมกำแพงให้คมกริบ
    EdgeInfo FindEdge(ViewCastInfo minViewCast, ViewCastInfo maxViewCast)
    {
        float minAngle = minViewCast.angle;
        float maxAngle = maxViewCast.angle;
        Vector3 minPoint = Vector3.zero;
        Vector3 maxPoint = Vector3.zero;

        for (int i = 0; i < edgeResolveIterations; i++)
        {
            float angle = (minAngle + maxAngle) / 2;
            ViewCastInfo newViewCast = ViewCast(angle);

            bool edgeDstThresholdExceeded = Mathf.Abs(minViewCast.dst - newViewCast.dst) > edgeDstThreshold;
            if (newViewCast.hit == minViewCast.hit && !edgeDstThresholdExceeded)
            {
                minAngle = angle;
                minPoint = newViewCast.point;
            }
            else
            {
                maxAngle = angle;
                maxPoint = newViewCast.point;
            }
        }

        return new EdgeInfo(minPoint, maxPoint);
    }

    public Vector3 DirFromAngle(float angleInDegrees, bool angleIsGlobal)
    {
        if (!angleIsGlobal) angleInDegrees += transform.eulerAngles.z;
        return new Vector3(Mathf.Cos(angleInDegrees * Mathf.Deg2Rad), Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0);
    }

    bool CanSeePlayer()
    {
        if (playerTransform == null) return false;
        float distToPlayer = Vector2.Distance(transform.position, playerTransform.position);
        if (distToPlayer > viewRadius) return false;

        Vector2 dirToPlayer = (playerTransform.position - transform.position).normalized;
        Vector2 facingDir = transform.localScale.x > 0 ? Vector2.right : Vector2.left;
        float baseAngle = (transform.localScale.x > 0) ? viewDirectionOffset : (180f - viewDirectionOffset);
        Vector3 coneDir = DirFromAngle(baseAngle, true);

        if (Vector3.Angle(coneDir, dirToPlayer) > viewAngle / 2f) return false;

        RaycastHit2D hit = Physics2D.Raycast(transform.position, dirToPlayer, distToPlayer, obstacleMask);
        if (hit.collider != null) return false;

        return true;
    }

    // ... (ส่วน Collision และอื่นๆ เหมือนเดิม) ...
    void StopSwoopAndReturn() { isSwooping = false; isReturning = true; returnTimer = 0f; swoopTimer = 0f; }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isDefeated) return;
        if (other.CompareTag("Player"))
        {
            if (isSwooping) StopSwoopAndReturn();
            Rigidbody2D playerRb = other.GetComponent<Rigidbody2D>();
            PlayerMovement playerMove = other.GetComponent<PlayerMovement>();
            if (playerRb != null)
            {
                float dirX = (other.transform.position.x > transform.position.x) ? 1f : -1f;
                Vector2 knockbackDirection = new Vector2(dirX * 2.5f, 1.0f).normalized;
                float finalForce = knockbackForce * (isSwooping ? swoopKnockbackMultiplier : 1f);
                if (playerMove != null) StartCoroutine(KnockbackRoutine(playerRb, playerMove, knockbackDirection * finalForce));
                else { playerRb.linearVelocity = Vector2.zero; playerRb.AddForce(knockbackDirection * finalForce, ForceMode2D.Impulse); }
            }
        }
    }
    IEnumerator KnockbackRoutine(Rigidbody2D rb, PlayerMovement moveScript, Vector2 force)
    {
        if (moveScript != null) moveScript.enabled = false;
        rb.linearVelocity = Vector2.zero; rb.AddForce(force, ForceMode2D.Impulse);
        yield return new WaitForSeconds(knockbackDuration);
        if (moveScript != null) moveScript.enabled = true;
    }
    void CheckPhaseProgression() { if (currentPhaseIndex < phases.Length - 1 && (currentHealth / maxHealth) <= phases[currentPhaseIndex].healthThreshold) EnterPhase(currentPhaseIndex + 1); }
    public void EnterPhase(int index) { currentPhaseIndex = index; phaseTime = 0f; isSwooping = false; isReturning = false; swoopTimer = 0f; if (animator != null) { animator.SetInteger("Phase", currentPhaseIndex); animator.SetTrigger("PhaseTransition"); } }
    public void TakeDamage(float amount) { if (!isDefeated) { currentHealth -= amount; if (animator != null) animator.SetTrigger("TakeDamage"); } }
    public void HitByUltimateGlue() { if (!isDefeated) { isDefeated = true; if (animator != null) animator.SetBool("Fall", true); rb.bodyType = RigidbodyType2D.Dynamic; rb.gravityScale = fallGravityScale; StartCoroutine(EndGameSequence()); } }
    IEnumerator EndGameSequence() { yield return new WaitForSeconds(4f); Debug.Log("Trigger Cutscene Here..."); }
    private void OnDrawGizmos() { Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, viewRadius); }
}