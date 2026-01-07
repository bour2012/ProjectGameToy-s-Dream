using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PlatformController))]
public class PlatformControllerEditor : Editor
{
    private SerializedProperty useParentingModeProperty;
    private SerializedProperty parentableTagsProperty;
    private SerializedProperty parentingDelayProperty;

    void OnEnable()
    {
        useParentingModeProperty = serializedObject.FindProperty("useParentingMode");
        parentableTagsProperty = serializedObject.FindProperty("parentableTags");
        parentingDelayProperty = serializedObject.FindProperty("parentingDelay");
    }

    public override void OnInspectorGUI()
    {
        // Draw default inspector
        DrawDefaultInspector();

        // Add some spacing
        EditorGUILayout.Space();

        // Custom section for parenting system
        if (useParentingModeProperty != null && useParentingModeProperty.boolValue)
        {
            EditorGUILayout.LabelField("Parenting System Status", EditorStyles.boldLabel);
            
            PlatformController platform = (PlatformController)target;
            
            if (Application.isPlaying)
            {
                // Show runtime information
                var parentedObjectsField = typeof(PlatformController).GetField("parentedObjects", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (parentedObjectsField != null)
                {
                    var parentedObjects = parentedObjectsField.GetValue(platform);
                    if (parentedObjects != null)
                    {
                        var dict = parentedObjects as System.Collections.Generic.Dictionary<GameObject, PlatformController.ParentedObjectData>;
                        if (dict != null)
                        {
                            EditorGUILayout.LabelField($"Currently Parented Objects: {dict.Count}");
                            
                            foreach (var kvp in dict)
                            {
                                if (kvp.Key != null)
                                {
                                    EditorGUILayout.LabelField($"  • {kvp.Key.name} (Was Kinematic from Glue: {kvp.Value.wasKinematicFromGlue})");
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Parenting System is enabled. Run the game to see active parented objects.", MessageType.Info);
            }

            EditorGUILayout.Space();
            
            // Help box with usage instructions
            EditorGUILayout.HelpBox(
                "Parenting Mode Instructions:\n" +
                "• Add 'Player', 'Box', 'CraftedObject', etc. to Parentable Tags\n" +
                "• Objects with these tags will be automatically parented when on platform\n" +
                "• Use GlueableObject component on objects that can be glued\n" +
                "• Platform requires a Trigger Collider2D for detection\n" +
                "• Objects need Rigidbody2D to work with parenting system", 
                MessageType.Info);
        }

        // Apply changes
        serializedObject.ApplyModifiedProperties();
    }
}