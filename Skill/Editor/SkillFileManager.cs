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
        public const string FILE_EXTENSION = ".skill";

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
            SkillSerializer.SaveBinary(cutscene, filePath);
            EditorApplication.delayCall += () => AssetDatabase.Refresh();

            Object.DestroyImmediate(cutscene.gameObject);

            return OpenFile(filePath);
        }

        public static Cutscene OpenFile(string filePath)
        {
            filePath = NormalizeAssetPath(filePath);
            CleanupStaleRefs();

            var cutscene = SkillSerializer.ImportBinary(filePath);
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
            filePath = NormalizeAssetPath(filePath);
            CleanupStaleRefs();
            DisposeOldRefs(filePath);
            AssetDatabase.DeleteAsset(filePath);
            string debugPath = SkillSerializer.GetDebugJsonPath(filePath);
            if (File.Exists(debugPath))
                AssetDatabase.DeleteAsset(debugPath);
        }

        public static bool HasUnsavedChanges(Cutscene cutscene, string filePath)
        {
            filePath = NormalizeAssetPath(filePath);
            if (cutscene == null || string.IsNullOrEmpty(filePath)) return false;
            if (!File.Exists(filePath)) return true;

            byte[] bytesOnDisk = File.ReadAllBytes(filePath);
            byte[] bytesCurrent = SkillSerializer.ExportToBinary(cutscene);
            if (!bytesOnDisk.SequenceEqual(bytesCurrent))
            {
                return true;
            }
            return false;
        }

        static void LogDiff(string a, string b)
        {
            int minLen = Mathf.Min(a.Length, b.Length);
            for (int i = 0; i < minLen; i++)
            {
                if (a[i] != b[i])
                {
                    int start = Mathf.Max(0, i - 40);
                    int endA = Mathf.Min(a.Length, i + 40);
                    int endB = Mathf.Min(b.Length, i + 40);
                    Debug.LogWarning($"[SkillFileManager] 差异位置: {i}, 盘上长度={a.Length}, 当前长度={b.Length}\n" +
                        $"  盘上: ...{a.Substring(start, endA - start)}...\n" +
                        $"  当前: ...{b.Substring(start, endB - start)}...");
                    return;
                }
            }
            Debug.LogWarning($"[SkillFileManager] 内容相同但长度不同: 盘上={a.Length}, 当前={b.Length}");
        }

        public static string NormalizeAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            path = path.Replace("\\", "/");
            string dataPath = Application.dataPath.Replace("\\", "/");
            if (path.StartsWith(dataPath + "/", StringComparison.OrdinalIgnoreCase))
                return "Assets" + path.Substring(dataPath.Length);

            return path;
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
