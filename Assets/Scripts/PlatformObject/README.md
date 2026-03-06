# Platform Parenting System

ระบบ Parenting ใหม่สำหรับ Platform Controller ที่แก้ปัญหา Physics Lag และการลื่นไถลของ Objects บน Moving Platform

## คุณสมบัติหลัก

### 1. Auto Parenting System
- Objects ที่อยู่บน Platform จะถูก Parent อัตโนมัติ
- ป้องกัน Physics Lag และการลื่นไถล
- รองรับ Delay ก่อน Parent เพื่อป้องกัน Flickering

### 2. Smart Physics Management
- Objects จะกลายเป็น Kinematic เมื่ออยู่บน Platform
- กลับเป็น Dynamic เมื่อออกจาก Platform
- รองรับ Glue System integration

### 3. Glue System Integration
- Objects ที่ถูก Glue จะยัง Kinematic ต่อไปแม้ออกจาก Platform
- กลับเป็น Dynamic เมื่อ Glue หมดและไม่อยู่บน Platform
- ระบบ Priority: Glue > Platform > Default Physics

## วิธีการ Setup

### 1. Platform Setup
```csharp
// ใน Inspector ของ PlatformController
useParentingMode = true;
parentableTags = {"Player", "Box", "CraftedObject", "Item"};
parentingDelay = 0.1f;
```

### 2. Platform GameObject Requirements
- **Rigidbody2D** (สำหรับ Platform movement)
- **Collider2D** ตั้งเป็น **Trigger** (สำหรับ OnTrigger events)
- **Collider2D** อีกอันที่ไม่ใช่ Trigger (สำหรับ OnCollision events)

### 3. Object Setup
```csharp
// เพิ่ม GlueableObject component ให้กับ Objects ที่ต้องการ Glue
GlueableObject glueable = gameObject.AddComponent<GlueableObject>();
glueable.isGlued = false;
glueable.glueStrength = 1f;
```

## การใช้งาน Glue System

### Apply Glue
```csharp
GlueableObject glueable = target.GetComponent<GlueableObject>();
if (glueable != null)
{
    glueable.ApplyGlue(5f); // Glue for 5 seconds
}
```

### Remove Glue
```csharp
glueable.RemoveGlue();
```

### Check Glue Status
```csharp
bool isStuck = glueable.IsStuck;
bool isActive = glueable.IsActive;
```

## หลักการทำงาน

### 1. Object เข้า Platform:
1. OnTriggerEnter2D หรือ OnCollisionEnter2D detect object
2. ตรวจสอบ Tag ว่าอยู่ใน parentableTags หรือไม่
3. รอ parentingDelay แล้ว Parent object
4. เปลี่ยน Rigidbody2D เป็น Kinematic (ถ้าไม่ถูก Glue อยู่แล้ว)

### 2. Object ออกจาก Platform:
1. OnTriggerExit2D หรือ OnCollisionExit2D detect object leaving
2. Unparent object
3. ตรวจสอบ Glue status
4. คืนค่า Rigidbody2D เป็น Dynamic (ถ้าไม่มี Glue)

### 3. Glue State Management:
- **Object บน Platform + Glued**: Kinematic
- **Object บน Platform + Not Glued**: Kinematic  
- **Object นอก Platform + Glued**: Kinematic
- **Object นอก Platform + Not Glued**: Dynamic

## Debug & Monitoring

### Runtime Information
- ใช้ Custom Inspector ดู Objects ที่ถูก Parent
- Console logs แสดงสถานะ Parenting
- Gizmos แสดงสถานะ Glue

### Common Issues

1. **Objects ไม่ถูก Parent**
   - ตรวจสอบ Tag ใน parentableTags
   - ตรวจสอบ Trigger Collider2D
   - ตรวจสอบ Rigidbody2D บน Object

2. **Objects ลื่นไถลแม้มี Parenting**
   - ตรวจสอบว่า useParentingMode = true
   - ตรวจสอบ parentingDelay ไม่สูงเกินไป
   - ตรวจสอบ Collision Detection mode

3. **Glue System ไม่ทำงาน**
   - เพิ่ม GlueableObject component
   - ตรวจสอบการเรียก SetCurrentPlatform()

## ตัวอย่างโค้ด

### Custom Glue Projectile
```csharp
void OnTriggerEnter2D(Collider2D other)
{
    GlueableObject glueable = other.GetComponent<GlueableObject>();
    if (glueable != null)
    {
        glueable.ApplyGlue(5f);
    }
    Destroy(gameObject);
}
```

### Platform Notification
```csharp
public void OnObjectGlueStateChanged(GameObject obj, bool isGlued)
{
    // Platform จะเรียก method นี้เมื่อ Glue state เปลี่ยน
    Debug.Log($"{obj.name} glue state: {isGlued}");
}
```

## Performance Notes

- ระบบใช้ Dictionary สำหรับ O(1) lookup
- Coroutines สำหรับ Delayed Parenting
- Efficient collision detection
- Automatic cleanup เมื่อ destroy objects