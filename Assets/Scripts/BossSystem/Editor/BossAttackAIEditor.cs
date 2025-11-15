#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(BossAttackAI))]
public class BossAttackAIEditor : Editor
{
    private SerializedProperty controllerProp;
    private SerializedProperty attackSystemProp;
    private SerializedProperty animatorProp;
    private SerializedProperty attackSequenceProp;
    private SerializedProperty phaseAttackSetsProp;
    private SerializedProperty cyclesBeforeExhaustionProp;
    private SerializedProperty exhaustionDurationProp;
    private SerializedProperty passiveAbilitiesFromAIProp;

    private bool[] attackFoldouts;
    private bool[] phaseFoldouts;
    private bool[] passiveFoldouts;

    private GUIStyle headerStyle;
    private GUIStyle subHeaderStyle;
    private GUIStyle boxStyle;

    void OnEnable()
    {
        controllerProp = serializedObject.FindProperty("controller");
        attackSystemProp = serializedObject.FindProperty("attackSystem");
        animatorProp = serializedObject.FindProperty("animator");
        attackSequenceProp = serializedObject.FindProperty("attackSequence");
        phaseAttackSetsProp = serializedObject.FindProperty("phaseAttackSets");
        cyclesBeforeExhaustionProp = serializedObject.FindProperty("cyclesBeforeExhaustion");
        exhaustionDurationProp = serializedObject.FindProperty("exhaustionDuration");
        passiveAbilitiesFromAIProp = serializedObject.FindProperty("passiveAbilitiesFromAI");

        if (attackSequenceProp.arraySize > 0)
        {
            attackFoldouts = new bool[attackSequenceProp.arraySize];
        }

        if (phaseAttackSetsProp.arraySize > 0)
        {
            phaseFoldouts = new bool[phaseAttackSetsProp.arraySize];
        }

        if (passiveAbilitiesFromAIProp.arraySize > 0)
        {
            passiveFoldouts = new bool[passiveAbilitiesFromAIProp.arraySize];
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        InitStyles();

        DrawHeader("Boss Attack AI", new Color(0.9f, 0.4f, 0.4f));

        EditorGUILayout.Space(10);
        DrawReferencesSection();

        EditorGUILayout.Space(10);
        DrawExhaustionSection();

        EditorGUILayout.Space(10);
        DrawDefaultAttackSequenceSection();

        EditorGUILayout.Space(10);
        DrawPerPhaseAttackSetsSection();

        EditorGUILayout.Space(10);
        DrawPassiveAbilitiesSection();

        serializedObject.ApplyModifiedProperties();
    }

    void InitStyles()
    {
        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.fontSize = 14;
            headerStyle.alignment = TextAnchor.MiddleCenter;
            headerStyle.normal.textColor = Color.white;
        }

        if (subHeaderStyle == null)
        {
            subHeaderStyle = new GUIStyle(EditorStyles.boldLabel);
            subHeaderStyle.fontSize = 12;
        }

        if (boxStyle == null)
        {
            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.padding = new RectOffset(10, 10, 10, 10);
        }
    }

    void DrawHeader(string title, Color bgColor)
    {
        Color originalBg = GUI.backgroundColor;
        GUI.backgroundColor = bgColor;

        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label(title, headerStyle);
        GUILayout.EndVertical();

        GUI.backgroundColor = originalBg;
    }

    void DrawSectionHeader(string title)
    {
        EditorGUILayout.LabelField(title, subHeaderStyle);
        EditorGUILayout.Space(5);
    }

    void DrawReferencesSection()
    {
        GUILayout.BeginVertical(boxStyle);
        DrawSectionHeader("Core References");

        EditorGUILayout.PropertyField(controllerProp, new GUIContent("Boss Controller"));
        EditorGUILayout.PropertyField(attackSystemProp, new GUIContent("Attack System"));
        EditorGUILayout.PropertyField(animatorProp, new GUIContent("Animator"));

        GUILayout.EndVertical();
    }

    void DrawExhaustionSection()
    {
        GUILayout.BeginVertical(boxStyle);
        DrawSectionHeader("Exhaustion / Stun Settings");

        EditorGUILayout.PropertyField(cyclesBeforeExhaustionProp, new GUIContent("Cycles Before Exhaustion"));
        EditorGUILayout.PropertyField(exhaustionDurationProp, new GUIContent("Exhaustion Duration (s)"));

        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox(
            "After completing the specified number of attack cycles, the boss will enter an exhausted/stunned state for the configured duration.",
            MessageType.Info
        );

        GUILayout.EndVertical();
    }

