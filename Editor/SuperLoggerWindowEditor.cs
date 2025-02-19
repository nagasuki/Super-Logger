using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using System.Collections.Generic;
using System.Linq;

public class SuperLoggerWindowEditor : EditorWindow
{
    private Vector2 scrollPosition;
    private Vector2 detailScrollPosition;
    private List<LogEntry> logs = new List<LogEntry>();
    private LogGroupData logGroupData;
    private string selectedGroup = "All";
    private string searchQuery = "";
    private bool showErrors = true;
    private bool showWarnings = true;
    private bool showDebugs = true;
    private bool autoScroll = true;

    private bool clearOnPlay = true;
    private bool clearOnBuild = true;
    private bool clearOnRecompile = false;

    private Dictionary<string, int> groupCounts = new Dictionary<string, int>();
    private int errorCount = 0;
    private int warningCount = 0;
    private int debugCount = 0;

    private Texture2D errorIcon;
    private Texture2D warningIcon;
    private Texture2D debugIcon;

    private LogEntry selectedLogEntry;

    private class LogEntry
    {
        public string message;
        public LogType type;
        public string stackTrace;
        public string group;

        public LogEntry(string msg, LogType type, string stackTrace, string group)
        {
            message = msg;
            this.type = type;
            this.stackTrace = stackTrace;
            this.group = group;
        }
    }

    [MenuItem("Window/Super Console %#x")]
    public static void ShowWindow()
    {
        GetWindow<SuperLoggerWindowEditor>("Super Console");
    }

    private void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        BuildPlayerWindow.RegisterBuildPlayerHandler(OnBuildStarted);
        CompilationPipeline.compilationStarted += OnCompilationStarted;

        errorIcon = EditorGUIUtility.IconContent("console.erroricon.sml").image as Texture2D;
        warningIcon = EditorGUIUtility.IconContent("console.warnicon.sml").image as Texture2D;
        debugIcon = EditorGUIUtility.IconContent("console.infoicon.sml").image as Texture2D;

