using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlatformController : MonoBehaviour
{
    public Animator platformAnimator;
    public string openAnimationName = "PlatformOpen";
    public string closeAnimationName = "PlatformClose";
    public string idleCloseAnimationName = "PlatformIdleClose";
    public string idleOpenAnimationName = "PlatformIdleOpen";
    private bool isActive = false;

    public Transform platform;
    private Rigidbody2D platformRb2D;
    private RigidbodyType2D initialBodyType;
    private bool animatorIsDrivingMotion = false;
    private RigidbodyConstraints2D defaultConstraints;
    public Vector3 pivotPosition;
    public Vector3 upPosition;
    public Vector3 downPosition;
    public float speed = 2f;
    public float openRotationAngle = 0f;
    public float rotationSpeed = 50f;
    public float startRotation = 0f;
    public bool isRotationMode = false;
    public bool isAnimMode = false;

    [Header("Lock Settings")]
    public bool isLocked = false;
    public string requiredKeyID;

    [Header("Lever + Player Trigger Mode")]
    [Tooltip("When true, the platform will only activate after a lever toggles it ON and the player comes nearby (plus optional delay). When false, platform behaves as before.")]
    public bool requireLeverAndPlayerNearby = false;
    [Tooltip("Player proximity radius to trigger activation when requireLeverAndPlayerNearby is true.")]
    public float playerProximityRadius = 3f;
    [Tooltip("Extra delay (seconds) after player is nearby before the platform actually activates.")]
    public float activationDelay = 0.5f;

    [Header("Parenting Mode")]
    [Tooltip("When enabled, objects on the platform will be parented to avoid physics lag/slip")]
    public bool useParentingMode = true;
    [Tooltip("Tag for the Glue object that controls the parenting system")]
    public string glueControlTag = "Glue";
    [Tooltip("Tags that should be parented when Glue is present on the platform")]
    public string[] parentableTagsWhenGlued;
    [Tooltip("Minimum contact time before parenting occurs (to avoid flickering)")]
    public float parentingDelay = 0.1f;

    // internal state for pending activation when using the lever+nearby mode
    private bool pendingActivation = false;
    private Coroutine pendingCoroutine = null;

    // Parenting system variables
    // Track all Glue objects currently on the platform. Parenting remains active while this set is non-empty.
    private System.Collections.Generic.HashSet<GameObject> glueObjects = new System.Collections.Generic.HashSet<GameObject>();
    private System.Collections.Generic.Dictionary<GameObject, ParentedObjectData> parentedObjects = new System.Collections.Generic.Dictionary<GameObject, ParentedObjectData>();
    private System.Collections.Generic.HashSet<GameObject> objectsOnPlatform = new System.Collections.Generic.HashSet<GameObject>(); // Track all objects on platform

    [System.Serializable]
    public class ParentedObjectData
    {
        public Transform originalParent;
        // Store state for every Rigidbody2D under the parent object (self + children)
        public System.Collections.Generic.List<ChildRigidbodyState> childRigidbodies = new System.Collections.Generic.List<ChildRigidbodyState>();
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
            childRigidbodies = new System.Collections.Generic.List<ChildRigidbodyState>();
            wasKinematicFromGlue = false;
        }
    }

    private float currentRotation = 0f;

    void Start()
    {
        // เพิ่มการดึง Rigidbody2D component
        platformRb2D = platform.GetComponent<Rigidbody2D>();
        if (platformRb2D == null)
        {
            Debug.LogError("Platform ต้องมี Rigidbody2D component!");
            return;
        }
        initialBodyType = platformRb2D.bodyType;
        defaultConstraints = platformRb2D.constraints; // เก็บค่าสำรองไว้
        FreezePlatform();

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

        // Initialize parenting system if enabled
        if (useParentingMode)
        {
            parentedObjects = new System.Collections.Generic.Dictionary<GameObject, ParentedObjectData>();
            objectsOnPlatform = new System.Collections.Generic.HashSet<GameObject>();
            glueObjects = new System.Collections.Generic.HashSet<GameObject>();
        }
    }

    /// <summary>
    /// Public helper for levers or external systems to notify this platform of a lever toggle.
    /// Keeps naming explicit so other scripts can call this instead of Toggle() if desired.
    /// </summary>
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
        // ล็อกแกน X, Y, Z (Platform ห้ามขยับ)
        platformRb2D.constraints = RigidbodyConstraints2D.FreezeAll;
    }

    void UnfreezePlatform()
    {
        // ปลดล็อก X, Y, Rotation Z (Platform ขยับได้)
        platformRb2D.constraints = defaultConstraints;
    }

    bool IsPlatformFrozen()
    {
        // ตรวจสอบว่า Platform ถูกล็อกอยู่หรือไม่
        return (platformRb2D.constraints == RigidbodyConstraints2D.FreezeAll);
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
        Debug.Log($"Platform '{gameObject.name}' was automatically unlocked by key: {requiredKeyID}");
        Toggle(true);
    }

    public void Toggle(bool state)
    {
        if (isLocked)
        {
            Debug.Log($"Platform '{gameObject.name}' is locked! Need key: {requiredKeyID}");
            return;
        }

        // If the special mode is enabled and we are being turned ON, defer activation until player is nearby
        if (requireLeverAndPlayerNearby && state == true)
        {
            // cancel any previous pending coroutine
            if (pendingCoroutine != null)
            {
                StopCoroutine(pendingCoroutine);
                pendingCoroutine = null;
            }

            pendingActivation = true;
            pendingCoroutine = StartCoroutine(WaitForPlayerAndActivate());
            return;
        }

        // If asked to turn OFF while a pending activation exists, cancel it
        if (requireLeverAndPlayerNearby && state == false)
        {
            if (pendingCoroutine != null)
            {
                StopCoroutine(pendingCoroutine);
                pendingCoroutine = null;
            }
            pendingActivation = false;
        }

        // only change state and play animation when the state actually changes
        bool prevActive = isActive;
        isActive = state;

        // If Animator mode is enabled, let Animator drive the visual motion only.
        if (isAnimMode && platformAnimator != null && prevActive != isActive)
        {
            if (isActive)
                platformAnimator.Play(openAnimationName);
            else
                platformAnimator.Play(closeAnimationName);

            // Switch Rigidbody to kinematic so Animator moving transform doesn't fight physics MovePosition.
            platformRb2D.bodyType = RigidbodyType2D.Kinematic;
            animatorIsDrivingMotion = true;

            // Do not unfreeze physics-driven movement while Animator is driving motion.
            return;
        }

        // If we were previously driven by the Animator but now using physics movement again, restore body type.
        if (animatorIsDrivingMotion && !isAnimMode)
        {
            platformRb2D.bodyType = initialBodyType;
            animatorIsDrivingMotion = false;
        }

        // allow movement physics when toggled (freeze handled on arrival)
        UnfreezePlatform();
    }

    private System.Collections.IEnumerator WaitForPlayerAndActivate()
    {
        // Wait until we find a Player within the radius, then wait the activationDelay, then activate
        float checkInterval = 0.1f;
        Transform player = null;
        while (true)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
                float dist = Vector2.Distance(player.position, transform.position);
                if (dist <= playerProximityRadius)
                {
                    break; // player is nearby
                }
            }

            yield return new WaitForSeconds(checkInterval);
        }

        // optional small delay before actual activation
        if (activationDelay > 0f)
            yield return new WaitForSeconds(activationDelay);

        pendingActivation = false;
        pendingCoroutine = null;

        // finally activate the platform (this will respect locking check earlier)
        // we call Toggle(false/true) carefully: directly set isActive and run activation path
        bool prevActive = isActive;
        isActive = true;

        if (isAnimMode && platformAnimator != null && prevActive != isActive)
        {
            platformAnimator.Play(openAnimationName);
            platformRb2D.bodyType = RigidbodyType2D.Kinematic;
            animatorIsDrivingMotion = true;
            yield break;
        }

        if (animatorIsDrivingMotion && !isAnimMode)
        {
            platformRb2D.bodyType = initialBodyType;
            animatorIsDrivingMotion = false;
        }

        UnfreezePlatform();
    }

    public void PlayIdleOpenAnimation()
    {
        if (platformAnimator != null)
        {
            platformAnimator.Play(idleOpenAnimationName);
        }
    }

    public void PlayIdleCloseAnimation()
    {
        if (platformAnimator != null)
        {
            platformAnimator.Play(idleCloseAnimationName);
        }
    }

    // เปลี่ยนจาก Update() เป็น FixedUpdate() เพื่อใช้กับฟิสิกส์
    void FixedUpdate()
    {
        if (platformRb2D == null) return;

        if (isRotationMode)
        {
            RotatePlatform();
        }
        else if (!isAnimMode && !animatorIsDrivingMotion)
        {
            MovePlatform();
        }
    }

    private void MovePlatform()
    {
        Vector2 current = platformRb2D.position;
        Vector2 target = isActive ? new Vector2(upPosition.x, upPosition.y) : new Vector2(downPosition.x, downPosition.y);
        Vector2 newPosition = Vector2.MoveTowards(current, target, speed * Time.deltaTime);

        // ถ้าใกล้ถึงเป้าหมาย (ใช้ threshold ระยะ < 0.02f)
        if (Vector2.Distance(newPosition, target) < 0.02f)
        {
            newPosition = target; // snap ตำแหน่งให้ตรง
        }

        platformRb2D.MovePosition(newPosition);

        if (!isActive && Vector3.Distance(newPosition, downPosition) < 0.01f)
        {
            // Freeze เมื่อถึงตำแหน่งเริ่มต้น (downPosition)
            FreezePlatform();
        }
    }

    private void RotatePlatform()
    {
        float targetRotation = isActive ? openRotationAngle : startRotation;
        currentRotation = Mathf.MoveTowards(currentRotation, targetRotation, rotationSpeed * Time.deltaTime);
        // Platform 2D ควรหมุนรอบ Z
    
        //platformRb2D.MoveRotation(pivotPosition, Vector3.forward, currentRotation - platform.eulerAngles.z);
        platform.RotateAround(pivotPosition, Vector3.forward, currentRotation - platform.eulerAngles.z);
    }

    #region Parenting System

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!useParentingMode) return;
        
        GameObject obj = other.gameObject;
        
        // Check if this is a Glue object
        if (obj.CompareTag(glueControlTag))
        {
            bool glueActivated = HandleGlueEnterPlatform(obj);
            // If glue was activated, re-parent all objects on platform
            if (glueActivated)
            {
                ParentAllObjectsOnPlatform();
            }
        }
        // Check if this is a parentable object
        else if (IsParentableObject(obj))
        {
            // Only parent if one or more Glue objects are present
            if (glueObjects != null && glueObjects.Count > 0)
            {
                HandleObjectEnterPlatform(obj);
            }
            else
            {
                // Just track the object without parenting
                if (!objectsOnPlatform.Contains(obj))
                {
                    objectsOnPlatform.Add(obj);
                    Debug.Log($"Object '{obj.name}' entered platform (no Glue present).");
                }
            }
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!useParentingMode) return;
        
        GameObject obj = other.gameObject;
        
        //// Check if this is a Glue object
        //if (obj.CompareTag(glueControlTag))
        //{
        //    HandleGlueExitPlatform(obj);
        //}
        // Check if this is a parentable object
        /*else*/ if (IsParentableObject(obj))
        {
            HandleObjectExitPlatform(obj);
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!useParentingMode) return;
        
        GameObject obj = collision.gameObject;
        
        // Check if object is on top of platform (not colliding from sides)
        Vector2 contactPoint = collision.contacts[0].point;
        Vector2 platformTop = new Vector2(transform.position.x, transform.position.y + GetComponent<Collider2D>().bounds.size.y / 2);
        
        if (contactPoint.y >= platformTop.y - 0.1f) // Small tolerance
        {
            // Check if this is a Glue object
            if (obj.CompareTag(glueControlTag))
            {
                bool glueActivated = HandleGlueEnterPlatform(obj);
                // If glueActivated is true, it means this was the first glue added (0->1).
                if (glueActivated)
                {
                    ParentAllObjectsOnPlatform();
                }
            }
            // Check if this is a parentable object
            else if (IsParentableObject(obj))
            {
                // Only parent if one or more Glue objects are present
                if (glueObjects != null && glueObjects.Count > 0)
                {
                    HandleObjectEnterPlatform(obj);
                }
                else
                {
                    // Just track the object without parenting
                    if (!objectsOnPlatform.Contains(obj))
                    {
                        objectsOnPlatform.Add(obj);
                        Debug.Log($"Object '{obj.name}' entered platform (no Glue present).");
                    }
                }
            }
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (!useParentingMode) return;
        
        GameObject obj = collision.gameObject;
        
        // Check if this is a Glue object
        if (obj.CompareTag(glueControlTag))
        {
            HandleGlueExitPlatform(obj);
        }
        // Check if this is a parentable object
        else if (IsParentableObject(obj))
        {
            HandleObjectExitPlatform(obj);
        }
    }

    private bool IsParentableObject(GameObject obj)
    {
        foreach (string tag in parentableTagsWhenGlued)
        {
            if (obj.CompareTag(tag))
                return true;
        }
        return false;
    }

    // ========== GLUE CONTROL SYSTEM ==========
    
    private bool HandleGlueEnterPlatform(GameObject glueObj)
    {
        // If this glue is already tracked, ignore
        if (glueObjects.Contains(glueObj))
        {
            return false;
        }

        bool wasEmpty = glueObjects.Count == 0;
        glueObjects.Add(glueObj);
        Debug.Log($"Glue '{glueObj.name}' entered platform. Now {glueObjects.Count} glue(s) present.");

        // Subscribe to destruction event
        GlueDestructionMonitor monitor = glueObj.GetComponent<GlueDestructionMonitor>();
        if (monitor == null)
        {
            monitor = glueObj.AddComponent<GlueDestructionMonitor>();
        }
        monitor.SetPlatform(this);

        // Return true only when this was the first glue (transition 0->1)
        return wasEmpty;
    }
    
    // Called when a Glue object exits the platform (or is destroyed).
    public void HandleGlueExitPlatform(GameObject glueObj)
    {
        if (glueObjects.Contains(glueObj))
        {
            glueObjects.Remove(glueObj);
            if (glueObjects.Count == 0)
            {
                Debug.Log($"Glue '{glueObj.name}' exited platform. No glue remaining — deactivating parenting system.");
                OnGlueRemoved();
            }
            else
            {
                Debug.Log($"Glue '{glueObj.name}' exited platform. {glueObjects.Count} remaining.");
            }
        }
    }
    
    // This method is called when Glue is destroyed or exits platform
    public void OnGlueRemoved()
    {
        glueObjects.Clear();

        // Unparent all currently parented objects
        UnparentAllObjects();

        Debug.Log("Glue removed. All objects unparented and restored to Dynamic.");
    }

    // ========== OBJECT MANAGEMENT ==========
    
    private void HandleObjectEnterPlatform(GameObject obj)
    {
        // Add to tracking set
        if (!objectsOnPlatform.Contains(obj))
        {
            objectsOnPlatform.Add(obj);
            Debug.Log($"Object '{obj.name}' entered platform with Glue present.");
        }
        
        // Parent this object (Glue is already verified to be present)
        if (!parentedObjects.ContainsKey(obj))
        {
            StartCoroutine(DelayedParenting(obj));
        }
    }
    
    private void HandleObjectExitPlatform(GameObject obj)
    {
        // Remove from tracking set
        if (objectsOnPlatform.Contains(obj))
        {
            objectsOnPlatform.Remove(obj);
            Debug.Log($"Object '{obj.name}' exited platform.");
        }
        
        // Unparent if currently parented
        if (parentedObjects.ContainsKey(obj))
        {
            UnparentObject(obj);
        }
    }

    private System.Collections.IEnumerator DelayedParenting(GameObject obj)
    {
        yield return new WaitForSeconds(parentingDelay);
        
        // Check if conditions are still valid
        if (glueObjects != null && glueObjects.Count > 0 && objectsOnPlatform.Contains(obj) && !parentedObjects.ContainsKey(obj))
        {
            ParentObject(obj);
        }
    }
    
    private void ParentAllObjectsOnPlatform()
    {
        foreach (GameObject obj in objectsOnPlatform)
        {
            if (obj != null && !parentedObjects.ContainsKey(obj) && !(glueObjects != null && glueObjects.Contains(obj)))
            {
                StartCoroutine(DelayedParenting(obj));
            }
        }
    }
    
    private void UnparentAllObjects()
    {
        // Create a copy of keys to avoid modification during iteration
        var objectsToUnparent = new System.Collections.Generic.List<GameObject>(parentedObjects.Keys);
        
        foreach (GameObject obj in objectsToUnparent)
        {
            if (obj != null)
            {
                UnparentObject(obj);
            }
        }
    }

    private void ParentObject(GameObject obj)
    {
        if (parentedObjects.ContainsKey(obj)) return;
        if (glueObjects != null && glueObjects.Contains(obj)) return; // Don't parent the Glue itself

        // Collect all Rigidbody2D components on this object and its children
        Rigidbody2D[] rbs = obj.GetComponentsInChildren<Rigidbody2D>(true);
        if (rbs == null || rbs.Length == 0) return;

        // Create ParentedObjectData and record each child's original state
        ParentedObjectData data = new ParentedObjectData(obj.transform.parent);
        foreach (var childRb in rbs)
        {
            var childState = new ParentedObjectData.ChildRigidbodyState(childRb, childRb.bodyType, childRb.constraints);
            data.childRigidbodies.Add(childState);
            if (childRb.bodyType == RigidbodyType2D.Kinematic)
                data.wasKinematicFromGlue = true; // note if any were already kinematic
        }

        parentedObjects[obj] = data;

        // Set parent for the root object (children follow)
        obj.transform.SetParent(platform);

        // Set all child rigidbodies to Kinematic and clear velocities
        foreach (var childState in data.childRigidbodies)
        {
            if (childState.rb != null)
            {
                childState.rb.bodyType = RigidbodyType2D.Kinematic;
                childState.rb.linearVelocity = Vector2.zero;
                childState.rb.angularVelocity = 0f;
            }
        }

        Debug.Log($"Parented '{obj.name}' and its {data.childRigidbodies.Count} Rigidbody2D(s) to platform and set to Kinematic.");
    }

    private void UnparentObject(GameObject obj)
    {
        if (!parentedObjects.ContainsKey(obj)) return;
        ParentedObjectData data = parentedObjects[obj];

        // Restore original parent for the root object
        obj.transform.SetParent(data.originalParent);

        // Restore each child's Rigidbody2D to its original body type and constraints
        foreach (var childState in data.childRigidbodies)
        {
            if (childState == null || childState.rb == null) continue;
            childState.rb.bodyType = childState.originalBodyType;
            childState.rb.constraints = childState.originalConstraints;
        }

        parentedObjects.Remove(obj);

        Debug.Log($"Unparented '{obj.name}' from platform and restored {data.childRigidbodies.Count} Rigidbody2D(s) to original states.");
    }

    private bool HasActiveGlueComponent(GameObject obj)
    {
        // Not used in new system, but kept for compatibility
        return (glueObjects != null && glueObjects.Count > 0);
    }

    // Public method for glue system to notify when glue state changes
    public void OnObjectGlueStateChanged(GameObject obj, bool isGlued)
    {
        // Not used in new system, but kept for compatibility
        // The new system is controlled by Glue presence/absence on platform
    }

    #endregion
}

// ========== GLUE DESTRUCTION MONITOR ==========
// This component monitors when Glue object is destroyed

public class GlueDestructionMonitor : MonoBehaviour
{
    private PlatformController platform;
    
    public void SetPlatform(PlatformController platformController)
    {
        platform = platformController;
    }
    
    void OnDestroy()
    {
        // Notify platform that Glue is being destroyed
        if (platform != null)
        {
            // Notify removal of this specific glue object so platform can track remaining glue instances.
            platform.HandleGlueExitPlatform(gameObject);
            Debug.Log($"Glue '{gameObject.name}' was destroyed. Platform notified of removal.");
        }
    }
}
