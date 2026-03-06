//#if UNITY_EDITOR
//using UnityEngine;
//using UnityEditor;

//[CustomEditor(typeof(BossPhasePassiveBehaviors))]
//public class BossPhasePassiveBehaviorsEditor : Editor
//{
//    private SerializedProperty phaseAbilitiesProp;
//    private SerializedProperty arenaMinProp;
//    private SerializedProperty arenaMaxProp;
//    private SerializedProperty bossControllerProp;
//    private SerializedProperty playerTransformProp;
//    private SerializedProperty showDebugGizmosProp;
//    private SerializedProperty showDebugLogsProp;

//    private bool[] abilityFoldouts;
//    private GUIStyle headerStyle;
//    private GUIStyle boxStyle;

//    void OnEnable()
//    {
//        phaseAbilitiesProp = serializedObject.FindProperty("phaseAbilities");
//        arenaMinProp = serializedObject.FindProperty("arenaMin");
//        arenaMaxProp = serializedObject.FindProperty("arenaMax");
//        bossControllerProp = serializedObject.FindProperty("bossController");
//        playerTransformProp = serializedObject.FindProperty("playerTransform");
//        showDebugGizmosProp = serializedObject.FindProperty("showDebugGizmos");
//        showDebugLogsProp = serializedObject.FindProperty("showDebugLogs");

//        if (phaseAbilitiesProp.arraySize > 0)
//        {
//            abilityFoldouts = new bool[phaseAbilitiesProp.arraySize];
//        }
//    }

//    public override void OnInspectorGUI()
//    {
//        serializedObject.Update();

//        InitStyles();

//        DrawHeader("Boss Phase Passive Behaviors", new Color(0.3f, 0.6f, 0.9f));

//        EditorGUILayout.Space(10);
//        DrawReferencesSection();

//        EditorGUILayout.Space(10);
//        DrawArenaSection();

//        EditorGUILayout.Space(10);
//        DrawAbilitiesSection();

//        EditorGUILayout.Space(10);
//        DrawDebugSection();

//        serializedObject.ApplyModifiedProperties();
//    }

//    void InitStyles()
//    {
//        if (headerStyle == null)
//        {
//            headerStyle = new GUIStyle(EditorStyles.boldLabel);
//            headerStyle.fontSize = 14;
//            headerStyle.alignment = TextAnchor.MiddleCenter;
//            headerStyle.normal.textColor = Color.white;
//        }

//        if (boxStyle == null)
//        {
//            boxStyle = new GUIStyle(GUI.skin.box);
//            boxStyle.padding = new RectOffset(10, 10, 10, 10);
//        }
//    }

//    void DrawHeader(string title, Color bgColor)
//    {
//        Color originalBg = GUI.backgroundColor;
//        GUI.backgroundColor = bgColor;

//        GUILayout.BeginVertical(EditorStyles.helpBox);
//        GUILayout.Label(title, headerStyle);
//        GUILayout.EndVertical();

//        GUI.backgroundColor = originalBg;
//    }

//    void DrawSectionHeader(string title)
//    {
//        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
//        EditorGUILayout.Space(5);
//    }

//    void DrawReferencesSection()
//    {
//        GUILayout.BeginVertical(boxStyle);
//        DrawSectionHeader("References");

//        EditorGUILayout.PropertyField(bossControllerProp, new GUIContent("Boss Controller"));
//        EditorGUILayout.PropertyField(playerTransformProp, new GUIContent("Player Transform"));

//        GUILayout.EndVertical();
//    }

//    void DrawArenaSection()
//    {
//        GUILayout.BeginVertical(boxStyle);
//        DrawSectionHeader("Arena Bounds");

//        EditorGUILayout.PropertyField(arenaMinProp, new GUIContent("Arena Min"));
//        EditorGUILayout.PropertyField(arenaMaxProp, new GUIContent("Arena Max"));

//        GUILayout.EndVertical();
//    }

//    void DrawAbilitiesSection()
//    {
//        GUILayout.BeginVertical(boxStyle);

//        EditorGUILayout.BeginHorizontal();
//        DrawSectionHeader("Phase Abilities");

//        GUILayout.FlexibleSpace();

//        if (GUILayout.Button("+", GUILayout.Width(30)))
//        {
//            phaseAbilitiesProp.arraySize++;
//            System.Array.Resize(ref abilityFoldouts, phaseAbilitiesProp.arraySize);
//            abilityFoldouts[phaseAbilitiesProp.arraySize - 1] = true;
//        }

//        EditorGUILayout.EndHorizontal();

//        EditorGUILayout.Space(5);