        LoadLogGroupData();
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        BuildPlayerWindow.RegisterBuildPlayerHandler(null);
        CompilationPipeline.compilationStarted -= OnCompilationStarted;
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (clearOnPlay && state == PlayModeStateChange.EnteredPlayMode)
        {
            ClearLogs();
        }
    }

    private void OnBuildStarted(BuildPlayerOptions report)
    {
        if (clearOnBuild)
        {
            ClearLogs();
        }
    }

    private void OnCompilationStarted(object obj)
    {
        if (clearOnRecompile)
        {
            ClearLogs();
        }
    }

    private void ClearLogs()
    {
        logs.Clear();
        groupCounts.Clear();
        errorCount = warningCount = debugCount = 0;
        selectedLogEntry = null;
        Repaint();
    }

    private void LoadLogGroupData()
    {
        logGroupData = AssetDatabase.LoadAssetAtPath<LogGroupData>("Assets/SLoggerGenerated/LogGroups.asset");

        if (logGroupData == null)
        {
            logGroupData = CreateInstance<LogGroupData>();
            AssetDatabase.CreateAsset(logGroupData, "Assets/SLoggerGenerated/LogGroups.asset");
            AssetDatabase.SaveAssets();
            Debug.Log("Created new LogGroups.asset");
        }
    }

    private void SaveLogGroups()
    {
        EditorUtility.SetDirty(logGroupData);
        AssetDatabase.SaveAssets();
    }

    private void HandleLog(string condition, string stackTrace, LogType type)
    {
        string group = "General";

        if (condition.StartsWith("[SLogger]["))
        {
            int start = condition.IndexOf("[SLogger][") + 10;
            int end = condition.IndexOf("]", start);
            if (end > start)
            {
                group = condition.Substring(start, end - start);
                condition = condition.Substring(end + 1);
            }
        }

        logs.Add(new LogEntry(condition, type, stackTrace, group));

        if (!logGroupData.Groups.Contains(group))
        {
            logGroupData.AddGroup(group, Color.white); // Add default color
            SaveLogGroups();
        }

        switch (type)
        {
            case LogType.Error: errorCount++; break;
            case LogType.Warning: warningCount++; break;
            case LogType.Log: debugCount++; break;
        }

        if (autoScroll)
        {
            scrollPosition.y = float.MaxValue;
        }

        Repaint();
    }

    private void OnGUI()
    {
        DrawToolbar();
        DrawLogList();
        DrawLogDetails();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        try
        {
            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                ClearLogs();
            }

            // Multi-select dropdown for clear options
            if (GUILayout.Button("", EditorStyles.toolbarPopup, GUILayout.Width(20)))
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Clear on Play"), clearOnPlay, () => ToggleClearOption(ref clearOnPlay));
                menu.AddItem(new GUIContent("Clear on Build"), clearOnBuild, () => ToggleClearOption(ref clearOnBuild));
                menu.AddItem(new GUIContent("Clear on Recompile"), clearOnRecompile, () => ToggleClearOption(ref clearOnRecompile));
                menu.DropDown(new Rect(0, 20, 0, 0));
            }

            DrawSearchBar();

            GUILayout.FlexibleSpace();
            showErrors = GUILayout.Toggle(showErrors, new GUIContent($"{errorCount}", errorIcon), EditorStyles.toolbarButton);
            showWarnings = GUILayout.Toggle(showWarnings, new GUIContent($"{warningCount}", warningIcon), EditorStyles.toolbarButton);
            showDebugs = GUILayout.Toggle(showDebugs, new GUIContent($"{debugCount}", debugIcon), EditorStyles.toolbarButton);

            int selectedIndex = logGroupData.Groups.ToList().IndexOf(selectedGroup);
            selectedIndex = EditorGUILayout.Popup(selectedIndex, logGroupData.Groups.ToArray(), GUILayout.Width(120));
            selectedGroup = logGroupData.Groups[selectedIndex];

            autoScroll = GUILayout.Toggle(autoScroll, "Auto Scroll", EditorStyles.toolbarButton);

            if (GUILayout.Button("Manage Groups", EditorStyles.toolbarButton))
            {
                GroupManagerWindow.ShowWindow(logGroupData);
            }
        }
        finally
        {
            EditorGUILayout.EndHorizontal();
        }
    }

    private void ToggleClearOption(ref bool option)
    {
        option = !option;
    }

    private void DrawSearchBar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        try
        {
            GUIStyle searchBarStyle = new GUIStyle("ToolbarSearchTextField");
            GUIStyle searchCancelStyle = new GUIStyle("ToolbarSearchCancelButton");

            // Add search icon
            GUIContent searchIconContent = EditorGUIUtility.IconContent("Search Icon");
            searchBarStyle.fixedHeight = 20;
            searchBarStyle.stretchHeight = true;
            searchBarStyle.padding = new RectOffset(20, 0, 0, 0);

            // Draw search bar with icon
            Rect searchRect = GUILayoutUtility.GetRect(200, 15, searchBarStyle);
            GUI.Label(new Rect(searchRect.x + 5, searchRect.y + 2, 16, 16), searchIconContent);
            searchQuery = GUI.TextField(searchRect, searchQuery, searchBarStyle);
            GUILayout.ExpandWidth(true);

            // Draw clear button (cross icon) if there is text in the search field
            if (!string.IsNullOrEmpty(searchQuery))
            {
                if (GUILayout.Button("", searchCancelStyle))
                {
                    searchQuery = "";
                    GUI.FocusControl(null);
                }
            }
        }
        finally
        {
            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawLogList()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        try
        {
            foreach (var log in logs)
            {
                if ((log.type == LogType.Error && showErrors) ||
                    (log.type == LogType.Warning && showWarnings) ||
                    (log.type == LogType.Log && showDebugs))
                {
                    if (selectedGroup != "All" && log.group != selectedGroup)
                        continue;

                    if (!string.IsNullOrEmpty(searchQuery) && !log.message.ToLower().Contains(searchQuery.ToLower()))
                        continue;

                    GUIStyle style = EditorStyles.label;
                    Texture2D icon = debugIcon;

                    if (log.type == LogType.Error)
                    {
                        style = EditorStyles.boldLabel;
                        icon = errorIcon;
                    }
                    else if (log.type == LogType.Warning)
                    {
                        icon = warningIcon;
                    }

                    EditorGUILayout.BeginHorizontal();
                    try
                    {
                        if (GUILayout.Button(icon, GUILayout.Width(20), GUILayout.Height(20)))
                        {
                            selectedLogEntry = log;
                        }
                        EditorGUILayout.LabelField($"[{log.group}] {log.message}", style);
                    }
                    finally
                    {
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }
        }
        finally
        {
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawLogDetails()
    {
        if (selectedLogEntry != null)
        {
            detailScrollPosition = EditorGUILayout.BeginScrollView(detailScrollPosition, GUILayout.Height(200));
            try
            {
                EditorGUILayout.BeginVertical("box");
                try
                {
                    EditorGUILayout.LabelField("Log Details", EditorStyles.boldLabel);

                    EditorGUILayout.LabelField("Message:");
                    EditorGUILayout.TextArea(selectedLogEntry.message, GUILayout.ExpandHeight(true));

                    EditorGUILayout.LabelField("Type:", selectedLogEntry.type.ToString());

                    EditorGUILayout.LabelField("Stack Trace:");
                    DrawStackTrace(selectedLogEntry.stackTrace);

                    EditorGUILayout.LabelField("Group:", selectedLogEntry.group);
                }
                finally
                {
                    EditorGUILayout.EndVertical();
                }
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }
    }

    private void DrawStackTrace(string stackTrace)
    {
        string[] stackTraceLines = stackTrace.Split('\n');
        foreach (var line in stackTraceLines)
        {
            if (string.IsNullOrEmpty(line)) continue;

            var match = System.Text.RegularExpressions.Regex.Match(line, @"\(at (.+):(\d+)\)");
            if (match.Success)
            {
                string filePath = match.Groups[1].Value;
                int lineNumber = int.Parse(match.Groups[2].Value);

                if (GUILayout.Button(line, EditorStyles.linkLabel))
                {
                    OpenStackTraceLine(filePath, lineNumber);
                }
            }
            else
            {
                EditorGUILayout.LabelField(line);
            }
        }
    }

    private void OpenStackTraceLine(string filePath, int lineNumber)
    {
        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(filePath);
        if (asset != null)
        {
            AssetDatabase.OpenAsset(asset, lineNumber);
        }
        else
        {
            Debug.LogWarning($"Unable to load asset at path: {filePath}");
        }
    }
}

public class GroupManagerWindow : EditorWindow
{
    private string newGroupName = "";
    private Color newGroupColor = Color.white;
    private LogGroupData logGroupData;

    public static void ShowWindow(LogGroupData logGroupData)
    {
        GroupManagerWindow window = GetWindow<GroupManagerWindow>("Group Manager");
        window.logGroupData = logGroupData;
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        try
        {
            newGroupName = EditorGUILayout.TextField("New Group:", newGroupName);
            newGroupColor = EditorGUILayout.ColorField("Group Color:", newGroupColor);
            if (GUILayout.Button("Add Group"))
            {
                if (!string.IsNullOrEmpty(newGroupName) && !logGroupData.Groups.Contains(newGroupName))
                {
                    logGroupData.AddGroup(newGroupName, newGroupColor);
                    SaveLogGroups();
                    logGroupData.GenerateStaticClass();
                    newGroupName = "";
                    newGroupColor = Color.white;
                    Repaint();
                }
            }
        }
        finally
        {
            EditorGUILayout.EndHorizontal();
        }

        // Dropdown for groups with remove button
        for (int i = 0; i < logGroupData.Groups.Count; i++)
        {
            if (logGroupData.Groups[i] != "All" && logGroupData.Groups[i] != "General")
            {
                EditorGUILayout.BeginHorizontal();
                try
                {
                    GUILayout.Label(logGroupData.Groups[i], GUILayout.Width(100));
                    if (GUILayout.Button("x", GUILayout.Width(20)))
                    {
                        logGroupData.RemoveGroupAt(i);
                        SaveLogGroups();
                        Repaint();
                        break;
                    }
                }
                finally
                {
                    EditorGUILayout.EndHorizontal();
                }
            }
        }
    }

    private void SaveLogGroups()
    {
        EditorUtility.SetDirty(logGroupData);
        AssetDatabase.SaveAssets();
    }
}