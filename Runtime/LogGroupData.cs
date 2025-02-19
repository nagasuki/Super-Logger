using UnityEngine;
using System.Collections.Generic;
using System;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public class GroupColorEntry
{
    public string Group;
    public Color Color;
}

public class LogGroupData : ScriptableObject
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

#if UNITY_EDITOR
    public void GenerateStaticClass()
    {
        string path = "Assets/SLoggerGenerated/SLogGroups.cs";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using (StreamWriter writer = new StreamWriter(path))
        {
            writer.WriteLine("// Auto-generated LogGroups class for Super Logger");
            writer.WriteLine("using System.ComponentModel;");
            writer.WriteLine("");
            writer.WriteLine("namespace PugDev.SuperLogger");
            writer.WriteLine("{");
            writer.WriteLine("public static class SLogGroups");
            writer.WriteLine("{");

            foreach (var group in groups)
            {
                string safeGroupName = group.Replace(" ", "_");
                var entry = groupColorEntries.Find(e => e.Group == group);
                string color = ColorUtility.ToHtmlStringRGBA(entry.Color);

                if (safeGroupName == "All")
                {
                    writer.WriteLine($"    private const string {safeGroupName} = \"{group}\";");
                    writer.WriteLine($"    [EditorBrowsable(EditorBrowsableState.Never)]");
                    writer.WriteLine($"    private const string {safeGroupName}_Color = \"#{color}\";");
                }
                else
                {
                    writer.WriteLine($"    public const string {safeGroupName} = \"{group}\";");
                    writer.WriteLine($"    [EditorBrowsable(EditorBrowsableState.Never)]");
                    writer.WriteLine($"    public const string {safeGroupName}_Color = \"#{color}\";");
                }
            }

            writer.WriteLine("}");
            writer.WriteLine("}");
        }
        UnityEditor.AssetDatabase.Refresh();
    }
#endif

    // Method to add a group
    public void AddGroup(string group, Color color)
    {
        if (!groups.Contains(group))
        {
            groups.Add(group);
            groupColorEntries.Add(new GroupColorEntry { Group = group, Color = color });
        }
    }

    // Method to remove a group
    public void RemoveGroup(string group)
    {
        if (groups.Contains(group) && group != "All" && group != "General")
        {
            groups.Remove(group);
            groupColorEntries.RemoveAll(e => e.Group == group);
        }
    }

    // Method to remove a group by index
    public void RemoveGroupAt(int index)
    {
        if (index >= 0 && index < groups.Count && groups[index] != "All" && groups[index] != "General")
        {
            string group = groups[index];
            groups.RemoveAt(index);
            groupColorEntries.RemoveAll(e => e.Group == group);
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(LogGroupData))]
public class LogGroupDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        LogGroupData logGroupData = (LogGroupData)target;

        EditorGUILayout.LabelField("Groups", EditorStyles.boldLabel);

        EditorGUI.BeginDisabledGroup(true);
        foreach (string group in logGroupData.Groups)
        {
            EditorGUILayout.TextField(group);
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Group Colors", EditorStyles.boldLabel);
        foreach (var entry in logGroupData.GroupColorEntries)
        {
            Color newColor = EditorGUILayout.ColorField(entry.Group, entry.Color);
            if (newColor != entry.Color)
            {
                Undo.RecordObject(logGroupData, "Change Group Color");
                entry.Color = newColor;
                EditorUtility.SetDirty(logGroupData);
            }
        }
    }
}
#endif