//        if (phaseAbilitiesProp.arraySize == 0)
//        {
//            EditorGUILayout.HelpBox("No abilities configured. Click '+' to add one.", MessageType.Info);
//        }

//        for (int i = 0; i < phaseAbilitiesProp.arraySize; i++)
//        {
//            DrawAbilityElement(i);
//        }

//        GUILayout.EndVertical();
//    }

//    void DrawAbilityElement(int index)
//    {
//        SerializedProperty ability = phaseAbilitiesProp.GetArrayElementAtIndex(index);
//        SerializedProperty abilityName = ability.FindPropertyRelative("abilityName");
//        SerializedProperty phaseIndex = ability.FindPropertyRelative("phaseIndex");
//        SerializedProperty abilityType = ability.FindPropertyRelative("abilityType");

//        Color bgColor = GetPhaseColor(phaseIndex.intValue);
//        Color originalBg = GUI.backgroundColor;
//        GUI.backgroundColor = bgColor;

//        GUILayout.BeginVertical(EditorStyles.helpBox);
//        GUI.backgroundColor = originalBg;

//        EditorGUILayout.BeginHorizontal();

//        string displayName = string.IsNullOrEmpty(abilityName.stringValue)
//            ? $"Ability {index + 1}"
//            : abilityName.stringValue;
//        string typeLabel = abilityType.enumNames[abilityType.enumValueIndex];

//        abilityFoldouts[index] = EditorGUILayout.Foldout(
//            abilityFoldouts[index],
//            $"Phase {phaseIndex.intValue}: {displayName} ({typeLabel})",
//            true,
//            EditorStyles.foldoutHeader
//        );

//        if (GUILayout.Button("×", GUILayout.Width(25)))
//        {
//            phaseAbilitiesProp.DeleteArrayElementAtIndex(index);
//            System.Array.Resize(ref abilityFoldouts, phaseAbilitiesProp.arraySize);
//            GUILayout.EndHorizontal();
//            GUILayout.EndVertical();
//            return;
//        }

//        EditorGUILayout.EndHorizontal();

//        if (abilityFoldouts[index])
//        {
//            EditorGUI.indentLevel++;
//            DrawAbilityProperties(ability);
//            EditorGUI.indentLevel--;
//        }

//        GUILayout.EndVertical();
//        EditorGUILayout.Space(5);
//    }

//    void DrawAbilityProperties(SerializedProperty ability)
//    {
//        EditorGUILayout.Space(5);

//        DrawPropertyGroup(ability, "Phase Info", new[] { "abilityName", "phaseIndex" });
//        DrawPropertyGroup(ability, "Ability Type", new[] { "abilityType", "autoStartOnPhaseChange" });

//        SerializedProperty abilityType = ability.FindPropertyRelative("abilityType");
//        BossPhasePassiveBehaviors.PassiveAbilityType type =
//            (BossPhasePassiveBehaviors.PassiveAbilityType)abilityType.enumValueIndex;

//        bool isCreateObstacle = (type == BossPhasePassiveBehaviors.PassiveAbilityType.CreateObstacle);

//        if (isCreateObstacle)
//        {
//            DrawObstacleSlotsSection(ability);
//        }
//        else
//        {
//            DrawPropertyGroup(ability, "Spawn Settings", new[] {
//                "spawnPrefab", "spawnInterval", "spawnCount"
//            });
//        }

//        DrawWaveLimitSection(ability);

//        if (!isCreateObstacle)
//        {
//            DrawPropertyGroup(ability, "Spawn Area", new[] {
//                "areaType", "spawnOffset", "spawnHeight", "randomRadius"
//            });
//            DrawPropertyGroup(ability, "Spawn Pattern", new[] {
//                "pattern", "patternSpacing"
//            });
//        }

//        DrawPropertyGroup(ability, "Spawn Timing", new[] {
//            "sequentialSpawn", "spawnDelay"
//        });
//        DrawPropertyGroup(ability, "Object Lifetime", new[] {
//            "autoDestroy", "destroyAfter"
//        });
//        DrawPropertyGroup(ability, "Advanced Settings", new[] {
//            "onlyWhenGrounded", "onlyWhenFlying", "startDelay", "maxActiveCount"
//        });
//        DrawPropertyGroup(ability, "Visual Effects", new[] {
//            "warningEffectPrefab", "warningDuration", "warningColor"
//        });
//        DrawPropertyGroup(ability, "Audio", new[] {
//            "spawnSound", "soundVolume"
//        });
//    }

//    void DrawObstacleSlotsSection(SerializedProperty ability)
//    {
//        EditorGUILayout.LabelField("Obstacle Slots", EditorStyles.boldLabel);
//        EditorGUI.indentLevel++;

