using UnityEditor;
using UnityEngine;
using KingsRescue.Audio;

namespace KingsRescue.EditorTools
{
    [CustomEditor(typeof(SoundEffectsSource))]
    public class SoundEffectsSourceEditor : UnityEditor.Editor
    {
        static bool showPlayback;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bank"));
            serializedObject.ApplyModifiedProperties();

            SoundEffectsSource source = (SoundEffectsSource)target;
            if (source.Bank == null)
            {
                EditorGUILayout.HelpBox("Assign SoundBank to edit the clip slots here.", MessageType.Info);
                return;
            }

            if (source.Bank.EnsureSlots())
            {
                EditorUtility.SetDirty(source.Bank);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Clips", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Empty slots stay silent. These clips are shared with gameplay.", MessageType.None);

            SerializedObject bank = new SerializedObject(source.Bank);
            bank.Update();
            SerializedProperty slots = bank.FindProperty("slots");
            showPlayback = EditorGUILayout.Foldout(showPlayback, "Volume, pitch, and repeat delay");

            for (int i = 0; i < slots.arraySize; i++)
            {
                SerializedProperty slot = slots.GetArrayElementAtIndex(i);
                SerializedProperty id = slot.FindPropertyRelative("id");
                string label = ObjectNames.NicifyVariableName(id.enumDisplayNames[id.enumValueIndex]);

                EditorGUILayout.PropertyField(slot.FindPropertyRelative("clip"), new GUIContent(label));
                if (id.enumNames[id.enumValueIndex] == "Typewriter")
                {
                    EditorGUILayout.PropertyField(slot.FindPropertyRelative("variants"), new GUIContent(label + " Takes"), true);
                }

                if (!showPlayback)
                {
                    continue;
                }

                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(slot.FindPropertyRelative("volume"));
                EditorGUILayout.PropertyField(slot.FindPropertyRelative("pitchMin"), new GUIContent("Pitch Min"));
                EditorGUILayout.PropertyField(slot.FindPropertyRelative("pitchMax"), new GUIContent("Pitch Max"));
                EditorGUILayout.PropertyField(slot.FindPropertyRelative("minInterval"), new GUIContent("Min Interval"));
                EditorGUI.indentLevel--;
            }

            bank.ApplyModifiedProperties();
        }
    }
}
