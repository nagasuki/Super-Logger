using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace PugDev.SuperLogger
{
    public class SuperLoggerWindowEditor : EditorWindow
    {
        #region Types

        private class LogEntry
        {
            public readonly string messageRich;   // มีเวลา + group มีสี
            public readonly string messageKey;    // ใช้ collapse (group + detail แบบไม่มีเวลา)
            public readonly string detailText;    // ข้อความจริง (condition เดิม)
            public readonly LogType type;
            public readonly string stackTrace;
            public readonly string group;

            public LogEntry(string messageRich, string messageKey, string detailText, LogType type, string stackTrace, string group)
            {
                this.messageRich = messageRich;
                this.messageKey = messageKey;
                this.detailText = detailText;
                this.type = type;
                this.stackTrace = stackTrace;
                this.group = group;
            }
        }

        private class CollapsedEntry
        {
            public readonly string key;
            public readonly string group;
            public readonly LogType type;

            public int count;
            public string latestRich;
            public string latestStack;
            public readonly string detailText;

            public CollapsedEntry(string key, string group, LogType type, string latestRich, string latestStack, string detailText)
            {
                this.key = key;
                this.group = group;
                this.type = type;
                this.latestRich = latestRich;
                this.latestStack = latestStack;
                this.detailText = detailText;
                this.count = 1;
            }
        }

        #endregion

        #region Fields

        private Vector2 scrollPosition;
        private Vector2 detailScrollPosition;

        private readonly List<LogEntry> logs = new();
        private const int kMaxLogs = 5000;

        // Collapse structures
        private readonly Dictionary<string, CollapsedEntry> collapsedMap = new(); // key -> entry
        private readonly List<string> collapsedOrder = new();                    // insertion order for display
        private CollapsedEntry selectedCollapsed;                                 // selection in collapse mode

        private LogGroupData logGroupData;
        private readonly Dictionary<string, Color> groupColorCache = new();

        private List<string> selectedGroups = new() { "All" };
        private string searchQuery = "";

        private bool showErrors;
        private bool showWarnings;
        private bool showDebugs;
        private bool autoScroll;
        private bool collapseMode; // NEW

        private bool clearOnPlay;
        private bool clearOnBuild;
        private bool clearOnRecompile;

        private int errorCount = 0;
        private int warningCount = 0;
        private int debugCount = 0;

        private Texture2D errorIcon;
        private Texture2D warningIcon;
        private Texture2D debugIcon;

        private LogEntry selectedLog;
        private float splitHeight;
        private float lastClickTime = 0f;

        private GUIStyle styleLog, styleLogBold, styleBox, styleCountBadge;

        private const string SplitHeightKey = "SuperConsole_SplitHeight";
        private const string Pref_ShowErrors = "SuperConsole_ShowErrors";
        private const string Pref_ShowWarnings = "SuperConsole_ShowWarnings";
        private const string Pref_ShowDebugs = "SuperConsole_ShowDebugs";
        private const string Pref_AutoScroll = "SuperConsole_AutoScroll";
        private const string Pref_Collapse = "SuperConsole_Collapse";
        private const string Pref_ClearPlay = "SuperConsole_ClearOnPlay";
        private const string Pref_ClearBuild = "SuperConsole_ClearOnBuild";
        private const string Pref_ClearRecompile = "SuperConsole_ClearOnRecompile";

        private static readonly Regex kStackRegex = new(@"\(at (.+):(\d+)\)", RegexOptions.Compiled);

        private const float doubleClickTime = 0.3f;

        #endregion

        #region Menu

        [MenuItem("Window/Super Console %#x")]
        public static void ShowWindow()
        {
            GetWindow<SuperLoggerWindowEditor>("Super Console");
        }

        #endregion

        #region Unity Messages

        private void OnEnable()
        {
            // prefs
            splitHeight = EditorPrefs.GetFloat(SplitHeightKey, 200f);
            showErrors = EditorPrefs.GetBool(Pref_ShowErrors, true);
            showWarnings = EditorPrefs.GetBool(Pref_ShowWarnings, true);
            showDebugs = EditorPrefs.GetBool(Pref_ShowDebugs, true);
            autoScroll = EditorPrefs.GetBool(Pref_AutoScroll, true);
            collapseMode = EditorPrefs.GetBool(Pref_Collapse, true);
            clearOnPlay = EditorPrefs.GetBool(Pref_ClearPlay, true);
            clearOnBuild = EditorPrefs.GetBool(Pref_ClearBuild, true);
            clearOnRecompile = EditorPrefs.GetBool(Pref_ClearRecompile, false);

            // handlers
            Application.logMessageReceived += HandleLog;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            BuildPlayerWindow.RegisterBuildPlayerHandler(BuildHandler);
            CompilationPipeline.compilationStarted += OnCompilationStarted;

            // icons
            errorIcon = (EditorGUIUtility.IconContent("console.erroricon.sml").image as Texture2D)
                        ?? EditorGUIUtility.FindTexture("console.erroricon");
            warningIcon = (EditorGUIUtility.IconContent("console.warnicon.sml").image as Texture2D)
                          ?? EditorGUIUtility.FindTexture("console.warnicon");
            debugIcon = (EditorGUIUtility.IconContent("console.infoicon.sml").image as Texture2D)
                        ?? EditorGUIUtility.FindTexture("console.infoicon");

            // styles (cache)
            styleLog = new GUIStyle(EditorStyles.label) { richText = true, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            styleLogBold = new GUIStyle(EditorStyles.boldLabel) { richText = true, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            styleBox = new GUIStyle("box") { contentOffset = Vector2.up / 2, richText = true };
            styleCountBadge = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };

            LoadLogGroupData();
        }

        private void OnDisable()
        {
            // prefs
            EditorPrefs.SetFloat(SplitHeightKey, splitHeight);
            EditorPrefs.SetBool(Pref_ShowErrors, showErrors);
            EditorPrefs.SetBool(Pref_ShowWarnings, showWarnings);
            EditorPrefs.SetBool(Pref_ShowDebugs, showDebugs);
            EditorPrefs.SetBool(Pref_AutoScroll, autoScroll);
            EditorPrefs.SetBool(Pref_Collapse, collapseMode);
            EditorPrefs.SetBool(Pref_ClearPlay, clearOnPlay);
            EditorPrefs.SetBool(Pref_ClearBuild, clearOnBuild);
            EditorPrefs.SetBool(Pref_ClearRecompile, clearOnRecompile);

            // handlers
            Application.logMessageReceived -= HandleLog;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            BuildPlayerWindow.RegisterBuildPlayerHandler(BuildPlayerWindow.DefaultBuildMethods.BuildPlayer);
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (clearOnPlay && state == PlayModeStateChange.EnteredPlayMode)
            {
                ClearLogs();
            }
        }

        private static void BuildHandler(BuildPlayerOptions options)
        {
            var window = GetWindow<SuperLoggerWindowEditor>();
            if (window != null && window.clearOnBuild)
                window.ClearLogs();

            BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(options);
        }

        private void OnCompilationStarted(object _)
        {
            if (clearOnRecompile)
            {
                ClearLogs();
            }
        }

        private void OnGUI()
        {
            DrawToolbar();

            var topH = splitHeight;
            Rect logListRect = new Rect(0, 25, position.width, topH);
            GUILayout.BeginArea(logListRect);
            DrawLogList();
            GUILayout.EndArea();

            Rect splitterRect = new Rect(0, 25 + topH, position.width, 3);
            EditorGUI.DrawRect(splitterRect, new Color(0, 0, 0, 0.25f));
            HandleResize(splitterRect);

            Rect logDetailsRect = new Rect(0, 25 + topH + 5, position.width, position.height - (25 + topH + 5));
            GUILayout.BeginArea(logDetailsRect);
            DrawLogDetails();
            GUILayout.EndArea();

            if (autoScroll && Event.current.type == EventType.Repaint)
                scrollPosition.y = float.MaxValue;
        }

        #endregion

        #region Data

        private void LoadLogGroupData()
        {
            string rootFolder = "Assets";
            string subFolder = "SLoggerGenerated";
            string folderPath = $"{rootFolder}/{subFolder}";
            string assetPath = $"{folderPath}/SLogGroups.asset";

            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder(rootFolder, subFolder);
                Debug.Log($"[SuperConsole] Created folder: {folderPath}");
            }

            logGroupData = AssetDatabase.LoadAssetAtPath<LogGroupData>(assetPath);

            if (logGroupData == null)
            {
                logGroupData = ScriptableObject.CreateInstance<LogGroupData>();
                AssetDatabase.CreateAsset(logGroupData, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[SuperConsole] Created new SLogGroups.asset");
            }

            RebuildGroupColorCache();
        }

        private void SaveLogGroups()
        {
            EditorUtility.SetDirty(logGroupData);
            AssetDatabase.SaveAssets();
        }

        private void RebuildGroupColorCache()
        {
            groupColorCache.Clear();
            foreach (var e in logGroupData.GroupColorEntries)
            {
                groupColorCache[e.Group] = e.Color;
            }
        }

        private void ClearLogs()
        {
            logs.Clear();
            collapsedMap.Clear();
            collapsedOrder.Clear();
            selectedCollapsed = null;

            errorCount = warningCount = debugCount = 0;
            selectedLog = null;
            Repaint();
        }

        #endregion

        #region Ingestion

        private static string BuildCollapseKey(LogType type, string group, string keyText)
            => ((int)type) + "|" + group + "|" + keyText;

        private void HandleLog(string condition, string stackTrace, LogType type)
        {
            // prefix [Group]
            string group = "General";
            const string sig = "[Slogger][";
            if (condition.StartsWith(sig, StringComparison.Ordinal))
            {
                int start = sig.Length;
                int end = condition.IndexOf(']', start);
                if (end > start)
                {
                    group = condition.Substring(start, end - start);
                    condition = condition.Substring(end + 1);
                }
            }

            if (!groupColorCache.TryGetValue(group, out var col))
            {
                RebuildGroupColorCache(); // refresh cache
                if (!groupColorCache.TryGetValue(group, out col))
                    col = Color.white;
            }

            string colorHex = ColorUtility.ToHtmlStringRGBA(col);

            string time = DateTime.Now.ToString("HH:mm:ss");

            string keyText = $"[{group}] {condition}".Trim();
            string richTextCondition = $"[{time}] <color=#{colorHex}>[{group}]</color> {condition}";

            // log ธรรมดา
            logs.Add(new LogEntry(richTextCondition, keyText, condition, type, stackTrace, group));

            // collapse
            var ckey = BuildCollapseKey(type, group, keyText);
            if (collapsedMap.TryGetValue(ckey, out var c))
            {
                c.count++;
                c.latestRich = richTextCondition;
                c.latestStack = stackTrace;
            }
            else
            {
                var ce = new CollapsedEntry(ckey, group, type, richTextCondition, stackTrace, condition);
                collapsedMap[ckey] = ce;
                collapsedOrder.Add(ckey);
            }

            // เพิ่ม group ใหม่อัตโนมัติถ้ายังไม่มี
            if (!logGroupData.Groups.Contains(group))
            {
                if (logGroupData.AddGroup(group, Color.white))
                {
                    SaveLogGroups();
                    RebuildGroupColorCache();
                }
            }

            switch (type)
            {
                case LogType.Error: errorCount++; break;
                case LogType.Warning: warningCount++; break;
                case LogType.Log: debugCount++; break;
            }

            if (autoScroll)
                scrollPosition.y = float.MaxValue;

            Repaint();
        }

        #endregion

        #region UI — Toolbar

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            try
            {
                if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    ClearLogs();

                if (GUILayout.Button("▼", EditorStyles.toolbarPopup, GUILayout.Width(24)))
                {
                    var r = GUILayoutUtility.GetLastRect();
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Clear on Play"), clearOnPlay, () => TogglePref(ref clearOnPlay, Pref_ClearPlay));
                    menu.AddItem(new GUIContent("Clear on Build"), clearOnBuild, () => TogglePref(ref clearOnBuild, Pref_ClearBuild));
                    menu.AddItem(new GUIContent("Clear on Recompile"), clearOnRecompile, () => TogglePref(ref clearOnRecompile, Pref_ClearRecompile));
                    var screen = GUIUtility.GUIToScreenPoint(new Vector2(r.x, r.yMax));
                    menu.DropDown(new Rect(screen, Vector2.zero));
                }

                GUILayout.Space(6);
                DrawSearchBarInToolbar();

                GUILayout.FlexibleSpace();

                // Collapse toggle (NEW)
                bool newCollapse = GUILayout.Toggle(collapseMode, "Collapse", EditorStyles.toolbarButton);
                if (newCollapse != collapseMode)
                {
                    collapseMode = newCollapse;
                    EditorPrefs.SetBool(Pref_Collapse, collapseMode);
                    // reset scroll to bottom when switch mode
                    if (autoScroll) scrollPosition.y = float.MaxValue;
                }

                showErrors = GUILayout.Toggle(showErrors, new GUIContent($"{errorCount}", errorIcon), EditorStyles.toolbarButton);
                showWarnings = GUILayout.Toggle(showWarnings, new GUIContent($"{warningCount}", warningIcon), EditorStyles.toolbarButton);
                showDebugs = GUILayout.Toggle(showDebugs, new GUIContent($"{debugCount}", debugIcon), EditorStyles.toolbarButton);

                if (GUILayout.Button("Groups", EditorStyles.toolbarPopup, GUILayout.Width(80)))
                {
                    GUI.FocusControl(null);
                    Rect buttonRect = GUILayoutUtility.GetLastRect();
                    MultiSelectDropdown.ShowDropdown(buttonRect, logGroupData.Groups.ToList(), selectedGroups, (selected) =>
                    {
                        selectedGroups = selected;
                        Repaint();
                    });
                }

                autoScroll = GUILayout.Toggle(autoScroll, "Auto Scroll", EditorStyles.toolbarButton);

                if (GUILayout.Button("View Groups", EditorStyles.toolbarButton))
                {
                    EditorUtility.FocusProjectWindow();
                    Selection.activeObject = logGroupData;
                    EditorGUIUtility.PingObject(logGroupData);
                    EditorApplication.ExecuteMenuItem("Window/General/Inspector");
                }
            }
            finally
            {
                EditorGUILayout.EndHorizontal();
            }
        }

        private void TogglePref(ref bool option, string key)
        {
            option = !option;
            EditorPrefs.SetBool(key, option);
        }

        private void DrawSearchBarInToolbar()
        {
            var tfStyle = GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.toolbarTextField;
            var cancelStyle = GUI.skin.FindStyle("ToolbarSearchCancelButton") ?? GUIStyle.none;

            Rect searchRect = GUILayoutUtility.GetRect(240, 20, tfStyle);
            tfStyle.padding = new RectOffset(6, 22, 2, 2);
            searchQuery = GUI.TextField(searchRect, searchQuery, tfStyle);
            EditorGUIUtility.AddCursorRect(searchRect, MouseCursor.Text);

            var cancelRect = new Rect(searchRect.xMax - 18, searchRect.y + 2, 16, 16);
            if (!string.IsNullOrEmpty(searchQuery) && GUI.Button(cancelRect, GUIContent.none, cancelStyle))
            {
                searchQuery = "";
                GUI.FocusControl(null);
            }
        }

        #endregion

        #region UI — Log List / Details

        private void DrawLogList()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            try
            {
                if (!collapseMode)
                {
                    // Normal mode
                    for (int i = 0; i < logs.Count; i++)
                    {
                        var log = logs[i];

                        if (!PassTypeFilter(log.type)) continue;
                        if (!PassGroupFilter(log.group)) continue;
                        if (!PassSearch(log.detailText)) continue;

                        var style = log.type == LogType.Error ? styleLogBold : styleLog;

                        GUILayout.BeginVertical(styleBox);
                        EditorGUILayout.BeginHorizontal();
                        try
                        {
                            var icon = GetIcon(log.type);
                            DrawLogRow(
                                richText: log.messageRich,
                                type: log.type,
                                onClick: () => HandleLogClick_Normal(log),
                                icon: icon,
                                drawCountBadge: false
                            );
                        }
                        finally
                        {
                            EditorGUILayout.EndHorizontal();
                            GUILayout.EndVertical();
                        }
                    }
                }
                else
                {
                    // Collapse mode
                    for (int idx = 0; idx < collapsedOrder.Count; idx++)
                    {
                        var key = collapsedOrder[idx];
                        if (!collapsedMap.TryGetValue(key, out var ce)) continue;

                        if (!PassTypeFilter(ce.type)) continue;
                        if (!PassGroupFilter(ce.group)) continue;

                        if (!PassSearch(ce.detailText)) continue;

                        var style = ce.type == LogType.Error ? styleLogBold : styleLog;

                        GUILayout.BeginVertical(styleBox);
                        EditorGUILayout.BeginHorizontal();
                        try
                        {
                            var icon = GetIcon(ce.type);
                            DrawLogRow(
                                richText: ce.latestRich,
                                type: ce.type,
                                onClick: () => HandleLogClick_Collapsed(ce),
                                icon: icon,
                                drawCountBadge: true,
                                count: ce.count
                            );
                        }
                        finally
                        {
                            EditorGUILayout.EndHorizontal();
                            GUILayout.EndVertical();
                        }
                    }
                }
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawLogRow(string richText, LogType type, Action onClick, Texture2D icon, bool drawCountBadge = false, int count = 1)
        {
            var style = type == LogType.Error ? styleLogBold : styleLog;

            float contentWidth = position.width - 24f - 12f - (drawCountBadge ? 40f : 0f);
            float textHeight = Mathf.Max(20f, style.CalcHeight(new GUIContent(richText), contentWidth));

            Rect rowRect = GUILayoutUtility.GetRect(0, float.MaxValue, textHeight, textHeight, GUILayout.ExpandWidth(true));

            EditorGUI.DrawRect(rowRect, GetRowBgColor(type));

            if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
                onClick?.Invoke();

            var iconRect = new Rect(rowRect.x + 4, rowRect.y + (rowRect.height - 18f) * 0.5f, 18f, 18f);
            if (icon != null)
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);

            var textRect = new Rect(
                iconRect.xMax + 6f,
                rowRect.y + 2f,
                rowRect.width - (iconRect.width + 4f + 6f) - (drawCountBadge ? 40f : 0f) - 6f,
                rowRect.height - 4f
            );
            GUI.Label(textRect, richText, style);

            if (drawCountBadge)
            {
                var badgeRect = new Rect(rowRect.xMax - 36f, rowRect.y + (rowRect.height - 18f) * 0.5f, 32f, 18f);
                var badgeBg = EditorGUIUtility.isProSkin
                    ? new Color(0.18f, 0.6f, 0.9f, 0.2f)
                    : new Color(0, 0.4f, 0.8f, 0.18f);

                EditorGUI.DrawRect(badgeRect, badgeBg);
                GUI.Label(badgeRect, count.ToString(), styleCountBadge);
            }
        }


        private bool PassTypeFilter(LogType t)
        {
            if (t == LogType.Error && !showErrors) return false;
            if (t == LogType.Warning && !showWarnings) return false;
            if (t == LogType.Log && !showDebugs) return false;
            return true;
        }

        private bool PassGroupFilter(string g)
        {
            return selectedGroups.Contains("All") || selectedGroups.Contains(g);
        }

        private bool PassSearch(string detail)
        {
            if (string.IsNullOrEmpty(searchQuery)) return true;
            return detail.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private Texture2D GetIcon(LogType t)
        {
            return t switch
            {
                LogType.Error => errorIcon,
                LogType.Warning => warningIcon,
                _ => debugIcon
            };
        }

        private void HandleLogClick_Normal(LogEntry log)
        {
            float clickTime = Time.realtimeSinceStartup;
            if (selectedLog == log && (clickTime - lastClickTime) < doubleClickTime)
            {
                OpenScriptFromStackTrace(log.stackTrace);
            }
            else
            {
                selectedLog = log;
                selectedCollapsed = null;
            }
            lastClickTime = clickTime;
        }

        private void HandleLogClick_Collapsed(CollapsedEntry ce)
        {
            float clickTime = Time.realtimeSinceStartup;
            if (selectedCollapsed == ce && (clickTime - lastClickTime) < doubleClickTime)
            {
                OpenScriptFromStackTrace(ce.latestStack);
            }
            else
            {
                selectedCollapsed = ce;
                selectedLog = null;
            }
            lastClickTime = clickTime;
        }

        private void OpenScriptFromStackTrace(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace)) return;

            foreach (var line in stackTrace.Split('\n'))
            {
                var m = kStackRegex.Match(line);
                if (m.Success && int.TryParse(m.Groups[2].Value, out int ln))
                {
                    var path = m.Groups[1].Value;
                    var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    if (obj != null)
                    {
                        AssetDatabase.OpenAsset(obj, ln);
                        return;
                    }
                    UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(path, ln);
                    return;
                }
            }
        }

        private void DrawLogDetails()
        {
            if (!collapseMode)
            {
                if (selectedLog == null) return;

                detailScrollPosition = EditorGUILayout.BeginScrollView(detailScrollPosition, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
                try
                {
                    GUILayout.Label(selectedLog.messageRich, styleLog, GUILayout.ExpandWidth(true));
                    EditorGUILayout.Space(5);
                    DrawStackTrace(selectedLog.stackTrace);
                }
                finally
                {
                    EditorGUILayout.EndScrollView();
                }
            }
            else
            {
                if (selectedCollapsed == null) return;

                detailScrollPosition = EditorGUILayout.BeginScrollView(detailScrollPosition, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
                try
                {
                    GUILayout.Label(selectedCollapsed.latestRich, styleLog, GUILayout.ExpandWidth(true));
                    EditorGUILayout.Space(5);
                    DrawStackTrace(selectedCollapsed.latestStack);
                }
                finally
                {
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void DrawStackTrace(string stackTrace)
        {
            var lines = stackTrace?.Split('\n') ?? Array.Empty<string>();
            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line)) continue;

                var m = kStackRegex.Match(line);
                if (m.Success)
                {
                    string filePath = m.Groups[1].Value;
                    int lineNumber = int.Parse(m.Groups[2].Value);

                    if (GUILayout.Button(line, EditorStyles.linkLabel))
                        OpenStackTraceLine(filePath, lineNumber);
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
                Debug.LogWarning($"[SuperConsole] Unable to load asset at path: {filePath}");
                UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(filePath, lineNumber);
            }
        }

        private static Color GetRowBgColor(LogType t)
        {
            bool pro = EditorGUIUtility.isProSkin;

            Color dbg = pro ? new Color(1f, 1f, 1f, 0.03f) : new Color(0f, 0f, 0f, 0.03f);

            Color warn = pro ? new Color(1f, 1f, 0f, 0.015f) : new Color(0.6f, 0.6f, 0f, 0.012f);

            Color err = pro ? new Color(1f, 0f, 0f, 0.08f) : new Color(1f, 0f, 0f, 0.06f);

            return t switch
            {
                LogType.Error => err,
                LogType.Warning => warn,
                _ => dbg
            };
        }

        #endregion

        #region UI — Splitter

        private void HandleResize(Rect splitterRect)
        {
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeVertical);

            var e = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive);

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (splitterRect.Contains(e.mousePosition))
                    {
                        GUIUtility.hotControl = id;
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        splitHeight = Mathf.Clamp(e.mousePosition.y - 25, 50, Mathf.Max(50, position.height - 100));
                        Repaint();
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        EditorPrefs.SetFloat(SplitHeightKey, splitHeight);
                        e.Use();
                    }
                    break;
            }
        }

        #endregion
    }

    /// <summary>
    /// Multi-select dropdown ที่ไม่พึ่งพา static state และปิดเองเมื่อโฟกัสหาย
    /// </summary>
    public class MultiSelectDropdown : EditorWindow
    {
        private List<string> _items;
        private List<string> _selected;
        private Action<List<string>> _onChange;
        private Vector2 _scroll;

        public static void ShowDropdown(Rect buttonRect, List<string> items, List<string> selectedItems, Action<List<string>> onSelectionChanged)
        {
            var w = CreateInstance<MultiSelectDropdown>();
            w._items = new(items);
            w._selected = new(selectedItems);
            w._onChange = onSelectionChanged;

            var screenPos = GUIUtility.GUIToScreenPoint(new Vector2(buttonRect.x, buttonRect.y + buttonRect.height));
            var height = Mathf.Clamp(items.Count * 20 + 8, 80, 260);
            w.ShowAsDropDown(new Rect(screenPos, Vector2.zero), new Vector2(220, height));
        }

        private void OnGUI()
        {
            bool useScroll = _items.Count > 10;
            if (useScroll) _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(200));

            for (int i = 0; i < _items.Count; i++)
            {
                string it = _items[i];
                bool cur = _selected.Contains(it);
                bool next = EditorGUILayout.ToggleLeft(it, cur);

                if (next != cur)
                {
                    if (next)
                    {
                        if (it == "All")
                            _selected = new List<string> { "All" };
                        else
                        {
                            _selected.Remove("All");
                            if (!_selected.Contains(it)) _selected.Add(it);
                        }
                    }
                    else
                    {
                        _selected.Remove(it);
                    }

                    _onChange?.Invoke(new List<string>(_selected));
                    Repaint();
                }
            }

            if (useScroll) EditorGUILayout.EndScrollView();

            if (_selected.Count == 0)
            {
                _selected.Add("All");
                _onChange?.Invoke(new List<string>(_selected));
            }
        }

        private void OnLostFocus() => Close();
    }
}