    void DrawDefaultAttackSequenceSection()
    {
        GUILayout.BeginVertical(boxStyle);

        EditorGUILayout.BeginHorizontal();
        DrawSectionHeader("Default Attack Sequence");

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("+", GUILayout.Width(30)))
        {
            attackSequenceProp.arraySize++;
            System.Array.Resize(ref attackFoldouts, attackSequenceProp.arraySize);
            attackFoldouts[attackSequenceProp.arraySize - 1] = true;
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        if (attackSequenceProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("No attacks configured. Click '+' to add one.", MessageType.Info);
        }

        for (int i = 0; i < attackSequenceProp.arraySize; i++)
        {
            DrawAttackEntry(attackSequenceProp.GetArrayElementAtIndex(i), i, ref attackFoldouts);
        }

        GUILayout.EndVertical();
    }

    void DrawPerPhaseAttackSetsSection()
    {
        GUILayout.BeginVertical(boxStyle);

        EditorGUILayout.BeginHorizontal();
        DrawSectionHeader("Per-Phase Attack Sets (Optional)");

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("+", GUILayout.Width(30)))
        {
            phaseAttackSetsProp.arraySize++;
            System.Array.Resize(ref phaseFoldouts, phaseAttackSetsProp.arraySize);
            phaseFoldouts[phaseAttackSetsProp.arraySize - 1] = true;
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        if (phaseAttackSetsProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("No phase-specific attack sets configured. Boss will use the default sequence for all phases.", MessageType.Info);
        }

        for (int i = 0; i < phaseAttackSetsProp.arraySize; i++)
        {
            DrawPhaseAttackSet(i);
        }

        GUILayout.EndVertical();
    }

    void DrawPhaseAttackSet(int index)
    {
        SerializedProperty phaseSet = phaseAttackSetsProp.GetArrayElementAtIndex(index);
        SerializedProperty phaseIndex = phaseSet.FindPropertyRelative("phaseIndex");
        SerializedProperty entries = phaseSet.FindPropertyRelative("entries");

        Color phaseColor = GetPhaseColor(phaseIndex.intValue);
        Color originalBg = GUI.backgroundColor;
        GUI.backgroundColor = phaseColor;

        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUI.backgroundColor = originalBg;

        EditorGUILayout.BeginHorizontal();

        phaseFoldouts[index] = EditorGUILayout.Foldout(
            phaseFoldouts[index],
            $"Phase {phaseIndex.intValue} Attack Set ({entries.arraySize} attacks)",
            true,
            EditorStyles.foldoutHeader
        );

        if (GUILayout.Button("×", GUILayout.Width(25)))
        {
            phaseAttackSetsProp.DeleteArrayElementAtIndex(index);
            System.Array.Resize(ref phaseFoldouts, phaseAttackSetsProp.arraySize);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            return;
        }

        EditorGUILayout.EndHorizontal();

        if (phaseFoldouts[index])
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(phaseIndex, new GUIContent("Phase Index"));

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Attack Entries", EditorStyles.boldLabel);

            if (GUILayout.Button("+", GUILayout.Width(25)))
            {
                entries.arraySize++;
            }

            EditorGUILayout.EndHorizontal();

            if (entries.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No attacks in this phase set.", MessageType.Warning);
            }

            for (int j = 0; j < entries.arraySize; j++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(j);

                GUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Attack {j + 1}", GUILayout.Width(70));

                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    entries.DeleteArrayElementAtIndex(j);
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                    break;
                }

                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel++;
                DrawAttackEntryFields(entry);
                EditorGUI.indentLevel--;

                GUILayout.EndVertical();
                EditorGUILayout.Space(3);
            }

