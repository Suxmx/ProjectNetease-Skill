#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector.Editor;
using Slate;
using UnityEditor;
using UnityEngine;

namespace Hoshino
{
    /// <summary>
    /// 技能数据黑板编辑窗口。弹出式面板，列出并编辑当前技能的特殊数据条目
    /// （如伤害组配置）。通过 <see cref="SkillBlackboardCache"/> 与 Cutscene 关联，
    /// 修改直接写回缓存，保存时由 <see cref="SkillSerializer"/> 序列化进 .skill 文件。
    /// </summary>
    public class SkillBlackboardWindow : EditorWindow
    {
        private Cutscene _cutscene;
        private List<SpecialDataEntry> _entries;
        private Vector2 _scrollPos;
        private List<PropertyTree> _trees = new();
        private List<string> _typeNames = new();

        /// <summary>从 SkillEditor 按钮触发，弹出浮动的数据黑板面板。</summary>
        public static void ShowWindow(Cutscene cutscene)
        {
            SkillBlackboardWindow window = CreateInstance<SkillBlackboardWindow>();
            window.titleContent = new GUIContent("数据黑板");
            window.Init(cutscene);
            window.ShowUtility();
        }

        private void Init(Cutscene cutscene)
        {
            _cutscene = cutscene;
            _entries = SkillBlackboardCache.Get(cutscene);
            minSize = new Vector2(320, 240);
            RebuildTrees();
        }

        private void RebuildTrees()
        {
            foreach (PropertyTree tree in _trees)
            {
                if (tree != null)
                    tree.Dispose();
            }
            _trees.Clear();
            _typeNames.Clear();

            foreach (SpecialDataEntry entry in _entries)
            {
                if (entry.customData == null)
                {
                    _trees.Add(null);
                    _typeNames.Add("(null)");
                    continue;
                }

                _trees.Add(PropertyTree.Create(entry.customData));
                _typeNames.Add(entry.customData.GetType().Name);
            }
        }

        private void OnGUI()
        {
            if (_cutscene == null || _entries == null)
            {
                Close();
                return;
            }

            // --- 顶部工具栏 ---
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("特殊数据", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("添加", EditorStyles.toolbarButton))
                ShowAddMenu();
            EditorGUILayout.EndHorizontal();

            // --- 列表区 ---
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            for (int i = 0; i < _entries.Count; i++)
            {
                DrawEntry(i);
            }

            if (_entries.Count == 0)
                EditorGUILayout.HelpBox("暂无特殊数据。点击\"添加\"创建。", MessageType.Info);

            EditorGUILayout.EndScrollView();
        }

        private void DrawEntry(int index)
        {
            SpecialDataEntry entry = _entries[index];
            PropertyTree tree = _trees[index];

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // --- 条目头：类型名 + 删除按钮 ---
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(_typeNames[index], EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("删除", EditorStyles.miniButton, GUILayout.Width(40)))
            {
                if (tree != null)
                    tree.Dispose();
                _entries.RemoveAt(index);
                _trees.RemoveAt(index);
                _typeNames.RemoveAt(index);
                return;
            }
            EditorGUILayout.EndHorizontal();

            // --- 字段编辑（Odin PropertyTree）---
            if (tree != null)
            {
                tree.Draw(false);
            }
            else
            {
                EditorGUILayout.LabelField("(数据为空)");
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        /// <summary>弹出"添加特殊数据"菜单，列出所有注册的 [SkillSpecialDataType] 类型。</summary>
        private void ShowAddMenu()
        {
            GenericMenu menu = new();
            List<SkillTypeInfo> types = SkillCodeGenUtilities.GatherTypes(SkillSerializedTypeKind.SpecialData);
            foreach (SkillTypeInfo info in types)
            {
                uint id = info.Id;
                string displayName = info.Type.Name;
                menu.AddItem(new GUIContent(displayName), false, () => AddSpecialData(id, info.Type));
            }

            if (types.Count == 0)
                menu.AddDisabledItem(new GUIContent("(无注册类型)"));

            menu.ShowAsContext();
        }

        private void AddSpecialData(uint id, Type type)
        {
            object instance = Activator.CreateInstance(type);
            SpecialDataEntry entry = new()
            {
                dataId = id,
                customData = instance
            };
            _entries.Add(entry);
            _trees.Add(PropertyTree.Create(instance));
            _typeNames.Add(type.Name);
        }

        private void OnDestroy()
        {
            foreach (PropertyTree tree in _trees)
            {
                if (tree != null)
                    tree.Dispose();
            }
            _trees.Clear();
        }
    }
}
#endif
