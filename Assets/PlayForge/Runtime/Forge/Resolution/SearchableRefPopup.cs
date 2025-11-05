#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    internal class SearchableRefPopup : PopupWindowContent
    {
        readonly string _title;
        readonly List<DataEntry> _items;   // already filtered by the caller (validation)
        readonly int _currentId;
        readonly Action<int> _onChoose;

        string _query = "";
        Vector2 _scroll;
        int _hoverIndex = -1;  // index within filtered list
        List<DataEntry> _filtered;

        const int NoneId = 0;

        public static void Show(Rect activatorRect, string title, IEnumerable<DataEntry> items, int currentId, Action<int> onChoose)
        {
            var list = items?.ToList() ?? new List<DataEntry>();
            var popup = new SearchableRefPopup(title, list, currentId, onChoose);
            PopupWindow.Show(activatorRect, popup);
        }

        SearchableRefPopup(string title, List<DataEntry> items, int currentId, Action<int> onChoose)
        {
            _title = $" {title}";
            _items = items;
            _currentId = currentId;
            _onChoose = onChoose;
            RebuildFilter();
            // set hover to current item if present
            _hoverIndex = _filtered.FindIndex(e => e.Id == currentId);
        }

        public override Vector2 GetWindowSize() => new Vector2(360, 360);

        public override void OnGUI(Rect rect)
        {
            // Header
            var header = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(header, _title, EditorStyles.boldLabel);

            // Search
            var searchRect = new Rect(rect.x + 4, header.yMax + 2, rect.width - 8, EditorGUIUtility.singleLineHeight);
#if UNITY_2021_2_OR_NEWER
            _query = EditorGUI.TextField(searchRect, GUIContent.none, _query, EditorStyles.toolbarSearchField);
#else
            _query = EditorGUI.TextField(searchRect, _query);
#endif
            HandleKeyboard(); // arrows/enter/escape

            if (GUI.changed) RebuildFilter();

            // None row
            var noneRect = new Rect(rect.x, searchRect.yMax + 4, rect.width, EditorGUIUtility.singleLineHeight + 2);
            DrawRow(noneRect, new GUIContent("<None>"), _currentId == NoneId, -1, () => Choose(NoneId));

            // List
            var listTop = noneRect.yMax + 2;
            var listRect = new Rect(rect.x, listTop, rect.width, rect.height - listTop - 4);

            var rowHeight = EditorGUIUtility.singleLineHeight + 2;
            int visibleRows = Mathf.FloorToInt(listRect.height / rowHeight);
            int totalRows = _filtered.Count;
            int hoverClamp = Mathf.Clamp(_hoverIndex, -1, totalRows - 1);
            if (hoverClamp != _hoverIndex) _hoverIndex = hoverClamp;

            _scroll = GUI.BeginScrollView(listRect, _scroll, new Rect(0, 0, listRect.width - 16, totalRows * rowHeight));
            for (int i = 0; i < totalRows; i++)
            {
                var r = new Rect(0, i * rowHeight, listRect.width - 16, rowHeight);
                bool isCurrent = _filtered[i].Id == _currentId;
                bool isHover = i == _hoverIndex;
                DrawRow(r, new GUIContent(DisplayName(_filtered[i])), isCurrent, i, () => Choose(_filtered[i].Id), isHover);
            }
            GUI.EndScrollView();
        }

        void DrawRow(Rect r, GUIContent label, bool isCurrent, int index, Action onClick, bool isHover = false)
        {
            // background
            var bg = isHover ? new Color(0.25f,0.5f,0.9f,0.25f) : (isCurrent ? new Color(0.2f,0.6f,0.2f,0.25f) : new Color(0,0,0,0));
            EditorGUI.DrawRect(new Rect(r.x+1, r.y, r.width-2, r.height), bg);

            // button-like row
            if (GUI.Button(r, GUIContent.none, GUIStyle.none))
            {
                onClick?.Invoke();
                editorWindow?.Close();
                GUIUtility.ExitGUI();
            }

            var labelRect = new Rect(r.x + 8, r.y, r.width - 16, r.height);
            EditorGUI.LabelField(labelRect, label);

            // hover handling
            if (r.Contains(Event.current.mousePosition))
                _hoverIndex = index;
        }

        static string DisplayName(DataEntry e)
        {
            if (e == null) return "<null>";
            return string.IsNullOrEmpty(e.Name) ? $"<{e.Id}>" : e.Name;
        }

        void RebuildFilter()
        {
            var q = (_query ?? "").Trim().ToLowerInvariant();
            IEnumerable<DataEntry> seq = _items;
            if (q.Length > 0)
            {
                seq = seq.Where(e =>
                    (e?.Name ?? "").ToLowerInvariant().Contains(q) ||
                    e.Id.ToString().Contains(q));
            }
            // sort by name, then id
            _filtered = seq.OrderBy(e => e?.Name ?? "", StringComparer.OrdinalIgnoreCase)
                           .ThenBy(e => e?.Id ?? 0)
                           .ToList();
            if (_filtered.Count == 0) _hoverIndex = -1;
        }

        void Choose(int id)
        {
            _onChoose?.Invoke(id);
        }

        void HandleKeyboard()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown) return;

            switch (e.keyCode)
            {
                case KeyCode.DownArrow:
                    _hoverIndex = Mathf.Min(_hoverIndex + 1, _filtered.Count - 1);
                    EnsureVisible();
                    e.Use();
                    break;
                case KeyCode.UpArrow:
                    _hoverIndex = Mathf.Max(_hoverIndex - 1, -1); // -1 selects <None>
                    EnsureVisible();
                    e.Use();
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (_hoverIndex >= 0 && _hoverIndex < _filtered.Count) Choose(_filtered[_hoverIndex].Id);
                    else Choose(NoneId);
                    editorWindow?.Close();
                    e.Use();
                    break;
                case KeyCode.Escape:
                    editorWindow?.Close();
                    e.Use();
                    break;
            }
        }

        void EnsureVisible()
        {
            if (_hoverIndex < 0) return;
            var rowHeight = EditorGUIUtility.singleLineHeight + 2;
            float y = _hoverIndex * rowHeight;
            if (y < _scroll.y) _scroll.y = y;
            if (y + rowHeight > _scroll.y + GetWindowSize().y - 64) _scroll.y = y - (GetWindowSize().y - 64 - rowHeight);
        }
    }
}

#endif