            EditorGUI.indentLevel--;
        }

        GUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }

    void DrawAttackEntry(SerializedProperty entry, int index, ref bool[] foldouts)
    {
        SerializedProperty attackIndex = entry.FindPropertyRelative("attackIndex");
        SerializedProperty triggerName = entry.FindPropertyRelative("triggerName");
        SerializedProperty enabled = entry.FindPropertyRelative("enabled");

        Color bgColor = enabled.boolValue ? new Color(0.9f, 1f, 0.9f) : new Color(1f, 0.9f, 0.9f);
        Color originalBg = GUI.backgroundColor;
        GUI.backgroundColor = bgColor;

        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUI.backgroundColor = originalBg;

        EditorGUILayout.BeginHorizontal();

        string displayName = $"Attack {index + 1}: Index {attackIndex.intValue} ({triggerName.stringValue})";
        if (!enabled.boolValue) displayName += " [DISABLED]";

        foldouts[index] = EditorGUILayout.Foldout(foldouts[index], displayName, true);

        if (GUILayout.Button("×", GUILayout.Width(25)))
        {
            attackSequenceProp.DeleteArrayElementAtIndex(index);
            System.Array.Resize(ref foldouts, attackSequenceProp.arraySize);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            return;
        }

        EditorGUILayout.EndHorizontal();

        if (foldouts[index])
        {
            EditorGUI.indentLevel++;
            DrawAttackEntryFields(entry);
            EditorGUI.indentLevel--;
        }

        GUILayout.EndVertical();
        EditorGUILayout.Space(3);
    }

    void DrawAttackEntryFields(SerializedProperty entry)
    {
        EditorGUILayout.PropertyField(entry.FindPropertyRelative("attackIndex"), new GUIContent("Attack Index"));
        EditorGUILayout.PropertyField(entry.FindPropertyRelative("triggerName"), new GUIContent("Trigger Name"));
        EditorGUILayout.PropertyField(entry.FindPropertyRelative("postCooldown"), new GUIContent("Post Cooldown (s)"));
        EditorGUILayout.PropertyField(entry.FindPropertyRelative("enabled"), new GUIContent("Enabled"));
    }

    void DrawPassiveAbilitiesSection()
    {
        GUILayout.BeginVertical(boxStyle);

        EditorGUILayout.BeginHorizontal();
        DrawSectionHeader("Inspector Passive Abilities (Optional)");

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("+", GUILayout.Width(30)))
        {
            passiveAbilitiesFromAIProp.arraySize++;
            System.Array.Resize(ref passiveFoldouts, passiveAbilitiesFromAIProp.arraySize);
            passiveFoldouts[passiveAbilitiesFromAIProp.arraySize - 1] = true;
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox(
            "Define passive abilities here to expose them to Animation Events. They will be forwarded to BossPhasePassiveBehaviors for execution.",
            MessageType.Info
        );

        if (passiveAbilitiesFromAIProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("No passive abilities configured.", MessageType.Info);
        }

        for (int i = 0; i < passiveAbilitiesFromAIProp.arraySize; i++)
        {
            DrawPassiveAbilityReference(i);
        }

        GUILayout.EndVertical();
    }

    void DrawPassiveAbilityReference(int index)
    {
        SerializedProperty ability = passiveAbilitiesFromAIProp.GetArrayElementAtIndex(index);

        if (ability == null) return;

        SerializedProperty abilityName = ability.FindPropertyRelative("abilityName");
        SerializedProperty abilityType = ability.FindPropertyRelative("abilityType");

        Color originalBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.9f, 0.95f, 1f);

        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUI.backgroundColor = originalBg;

        EditorGUILayout.BeginHorizontal();

        string displayName = string.IsNullOrEmpty(abilityName.stringValue)
            ? $"Passive {index + 1}"
            : abilityName.stringValue;

        string typeLabel = abilityType != null ? abilityType.enumNames[abilityType.enumValueIndex] : "Unknown";

        passiveFoldouts[index] = EditorGUILayout.Foldout(
            passiveFoldouts[index],
            $"{displayName} ({typeLabel})",
            true,
            EditorStyles.foldoutHeader
        );

        if (GUILayout.Button("×", GUILayout.Width(25)))
        {
            passiveAbilitiesFromAIProp.DeleteArrayElementAtIndex(index);
            System.Array.Resize(ref passiveFoldouts, passiveAbilitiesFromAIProp.arraySize);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            return;
        }

        EditorGUILayout.EndHorizontal();

        if (passiveFoldouts[index])
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(ability, new GUIContent($"Passive Ability {index + 1}"), true);
            EditorGUI.indentLevel--;
        }

        GUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }

    Color GetPhaseColor(int phase)
    {
        Color[] colors = new Color[]
        {
            new Color(0.8f, 0.9f, 1.0f),   // Phase 0 - Light Blue
            new Color(1.0f, 0.9f, 0.8f),   // Phase 1 - Light Orange
            new Color(0.9f, 1.0f, 0.8f),   // Phase 2 - Light Green
            new Color(1.0f, 0.8f, 0.9f),   // Phase 3 - Light Pink
            new Color(0.9f, 0.8f, 1.0f),   // Phase 4 - Light Purple
        };

        return colors[phase % colors.Length];
    }
}
#endif