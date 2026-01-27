using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlatformController : MonoBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("ติ๊กถูกเพื่อสั่ง Play Animation (แต่การเคลื่อนที่ยังใช้ Script คุมเหมือนเดิม)")]
    public bool isAnimMode = false;
    public Animator platformAnimator;

    [Header("Triggers")]
    public string activateTriggerName = "Open";
    public string deactivateTriggerName = "Close";

    //[Header("State Names (Start Only)")]
    //[Tooltip("ชื่อ State ใน Animator ต้องตรงเป๊ะ (ถ้าไม่มีให้เว้นว่างไว้)")]
    //public string idleOpenStateName = "IdleOpen";
    //[Tooltip("ชื่อ State ใน Animator ต้องตรงเป๊ะ (ถ้าไม่มีให้เว้นว่างไว้)")]
    //public string idleCloseStateName = "IdleClose";

    private bool isActive = false;

    // --- Optimization: Cache Animator Hashes ---
    private int activateTriggerID;
    private int deactivateTriggerID;
    private int idleOpenStateID;
    private int idleCloseStateID;

    [Header("Physics Settings")]
    public Transform platform;
    private Rigidbody2D platformRb2D;
    private RigidbodyType2D initialBodyType;
    private RigidbodyConstraints2D defaultConstraints;
    private Collider2D platformCollider; // Cache Collider

    [Header("Movement Settings")]
    public Vector3 pivotPosition;
    public Vector3 upPosition;
    public Vector3 downPosition;
    public float speed = 2f;
    public float openRotationAngle = 0f;
    public float rotationSpeed = 50f;
    public float startRotation = 0f;
    public bool isRotationMode = false;

    [Header("Lock Settings")]
    public bool isLocked = false;
    public string requiredKeyID;

    [Header("Lever + Player Trigger Mode")]
    public bool requireLeverAndPlayerNearby = false;
    public float playerProximityRadius = 3f;
    public float activationDelay = 0.5f;

    [Header("Parenting Mode")]
    public bool useParentingMode = true;
    public string glueControlTag = "Glue";
    public string[] parentableTagsWhenGlued;
    public float parentingDelay = 0.1f;

    private bool pendingActivation = false;
    private Coroutine pendingCoroutine = null;

    private HashSet<GameObject> glueObjects = new HashSet<GameObject>();
    private Dictionary<GameObject, ParentedObjectData> parentedObjects = new Dictionary<GameObject, ParentedObjectData>();
    private HashSet<GameObject> objectsOnPlatform = new HashSet<GameObject>();

    // --- Optimization: Cache Player ---
    private Transform cachedPlayerTransform;

    [System.Serializable]
    public class ParentedObjectData
    {
        public Transform originalParent;
        public List<ChildRigidbodyState> childRigidbodies = new List<ChildRigidbodyState>();
        public bool wasKinematicFromGlue = false;

        [System.Serializable]
        public class ChildRigidbodyState
        {
            public Rigidbody2D rb;
            public RigidbodyType2D originalBodyType;
            public RigidbodyConstraints2D originalConstraints;

            public ChildRigidbodyState(Rigidbody2D rb, RigidbodyType2D bodyType, RigidbodyConstraints2D constraints)
            {
                this.rb = rb;
                this.originalBodyType = bodyType;
                this.originalConstraints = constraints;
            }
        }

        public ParentedObjectData(Transform parent)
        {
            originalParent = parent;
            childRigidbodies = new List<ChildRigidbodyState>();
            wasKinematicFromGlue = false;
        }
    }

    private float currentRotation = 0f;

    void Start()
    {
        platformRb2D = platform.GetComponent<Rigidbody2D>();
        if (platformRb2D == null)
        {
            Debug.LogError("Platform ต้องมี Rigidbody2D component!");
            return;
        }

        // Cache Collider
        platformCollider = platform.GetComponent<Collider2D>();

        initialBodyType = platformRb2D.bodyType;
        defaultConstraints = platformRb2D.constraints;
        FreezePlatform();

        // --- Optimization: Find Player Once ---
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) cachedPlayerTransform = playerObj.transform;

        if (isRotationMode)
        {
            startRotation = platform.eulerAngles.z;
            currentRotation = startRotation;
        }
        else
        {
            if (Mathf.Approximately(downPosition.x, 0f) &&
                Mathf.Approximately(downPosition.y, 0f) &&
                Mathf.Approximately(downPosition.z, 0f))
            {
                downPosition = platform.position;
            }
        }

        if (useParentingMode)
        {
            parentedObjects = new Dictionary<GameObject, ParentedObjectData>();
            objectsOnPlatform = new HashSet<GameObject>();
            glueObjects = new HashSet<GameObject>();
        }

        // --- Optimization: Convert Strings to Hashes ---
        if (isAnimMode && platformAnimator != null)
        {
            activateTriggerID = Animator.StringToHash(activateTriggerName);
            deactivateTriggerID = Animator.StringToHash(deactivateTriggerName);

            //// ป้องกัน Error โดยเช็คว่าชื่อไม่ว่างเปล่าก่อนแปลง Hash
            //if (!string.IsNullOrEmpty(idleOpenStateName))
            //    idleOpenStateID = Animator.StringToHash(idleOpenStateName);

            //if (!string.IsNullOrEmpty(idleCloseStateName))
            //    idleCloseStateID = Animator.StringToHash(idleCloseStateName);

            //// --- Fix: Check before Play ---
            //if (isActive)
            //{
            //    if (!string.IsNullOrEmpty(idleOpenStateName))
            //        platformAnimator.Play(idleOpenStateID);
            //}
            //else
            //{
            //    if (!string.IsNullOrEmpty(idleCloseStateName))
            //        platformAnimator.Play(idleCloseStateID);
            //}
        }
    }

    public void OnLeverToggled(bool state)
    {
        Toggle(state);
    }

    private void OnEnable()
    {
        ItemManager.OnKeyCollected += OnKeyCollectedHandler;
    }

    private void OnDisable()
    {
        ItemManager.OnKeyCollected -= OnKeyCollectedHandler;
    }

    void FreezePlatform()
    {
        platformRb2D.constraints = RigidbodyConstraints2D.FreezeAll;
    }

    void UnfreezePlatform()
    {
        platformRb2D.constraints = defaultConstraints;
    }

    private void OnKeyCollectedHandler(string collectedKeyID)
    {
        if (isLocked && collectedKeyID == requiredKeyID)
        {
            UnlockAndActivate();
        }
    }

    private void UnlockAndActivate()
    {
        isLocked = false;
        Toggle(true);
    }

    public void Toggle(bool state)
    {
        if (isLocked)
        {
            Debug.Log($"Platform Locked: {requiredKeyID}");
            return;
        }

        if (requireLeverAndPlayerNearby && state == true)
        {
            if (pendingCoroutine != null) StopCoroutine(pendingCoroutine);
            pendingActivation = true;
            pendingCoroutine = StartCoroutine(WaitForPlayerAndActivate());
            return;
        }

        if (requireLeverAndPlayerNearby && state == false)
        {
            if (pendingCoroutine != null) StopCoroutine(pendingCoroutine);
            pendingActivation = false;
        }

        bool prevActive = isActive;
        isActive = state;

        if (isAnimMode && platformAnimator != null && prevActive != isActive)
        {
            if (isActive) platformAnimator.SetTrigger(activateTriggerID);
            else platformAnimator.SetTrigger(deactivateTriggerID);
        }

        UnfreezePlatform();
    }

    private IEnumerator WaitForPlayerAndActivate()
    {
        float checkInterval = 0.1f;
        while (true)
        {
            // --- Optimization: Use Cached Player ---
            // ถ้าหาไม่เจอตอน Start ให้ลองหาใหม่ (เผื่อ Player เกิดทีหลัง)
            if (cachedPlayerTransform == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) cachedPlayerTransform = p.transform;
            }

            if (cachedPlayerTransform != null)
            {
                // ใช้ sqrMagnitude เร็วกว่า Distance
                float distSqr = (cachedPlayerTransform.position - transform.position).sqrMagnitude;
                if (distSqr <= (playerProximityRadius * playerProximityRadius)) break;
            }

            yield return new WaitForSeconds(checkInterval);
        }

        if (activationDelay > 0f) yield return new WaitForSeconds(activationDelay);

        pendingActivation = false;
        bool prevActive = isActive;
        isActive = true;

        if (isAnimMode && platformAnimator != null && prevActive != isActive)
        {
            platformAnimator.SetTrigger(activateTriggerID);
        }

        UnfreezePlatform();
    }

    void FixedUpdate()
    {
        if (platformRb2D == null) return;

        if (isRotationMode)
        {
            RotatePlatform();
        }
        else
        {
            MovePlatform();
        }
    }

    private void MovePlatform()
    {
        Vector2 current = platformRb2D.position;
        Vector2 target = isActive ? new Vector2(upPosition.x, upPosition.y) : new Vector2(downPosition.x, downPosition.y);

    
        Vector2 newPosition = Vector2.MoveTowards(current, target, speed * Time.deltaTime);

   
        if ((target - newPosition).sqrMagnitude < 0.000001f)
        {
         
            newPosition = target;

          
            if (!isActive) FreezePlatform();
        }

     
        platformRb2D.MovePosition(newPosition);
    }

    private void RotatePlatform()
    {
        float targetRotation = isActive ? openRotationAngle : startRotation;

        // --- Optimization: Check angle ---
        if (Mathf.Abs(currentRotation - targetRotation) < 0.01f) return;

        float rotSpeed = isRotationMode ? speed : rotationSpeed;
        currentRotation = Mathf.MoveTowards(currentRotation, targetRotation, rotSpeed * Time.deltaTime);
        platform.RotateAround(pivotPosition, Vector3.forward, currentRotation - platform.eulerAngles.z);
    }

    #region Parenting System
    void OnTriggerEnter2D(Collider2D other) { if (!useParentingMode) return; HandleTriggerEnter(other.gameObject); }
    void OnTriggerExit2D(Collider2D other) { if (!useParentingMode) return; HandleTriggerExit(other.gameObject); }
    void OnCollisionEnter2D(Collision2D col) { if (!useParentingMode) return; HandleCollisionEnter(col); }
    void OnCollisionExit2D(Collision2D col) { if (!useParentingMode) return; HandleTriggerExit(col.gameObject); }

    void HandleTriggerEnter(GameObject obj)
    {
        if (obj.CompareTag(glueControlTag)) { if (HandleGlueEnterPlatform(obj)) ParentAllObjectsOnPlatform(); }
        else if (IsParentableObject(obj))
        {
            if (glueObjects.Count > 0) HandleObjectEnterPlatform(obj);
            else objectsOnPlatform.Add(obj);
        }
    }
    void HandleTriggerExit(GameObject obj)
    {
        if (obj.CompareTag(glueControlTag)) HandleGlueExitPlatform(obj);
        else if (IsParentableObject(obj)) HandleObjectExitPlatform(obj);
    }
    void HandleCollisionEnter(Collision2D col)
    {
        Vector2 contact = col.contacts[0].point;

        // Use cached collider
        float boundsY = (platformCollider != null) ? platformCollider.bounds.size.y : 1f;
        Vector2 top = new Vector2(transform.position.x, transform.position.y + boundsY / 2);

        if (contact.y >= top.y - 0.1f) HandleTriggerEnter(col.gameObject);
    }

    bool IsParentableObject(GameObject obj) { foreach (var t in parentableTagsWhenGlued) if (obj.CompareTag(t)) return true; return false; }
    bool HandleGlueEnterPlatform(GameObject g) { if (glueObjects.Contains(g)) return false; bool e = glueObjects.Count == 0; glueObjects.Add(g); var m = g.GetComponent<GlueDestructionMonitor>() ?? g.AddComponent<GlueDestructionMonitor>(); m.SetPlatform(this); return e; }
    public void HandleGlueExitPlatform(GameObject g) { if (glueObjects.Remove(g) && glueObjects.Count == 0) OnGlueRemoved(); }
    public void OnGlueRemoved() { glueObjects.Clear(); UnparentAllObjects(); }
    void HandleObjectEnterPlatform(GameObject o) { objectsOnPlatform.Add(o); if (!parentedObjects.ContainsKey(o)) StartCoroutine(DelayedParenting(o)); }
    void HandleObjectExitPlatform(GameObject o) { objectsOnPlatform.Remove(o); if (parentedObjects.ContainsKey(o)) UnparentObject(o); }
    IEnumerator DelayedParenting(GameObject o) { yield return new WaitForSeconds(parentingDelay); if (glueObjects.Count > 0 && objectsOnPlatform.Contains(o)) ParentObject(o); }
    void ParentAllObjectsOnPlatform() { foreach (var o in objectsOnPlatform) if (!parentedObjects.ContainsKey(o)) StartCoroutine(DelayedParenting(o)); }
    void UnparentAllObjects() { foreach (var o in new List<GameObject>(parentedObjects.Keys)) UnparentObject(o); }
    void ParentObject(GameObject obj)
    {
        if (parentedObjects.ContainsKey(obj)) return;
        var rbs = obj.GetComponentsInChildren<Rigidbody2D>(true); if (rbs.Length == 0) return;
        var data = new ParentedObjectData(obj.transform.parent);
        foreach (var r in rbs)
        {
            data.childRigidbodies.Add(new ParentedObjectData.ChildRigidbodyState(r, r.bodyType, r.constraints));
            if (r.bodyType == RigidbodyType2D.Kinematic) data.wasKinematicFromGlue = true;
        }
        parentedObjects[obj] = data; obj.transform.SetParent(platform);
        foreach (var c in data.childRigidbodies) { c.rb.bodyType = RigidbodyType2D.Kinematic; c.rb.linearVelocity = Vector2.zero; c.rb.angularVelocity = 0f; }
    }
    void UnparentObject(GameObject obj)
    {
        if (!parentedObjects.ContainsKey(obj)) return;
        var data = parentedObjects[obj]; obj.transform.SetParent(data.originalParent);
        foreach (var c in data.childRigidbodies) { if (c.rb != null) { c.rb.bodyType = c.originalBodyType; c.rb.constraints = c.originalConstraints; } }
        parentedObjects.Remove(obj);
    }
    #endregion
}

public class GlueDestructionMonitor : MonoBehaviour
{
    private PlatformController p; public void SetPlatform(PlatformController c) => p = c;
    void OnDestroy() { if (p != null) p.HandleGlueExitPlatform(gameObject); }
}