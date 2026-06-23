using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 批量为项目脚本添加命名空间。按 Step 顺序执行，每步完成后编译验证。
/// Step1: 底层模块（Common, Foundation, Data, Events, DI）
/// Step2: 工具层（Input, Character, Map, Teleport）
/// Step3: 系统层（Network, Combat, Skill, Task, NPC, Player, Monster）
/// Step4: 顶层（UI, Managers）
/// 
/// 使用：菜单 -> Tools -> Namespace Fixer -> 选择 Step
/// </summary>
public class NamespaceFixer : EditorWindow
{
    private static readonly List<Step> Steps = new()
    {
        new Step("Step 1 - 底层", new ()
        {
            { "Assets/Script/Common/",       "Game.Common" },
            { "Assets/Script/ProjectBase/",   "Game.Foundation" },
            { "Assets/Script/Data/",          "Game.Data" },
            { "Assets/Script/Events/",        "Game.Events" },
            { "Assets/Script/VContainer/",    "Game.DI" },
        }),
        new Step("Step 2 - 工具层", new ()
        {
            { "Assets/Script/Input/",         "Game.Input" },
            { "Assets/Script/Character/",     "Game.Character" },
            { "Assets/Script/Map/",           "Game.Map" },
            { "Assets/Script/Teleport/",      "Game.Teleport" },
        }),
        new Step("Step 3 - 系统层", new ()
        {
            { "Assets/Script/Network/",       "Game.Network" },
            { "Assets/Script/Combat/",        "Game.Combat" },
            { "Assets/Script/Skill/",         "Game.Skill" },
            { "Assets/Script/Task/",          "Game.Task" },
            { "Assets/Script/NPC/",           "Game.NPC" },
            { "Assets/Script/Player/",        "Game.Player" },
            { "Assets/Script/Monster/",       "Game.Monster" },
        }),
        new Step("Step 4 - 顶层", new ()
        {
            { "Assets/Script/UI/",            "Game.UI" },
            { "Assets/Script/Mgr/",           "Game.Managers" },
        }),
    };

    // 定义了命名空间的目录，跳过它们（已处理过的或手动处理的）
    private static readonly HashSet<string> ExcludedDirs = new()
    {
        "Assets/Script/Editor/",  // 编辑器代码保持全局或独立
        "Assets/Script/Utilit/",  // 第三方/模板代码
        "Assets/Script/Dev/",     // 开发工具
        "Assets/Script/Tests/",   // 测试代码
        "Assets/Script/Script/",  // Script 子目录
    };

    private int selectedStep = 0;
    private bool dryRun = true;
    private Vector2 scrollPos;

