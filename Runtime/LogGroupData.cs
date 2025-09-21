using UnityEngine;
using System.Collections.Generic;
using System;
using System.IO;
using System.Text.RegularExpressions;

#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public class GroupColorEntry
{
    public string Group;
    public Color Color;
}

public sealed class LogGroupData : ScriptableObject
{
    [SerializeField, HideInInspector] private List<string> groups = new List<string> { "All", "General" };
    [SerializeField, HideInInspector]
    private List<GroupColorEntry> groupColorEntries = new List<GroupColorEntry>
    {
        new GroupColorEntry { Group = "All", Color = Color.white },
        new GroupColorEntry { Group = "General", Color = Color.white }
    };

    public IReadOnlyList<string> Groups => groups.AsReadOnly();
    public IReadOnlyList<GroupColorEntry> GroupColorEntries => groupColorEntries.AsReadOnly();

    // === Duplicate-safe helpers ===
    private static string Sanitize(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        // trim + collapse inner spaces to single space
        var trimmed = name.Trim();
        return Regex.Replace(trimmed, @"\s+", " ");
    }

    private bool ContainsGroup(string group)
    {
        var g = Sanitize(group);
        for (int i = 0; i < groups.Count; i++)
        {
            if (string.Equals(groups[i], g, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

#if UNITY_EDITOR
    public void GenerateStaticClass()
    {
        string path = "Assets/SLoggerGenerated/SLogGroups.cs";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using (StreamWriter writer = new StreamWriter(path))
        {
            writer.WriteLine("// Auto-generated SLogGroups class for Super Logger");
            writer.WriteLine("using System.ComponentModel;");
            writer.WriteLine("");
            writer.WriteLine("namespace PugDev.SuperLogger");
            writer.WriteLine("{");
            writer.WriteLine("public static class SLogGroups");
            writer.WriteLine("{");

            foreach (var group in groups)
            {
                string safeGroupName = group.Replace(" ", "_");
                var entry = groupColorEntries.Find(e => string.Equals(e.Group, group, StringComparison.OrdinalIgnoreCase));
                // string color = ColorUtility.ToHtmlStringRGBA(entry?.Color ?? Color.white); // <- เผื่อจะใช้ต่อภายหลัง

                if (string.Equals(safeGroupName, "All", StringComparison.Ordinal))
                {
                    writer.WriteLine($"    private const string {safeGroupName} = \"{group}\";");
                }
                else
                {
                    writer.WriteLine($"    public const string {safeGroupName} = \"{group}\";");
                }
            }

            writer.WriteLine("}");
            writer.WriteLine("}");
        }
        UnityEditor.AssetDatabase.Refresh();
    }
#endif

    // Add group (duplicate-safe)
    public bool AddGroup(string group, Color color)
    {
        var g = Sanitize(group);
        if (string.IsNullOrEmpty(g)) return false;

        // กันชื่อซ้ำแบบ case-insensitive
        if (ContainsGroup(g)) return false;

        groups.Add(g);
        groupColorEntries.Add(new GroupColorEntry { Group = g, Color = color });
        return true;
    }

    // Remove by name
    public void RemoveGroup(string group)
    {
        var g = Sanitize(group);
        if (string.Equals(g, "All", StringComparison.OrdinalIgnoreCase)) return;
        if (string.Equals(g, "General", StringComparison.OrdinalIgnoreCase)) return;

        if (ContainsGroup(g))
        {
            groups.RemoveAll(x => string.Equals(x, g, StringComparison.OrdinalIgnoreCase));
            groupColorEntries.RemoveAll(e => string.Equals(e.Group, g, StringComparison.OrdinalIgnoreCase));
        }
    }

    // Remove by index
    public void RemoveGroupAt(int index)
    {
        if (index < 0 || index >= groups.Count) return;
        var g = groups[index];
        if (string.Equals(g, "All", StringComparison.OrdinalIgnoreCase)) return;
        if (string.Equals(g, "General", StringComparison.OrdinalIgnoreCase)) return;

        groups.RemoveAt(index);
        groupColorEntries.RemoveAll(e => string.Equals(e.Group, g, StringComparison.OrdinalIgnoreCase));
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(LogGroupData))]
public class LogGroupDataEditor : Editor
{
    private string newGroupName = "NewGroup";
    private Color newGroupColor = Color.white;

    public override void OnInspectorGUI()
    {
        LogGroupData logGroupData = (LogGroupData)target;

        // === Add new group UI ===
        EditorGUILayout.LabelField("Add New Group", EditorStyles.boldLabel);

        newGroupName = EditorGUILayout.TextField("Group Name", newGroupName);
        newGroupColor = EditorGUILayout.ColorField("Color", newGroupColor);

        // ตรวจซ้ำล่วงหน้า (preview) แบบ case-insensitive + trim
        bool isDuplicate = false;
        {
            string previewName = SanitizePreview(newGroupName);
            foreach (var g in logGroupData.Groups)
            {
                if (string.Equals(g, previewName, StringComparison.OrdinalIgnoreCase))
                {
                    isDuplicate = true;
                    break;
                }
            }
        }

        using (new EditorGUI.DisabledScope(isDuplicate || string.IsNullOrWhiteSpace(newGroupName)))
        {
            if (GUILayout.Button("Add Group"))
            {
                Undo.RecordObject(logGroupData, "Add Group");
                if (logGroupData.AddGroup(newGroupName, newGroupColor))
                {
                    EditorUtility.SetDirty(logGroupData);
                    logGroupData.GenerateStaticClass();
                }
                else
                {
                    // เผื่อกรณีผู้ใช้กดทันทีตอนยังขึ้น Duplicate
                    EditorUtility.DisplayDialog("Duplicate Group",
                        $"Group \"{newGroupName}\" already exists.", "OK");
                }
            }
        }

        if (isDuplicate && !string.IsNullOrWhiteSpace(newGroupName))
        {
            EditorGUILayout.HelpBox("This group name already exists (case-insensitive).", MessageType.Warning);
        }

        EditorGUILayout.Space();

        // === Existing groups list ===
        EditorGUILayout.LabelField("Group Colors", EditorStyles.boldLabel);
        var groupEntries = logGroupData.GroupColorEntries;

        for (int i = 0; i < groupEntries.Count; i++)
        {
            if (i == 0) continue;

            var entry = groupEntries[i];

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(entry.Group, GUILayout.Width(140));

            Color newColor = EditorGUILayout.ColorField(entry.Color);
            if (newColor != entry.Color)
            {
                Undo.RecordObject(logGroupData, "Change Group Color");
                entry.Color = newColor;
                EditorUtility.SetDirty(logGroupData);
            }

            if (!string.Equals(entry.Group, "All", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(entry.Group, "General", StringComparison.OrdinalIgnoreCase))
            {
                if (GUILayout.Button("X", GUILayout.Width(22)))
                {
                    Undo.RecordObject(logGroupData, "Remove Group");
                    logGroupData.RemoveGroup(entry.Group);
                    EditorUtility.SetDirty(logGroupData);
                    logGroupData.GenerateStaticClass();
                    GUIUtility.ExitGUI();
                }
            }
            else
            {
                GUILayout.Space(26);
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    private static string SanitizePreview(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        var trimmed = name.Trim();
        return Regex.Replace(trimmed, @"\s+", " ");
    }
}
#endif
