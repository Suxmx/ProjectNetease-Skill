#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Slate;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Path = System.IO.Path;

namespace Hoshino
{
    public class SkillFileManager
    {
        private SkillFileManager() { }
        public const string DATA_FOLDER = "Assets/SkillData";
        public const string FILE_EXTENSION = ".json";

        private static List<WeakReference<SkillFileRef>> trackedRefs = new();

        public static List<string> GetFilePaths()
        {
            EnsureDataFolder();

            var paths = Directory.GetFiles(DATA_FOLDER, "*" + FILE_EXTENSION)
                .Select(p => p.Replace("\\", "/"))
                .ToList();

            paths.AddRange(AssetDatabase.FindAssets("t:TextAsset", new[] { DATA_FOLDER })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(FILE_EXTENSION) && !paths.Contains(p)));

            return paths.Distinct().OrderBy(p => p).ToList();
        }

        public static Cutscene CreateNewFile(string fileName)
        {
            EnsureDataFolder();

            var filePath = $"{DATA_FOLDER}/{fileName}{FILE_EXTENSION}";
            filePath = AssetDatabase.GenerateUniqueAssetPath(filePath);

            var cutscene = Cutscene.Create();
            var data = SkillSerializer.Export(cutscene);
            var json = JsonUtility.ToJson(data, true);

            File.WriteAllText(filePath, json);
            AssetDatabase.Refresh();

            Object.DestroyImmediate(cutscene.gameObject);

            return OpenFile(filePath);
        }

        public static Cutscene OpenFile(string filePath)
        {
            CleanupStaleRefs();

            var json = File.ReadAllText(filePath);
            var cutscene = SkillSerializer.Import(json, filePath);
            if (cutscene == null)
            {
                EditorUtility.DisplayDialog("错误", "无法解析技能文件", "确定");
                return null;
            }

            DisposeOldRefs(filePath);

            var fileRef = new SkillFileRef(cutscene);
            trackedRefs.Add(new WeakReference<SkillFileRef>(fileRef));

            EditorGUIUtility.PingObject(cutscene);
            return cutscene;
        }

        public static void DeleteFile(string filePath)
        {
            CleanupStaleRefs();
            DisposeOldRefs(filePath);
            AssetDatabase.DeleteAsset(filePath);
        }

        public static bool HasUnsavedChanges(Cutscene cutscene, string filePath)
        {
            if (cutscene == null || string.IsNullOrEmpty(filePath)) return false;
            if (!File.Exists(filePath)) return true;

            var jsonOnDisk = File.ReadAllText(filePath);
            var jsonCurrent = SkillSerializer.ExportToJson(cutscene);
            return jsonOnDisk != jsonCurrent;
        }

        static void DisposeOldRefs(string filePath)
        {
            for (int i = trackedRefs.Count - 1; i >= 0; i--)
            {
                if (trackedRefs[i].TryGetTarget(out var fileRef) && fileRef.FilePath == filePath)
                {
                    fileRef.Dispose();
                    trackedRefs.RemoveAt(i);
                }
            }
        }

        static void CleanupStaleRefs()
        {
            trackedRefs.RemoveAll(r => !r.TryGetTarget(out _));
        }

        static void EnsureDataFolder()
        {
            if (!AssetDatabase.IsValidFolder(DATA_FOLDER))
            {
                var parent = Path.GetDirectoryName(DATA_FOLDER);
                AssetDatabase.CreateFolder(parent, Path.GetFileName(DATA_FOLDER));
            }
        }
    }
}

#endif