//        SerializedProperty obstacleSlots = ability.FindPropertyRelative("obstacleSlots");

//        EditorGUILayout.BeginHorizontal();
//        EditorGUILayout.PropertyField(obstacleSlots, new GUIContent("Slots"), false);

//        if (obstacleSlots.isExpanded)
//        {
//            if (GUILayout.Button("+", GUILayout.Width(25)))
//            {
//                obstacleSlots.arraySize++;
//            }
//        }
//        EditorGUILayout.EndHorizontal();

//        if (obstacleSlots.isExpanded)
//        {
//            EditorGUI.indentLevel++;

//            for (int i = 0; i < obstacleSlots.arraySize; i++)
//            {
//                SerializedProperty slot = obstacleSlots.GetArrayElementAtIndex(i);

//                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
//                EditorGUILayout.BeginHorizontal();
//                EditorGUILayout.LabelField($"Slot {i + 1}", EditorStyles.boldLabel);

//                if (GUILayout.Button("×", GUILayout.Width(25)))
//                {
//                    obstacleSlots.DeleteArrayElementAtIndex(i);
//                    EditorGUILayout.EndHorizontal();
//                    EditorGUILayout.EndVertical();
//                    break;
//                }
//                EditorGUILayout.EndHorizontal();

//                EditorGUILayout.PropertyField(slot.FindPropertyRelative("slotPosition"));
//                EditorGUILayout.PropertyField(slot.FindPropertyRelative("relativeToBoss"));
//                EditorGUILayout.PropertyField(slot.FindPropertyRelative("prefabs"), true);
//                EditorGUILayout.PropertyField(slot.FindPropertyRelative("spawnCount"));
//                EditorGUILayout.PropertyField(slot.FindPropertyRelative("startPrefabIndex"));
//                EditorGUILayout.PropertyField(slot.FindPropertyRelative("usePrefabSequence"));
//                EditorGUILayout.PropertyField(slot.FindPropertyRelative("spacingBetweenItems"));

//                EditorGUILayout.EndVertical();
//                EditorGUILayout.Space(3);
//            }

//            EditorGUI.indentLevel--;
//        }

//        EditorGUI.indentLevel--;
//        EditorGUILayout.Space(5);
//    }

//    void DrawWaveLimitSection(SerializedProperty ability)
//    {
//        EditorGUILayout.LabelField("Wave Limit Settings", EditorStyles.boldLabel);
//        EditorGUI.indentLevel++;

//        SerializedProperty useWaveLimit = ability.FindPropertyRelative("useWaveLimit");
//        EditorGUILayout.PropertyField(useWaveLimit);

//        if (useWaveLimit.boolValue)
//        {
//            EditorGUILayout.PropertyField(ability.FindPropertyRelative("maxWaves"));
//            EditorGUILayout.PropertyField(ability.FindPropertyRelative("enemiesPerWave"));
//            EditorGUILayout.PropertyField(ability.FindPropertyRelative("waitForWaveComplete"));
//        }

//        EditorGUI.indentLevel--;
//        EditorGUILayout.Space(5);
//    }

//    void DrawPropertyGroup(SerializedProperty parent, string label, string[] propertyNames)
//    {
//        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
//        EditorGUI.indentLevel++;

//        foreach (string propName in propertyNames)
//        {
//            SerializedProperty prop = parent.FindPropertyRelative(propName);
//            if (prop != null)
//            {
//                EditorGUILayout.PropertyField(prop);
//            }
//        }

//        EditorGUI.indentLevel--;
//        EditorGUILayout.Space(5);
//    }

//    void DrawDebugSection()
//    {
//        GUILayout.BeginVertical(boxStyle);
//        DrawSectionHeader("Debug");

//        EditorGUILayout.PropertyField(showDebugGizmosProp);
//        EditorGUILayout.PropertyField(showDebugLogsProp);

//        GUILayout.EndVertical();
//    }

//    Color GetPhaseColor(int phase)
//    {
//        Color[] colors = new Color[]
//        {
//            new Color(0.8f, 0.9f, 1.0f),   // Phase 0 - Light Blue
//            new Color(1.0f, 0.9f, 0.8f),   // Phase 1 - Light Orange
//            new Color(0.9f, 1.0f, 0.8f),   // Phase 2 - Light Green
//            new Color(1.0f, 0.8f, 0.9f),   // Phase 3 - Light Pink
//            new Color(0.9f, 0.8f, 1.0f),   // Phase 4 - Light Purple
//        };

//        return colors[phase % colors.Length];
//    }
//}
//#endif