    [MenuItem("Tools/Namespace Fixer")]
    private static void ShowWindow()
    {
        var window = GetWindow<NamespaceFixer>("命名空间修复工具");
        window.minSize = new Vector2(500, 400);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("命名空间批量添加工具", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "按 Step 顺序执行，每步完成后等待 Unity 编译通过再执行下一步。\n" +
            "建议先勾选「Dry Run」预览变更，确认无误后再取消勾选实际执行。",
            MessageType.Info);

        EditorGUILayout.Space(5);
        dryRun = EditorGUILayout.Toggle("Dry Run（仅预览）", dryRun);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("选择执行步骤：", EditorStyles.boldLabel);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        for (int i = 0; i < Steps.Count; i++)
        {
            var step = Steps[i];

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(step.name, EditorStyles.boldLabel);
            foreach (var kv in step.mapping)
            {
                EditorGUILayout.LabelField($"    {kv.Key}  →  {kv.Value}");
            }
            EditorGUILayout.EndVertical();

            GUI.backgroundColor = selectedStep == i ? Color.green : Color.white;
            if (GUILayout.Button($"执行 {step.name}", GUILayout.Height(30)))
            {
                if (dryRun)
                {
                    DryRun(step);
                }
                else
                {
                    ExecuteStep(step);
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.Space(5);
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(10);
        if (GUILayout.Button("全部 Dry Run", GUILayout.Height(30)))
        {
            foreach (var step in Steps)
                DryRun(step);
        }
    }

    /// <summary>
    /// 预览模式：只打印将要修改的内容，不动文件
    /// </summary>
    private void DryRun(Step step)
    {
        int totalFiles = 0;
        int affectedFiles = 0;
        var sb = new System.Text.StringBuilder();

        foreach (var kv in step.mapping)
        {
            string dir = kv.Key;
            string ns = kv.Value;

            if (!Directory.Exists(dir))
            {
                Debug.LogWarning($"[NamespaceFixer] 目录不存在: {dir}");
                continue;
            }

            var files = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                string relativePath = file.Replace("\\", "/");
                if (ExcludedDirs.Any(e => relativePath.StartsWith(e)))
                    continue;

                totalFiles++;
                if (HasNamespace(file))
                    continue;

                affectedFiles++;
                sb.AppendLine($"  [待添加] {relativePath}  →  namespace {ns}");
            }
        }

        Debug.Log($"<color=cyan>[NamespaceFixer DryRun] {step.name}</color>\n" +
                  $"检查文件: {totalFiles}, 需要添加命名空间: {affectedFiles}\n" +
                  (affectedFiles > 0 ? sb.ToString() : "  ✅ 该步骤无需修改！"));
    }

    /// <summary>
    /// 实际执行：为文件添加命名空间
    /// </summary>
    private void ExecuteStep(Step step)
    {
        int total = 0;
        int modified = 0;

        foreach (var kv in step.mapping)
        {
            string dir = kv.Key;
            string ns = kv.Value;

            if (!Directory.Exists(dir))
            {
                Debug.LogWarning($"[NamespaceFixer] 目录不存在: {dir}");
                continue;
            }

            var files = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                string relativePath = file.Replace("\\", "/");

                // 排除指定目录
                if (ExcludedDirs.Any(e => relativePath.StartsWith(e)))
                    continue;

                // 跳过已有命名空间的文件
                if (HasNamespace(file))
                    continue;

                total++;
                if (WrapInNamespace(file, ns))
                    modified++;
            }
        }

        Debug.Log($"<color=green>[NamespaceFixer] {step.name} 完成！</color>\n" +
                  $"处理文件: {total}, 已修改: {modified}");
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// 检测文件是否已定义命名空间
    /// </summary>
    private static bool HasNamespace(string filePath)
    {
        var content = File.ReadAllText(filePath);
        return Regex.IsMatch(content, @"^\s*namespace\s+\w", RegexOptions.Multiline);
    }

    /// <summary>
    /// 将 .cs 文件内容包裹在命名空间里
    /// 处理逻辑：
    /// 1. 保留文件顶部的 using 语句
    /// 2. 将 using 后面的所有代码包裹进 namespace { }
    /// 3. 正确处理缩进（顶级内容增加 4 个空格）
    /// </summary>
    private static bool WrapInNamespace(string filePath, string namespaceName)
    {
        try
        {
            var lines = File.ReadAllLines(filePath);
            if (lines.Length == 0) return false;

            // 找到第一个非空、非 using、非注释的实际代码行（namespace 内容起始位置）
            int usingEndLine = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                if (!string.IsNullOrEmpty(trimmed) &&
                    !trimmed.StartsWith("using ") &&
                    !trimmed.StartsWith("//") &&
                    !trimmed.StartsWith("/*") &&
                    !trimmed.StartsWith("*") &&
                    !trimmed.StartsWith("#"))
                {
                    usingEndLine = i;
                    break;
                }
            }

            var result = new System.Text.StringBuilder();

            // 写入 using 部分（保持不变）
            for (int i = 0; i < usingEndLine; i++)
            {
                result.AppendLine(lines[i]);
            }

            // 写入 namespace 开头的空行 + namespace 声明
            if (usingEndLine > 0 && !string.IsNullOrWhiteSpace(lines[usingEndLine - 1]))
                result.AppendLine();

            result.AppendLine($"namespace {namespaceName}");
            result.AppendLine("{");

            // 写入 namespace 内部内容（统一缩进 4 空格）
            for (int i = usingEndLine; i < lines.Length; i++)
            {
                string line = lines[i];
                // 跳过末尾的纯空行
                if (i == lines.Length - 1 && string.IsNullOrWhiteSpace(line))
                    continue;

                if (string.IsNullOrWhiteSpace(line))
                    result.AppendLine();
                else
                    result.AppendLine("    " + line);
            }

            result.AppendLine("}");

            File.WriteAllText(filePath, result.ToString());
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[NamespaceFixer] 处理文件失败: {filePath}\n{ex.Message}");
            return false;
        }
    }

    private class Step
    {
        public string name;
        /// <summary>目录路径 → 命名空间名称</summary>
        public Dictionary<string, string> mapping;

        public Step(string name, Dictionary<string, string> mapping)
        {
            this.name = name;
            this.mapping = mapping;
        }
    }
}
