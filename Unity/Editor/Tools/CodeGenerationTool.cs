using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Tool for code generation operations
    /// </summary>
    public class CodeGenerationTool : McpToolBase
    {
        public override string ToolName => "code_generate";
        public override string Description => "Generate and manage C# scripts";
        public override string Category => "code";

        [Serializable]
        public class CodeGenerationParameters
        {
            public string action = "create_script";
            public string scriptName = "NewScript";
            public string name = "";
            public string folderPath = "Assets";
            public string savePath = "";
            public string @namespace = "";
            public string baseClass = "MonoBehaviour";
            public string template = "MonoBehaviour";
            public bool overwrite = false;
            public string scriptPath = "";
            public string directory = "";
            public string gameObjectName = "";
            public int gameObjectId = 0;
            public string searchType = "script";
            public string searchTerm = "";
            public string operation = "rename";
            public string oldName = "";
            public string newName = "";
            public string methodName = "";
        }

        public override object Execute(object parameters)
        {
            try
            {
                var args = GetParameters<CodeGenerationParameters>(parameters);
                var action = McpEditorHelpers.GetAction(parameters, args.action ?? "create_script");

                return action switch
                {
                    "create_script" => CreateScript(args),
                    "list_templates" => ListTemplates(),
                    "analyze_scripts" => AnalyzeScripts(args),
                    "attach_script" => AttachScript(args),
                    "find_references" => FindReferences(args),
                    "refactor" => RefactorScript(args),
                    "generate_documentation" => GenerateDocumentation(args),
                    "validate" => ValidateScripts(args),
                    "format" => FormatScripts(args),
                    _ => CreateErrorResponse(
                        $"Unknown code action '{action}'. Supported: create_script, analyze_scripts, attach_script, find_references, refactor, generate_documentation, validate, format")
                };
            }
            catch (Exception e)
            {
                LogError($"Code operation failed: {e.Message}");
                return CreateErrorResponse($"Code operation failed: {e.Message}", e.StackTrace);
            }
        }

        private object CreateScript(CodeGenerationParameters args)
        {
            var scriptName = !string.IsNullOrWhiteSpace(args.name) ? args.name : args.scriptName;
            if (string.IsNullOrWhiteSpace(scriptName))
                return CreateErrorResponse("name/scriptName is required.");

            var folder = !string.IsNullOrWhiteSpace(args.savePath) ? args.savePath : args.folderPath;
            var safeFolder = McpEditorHelpers.EnsureAssetFolder(folder);

            var targetPath = $"{safeFolder}/{scriptName}.cs";
            var exists = File.Exists(targetPath);

            if (exists && !args.overwrite)
                return CreateErrorResponse($"Script '{scriptName}' already exists. Set overwrite=true to replace.");

            var baseClass = args.baseClass;
            if (!string.IsNullOrWhiteSpace(args.template) && string.IsNullOrWhiteSpace(args.baseClass))
            {
                baseClass = args.template switch
                {
                    "ScriptableObject" => "ScriptableObject",
                    "Interface" => null,
                    "Enum" => null,
                    "Class" => null,
                    _ => "MonoBehaviour"
                };
            }

            var contents = BuildTemplate(scriptName, args.@namespace, baseClass ?? "MonoBehaviour", args.template);
            File.WriteAllText(targetPath, contents);
            AssetDatabase.ImportAsset(targetPath);

            return CreateSuccessResponse(new
            {
                path = targetPath,
                name = scriptName,
                folder = safeFolder
            }, "Script created");
        }

        private object AnalyzeScripts(CodeGenerationParameters args)
        {
            var paths = ResolveScriptPaths(args);
            if (paths.Count == 0)
                return CreateErrorResponse("scriptPath or directory is required.");

            var results = new List<object>();
            foreach (var path in paths)
            {
                var content = File.ReadAllText(path);
                var classes = Regex.Matches(content, @"(public|internal)\s+(partial\s+)?class\s+(\w+)")
                    .Cast<Match>()
                    .Select(m => m.Groups[3].Value)
                    .ToArray();

                results.Add(new
                {
                    path,
                    lineCount = content.Split('\n').Length,
                    classes,
                    hasMonoBehaviour = content.Contains(": MonoBehaviour"),
                    hasScriptableObject = content.Contains(": ScriptableObject"),
                    usingCount = Regex.Matches(content, @"^using\s+", RegexOptions.Multiline).Count,
                    summaryCommentCount = Regex.Matches(content, @"///\s*<summary>").Count
                });
            }

            return CreateSuccessResponse(new { analyzed = results.Count, scripts = results }, "Script analysis complete");
        }

        private object AttachScript(CodeGenerationParameters args)
        {
            var go = McpEditorHelpers.FindGameObject(args.gameObjectName, args.gameObjectId);
            if (go == null)
                return CreateErrorResponse("gameObjectName or gameObjectId is required.");

            Type scriptType = null;
            if (!string.IsNullOrWhiteSpace(args.scriptPath))
            {
                var monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(McpEditorHelpers.NormalizeAssetPath(args.scriptPath));
                if (monoScript != null)
                    scriptType = monoScript.GetClass();
            }

            if (scriptType == null && !string.IsNullOrWhiteSpace(args.scriptName))
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    scriptType = assembly.GetTypes().FirstOrDefault(t =>
                        t.Name == args.scriptName && typeof(MonoBehaviour).IsAssignableFrom(t));
                    if (scriptType != null) break;
                }
            }

            if (scriptType == null)
                return CreateErrorResponse("Could not resolve script type. Provide scriptPath to a compiled script.");

            if (go.GetComponent(scriptType) != null)
                return CreateErrorResponse($"Script '{scriptType.Name}' is already attached to '{go.name}'.");

            var component = Undo.AddComponent(go, scriptType);
            return CreateSuccessResponse(new
            {
                gameObject = go.name,
                script = scriptType.Name,
                component = component.GetType().Name
            }, "Script attached");
        }

        private object FindReferences(CodeGenerationParameters args)
        {
            if (string.IsNullOrWhiteSpace(args.searchTerm))
                return CreateErrorResponse("searchTerm is required.");

            var searchPaths = string.IsNullOrWhiteSpace(args.directory)
                ? AssetDatabase.FindAssets("t:Script", new[] { "Assets" })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .ToList()
                : ResolveScriptPaths(args);

            var references = new List<object>();
            foreach (var path in searchPaths)
            {
                var content = File.ReadAllText(path);
                if (content.IndexOf(args.searchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var lines = content.Split('\n');
                    var matchingLines = new List<int>();
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].IndexOf(args.searchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                            matchingLines.Add(i + 1);
                    }

                    references.Add(new { path, matchingLines });
                }
            }

            return CreateSuccessResponse(new
            {
                searchTerm = args.searchTerm,
                matchCount = references.Count,
                references
            }, $"Found {references.Count} file(s) referencing '{args.searchTerm}'");
        }

        private object RefactorScript(CodeGenerationParameters args)
        {
            if (string.IsNullOrWhiteSpace(args.scriptPath))
                return CreateErrorResponse("scriptPath is required.");

            var path = McpEditorHelpers.NormalizeAssetPath(args.scriptPath);
            if (path == null || !File.Exists(path))
                return CreateErrorResponse($"Script not found: {args.scriptPath}");

            var operation = (args.operation ?? "rename").Trim().ToLowerInvariant();

            if (operation != "rename")
                return CreateErrorResponse($"Refactor operation '{operation}' is not yet implemented. Supported: rename.");

            if (string.IsNullOrWhiteSpace(args.oldName) || string.IsNullOrWhiteSpace(args.newName))
                return CreateErrorResponse("oldName and newName are required for rename.");

            var content = File.ReadAllText(path);
            var pattern = $@"\b{Regex.Escape(args.oldName)}\b";
            var newContent = Regex.Replace(content, pattern, args.newName);
            var replacements = Regex.Matches(content, pattern).Count;

            if (replacements == 0)
                return CreateErrorResponse($"No occurrences of '{args.oldName}' found in {path}.");

            File.WriteAllText(path, newContent);
            AssetDatabase.ImportAsset(path);

            return CreateSuccessResponse(new
            {
                path,
                oldName = args.oldName,
                newName = args.newName,
                replacements
            }, $"Renamed {replacements} occurrence(s)");
        }

        private object GenerateDocumentation(CodeGenerationParameters args)
        {
            var paths = ResolveScriptPaths(args);
            if (paths.Count == 0)
                return CreateErrorResponse("scriptPath or directory is required.");

            var documented = 0;
            foreach (var path in paths)
            {
                var content = File.ReadAllText(path);
                if (!content.Contains("/// <summary>"))
                {
                    var classMatch = Regex.Match(content, @"(public\s+(?:partial\s+)?class\s+)(\w+)");
                    if (classMatch.Success)
                    {
                        var insertion = $"/// <summary>\n/// {classMatch.Groups[2].Value} — auto-generated documentation.\n/// </summary>\n";
                        content = content.Insert(classMatch.Index, insertion);
                        File.WriteAllText(path, content);
                        AssetDatabase.ImportAsset(path);
                        documented++;
                    }
                }
            }

            return CreateSuccessResponse(new { filesProcessed = paths.Count, documented }, "Documentation generation complete");
        }

        private object ValidateScripts(CodeGenerationParameters args)
        {
            if (EditorApplication.isCompiling)
                return CreateSuccessResponse(new { compiling = true }, "Scripts are currently compiling.");

            var paths = ResolveScriptPaths(args);
            var issues = new List<object>();

            foreach (var path in paths)
            {
                var content = File.ReadAllText(path);
                if (content.Contains("TODO") || content.Contains("FIXME"))
                    issues.Add(new { path, type = "info", message = "Contains TODO/FIXME markers" });
                if (content.Contains("FindObjectOfType") && !content.Contains("FindObjectOfType<"))
                    issues.Add(new { path, type = "warning", message = "Uses deprecated FindObjectOfType pattern" });
                if (content.Contains("Update()") && content.Contains("MonoBehaviour"))
                    issues.Add(new { path, type = "info", message = "Contains Update() — consider event-driven alternatives" });
            }

            return CreateSuccessResponse(new
            {
                filesChecked = paths.Count,
                issueCount = issues.Count,
                issues
            }, $"Validation complete — {issues.Count} note(s)");
        }

        private object FormatScripts(CodeGenerationParameters args)
        {
            var paths = ResolveScriptPaths(args);
            if (paths.Count == 0)
                return CreateErrorResponse("scriptPath or directory is required.");

            var formatted = 0;
            foreach (var path in paths)
            {
                var lines = File.ReadAllLines(path);
                var sb = new StringBuilder();
                foreach (var line in lines)
                    sb.AppendLine(line.TrimEnd());

                var result = sb.ToString().TrimEnd() + Environment.NewLine;
                if (result != File.ReadAllText(path))
                {
                    File.WriteAllText(path, result);
                    formatted++;
                }
            }

            AssetDatabase.Refresh();
            return CreateSuccessResponse(new { filesProcessed = paths.Count, formatted }, "Format complete");
        }

        private List<string> ResolveScriptPaths(CodeGenerationParameters args)
        {
            var paths = new List<string>();

            if (!string.IsNullOrWhiteSpace(args.scriptPath))
            {
                var p = McpEditorHelpers.NormalizeAssetPath(args.scriptPath);
                if (p != null && File.Exists(p)) paths.Add(p);
            }

            var dir = args.directory;
            if (!string.IsNullOrWhiteSpace(dir))
            {
                var folder = McpEditorHelpers.NormalizeAssetPath(dir) ?? "Assets";
                paths.AddRange(
                    AssetDatabase.FindAssets("t:Script", new[] { folder })
                        .Select(AssetDatabase.GUIDToAssetPath));
            }

            return paths.Distinct().ToList();
        }

        private object ListTemplates()
        {
            return CreateSuccessResponse(new
            {
                templates = new[]
                {
                    new { name = "MonoBehaviour", baseClass = "MonoBehaviour" },
                    new { name = "ScriptableObject", baseClass = "ScriptableObject" },
                    new { name = "Class", baseClass = "object" },
                    new { name = "Interface", baseClass = "(interface)" },
                    new { name = "Enum", baseClass = "(enum)" }
                }
            }, "Available templates listed");
        }

        private string BuildTemplate(string scriptName, string scriptNamespace, string baseClass, string template)
        {
            if (template == "Enum")
            {
                return string.IsNullOrWhiteSpace(scriptNamespace)
                    ? $"public enum {scriptName}\n{{\n    Value1,\n    Value2\n}}\n"
                    : $"namespace {scriptNamespace}\n{{\n    public enum {scriptName}\n    {{\n        Value1,\n        Value2\n    }}\n}}\n";
            }

            if (template == "Interface")
            {
                return string.IsNullOrWhiteSpace(scriptNamespace)
                    ? $"public interface {scriptName}\n{{\n}}\n"
                    : $"namespace {scriptNamespace}\n{{\n    public interface {scriptName}\n    {{\n    }}\n}}\n";
            }

            var nsOpen = string.IsNullOrWhiteSpace(scriptNamespace) ? "" : $"namespace {scriptNamespace}\n{{\n";
            var nsClose = string.IsNullOrWhiteSpace(scriptNamespace) ? "" : "}\n";
            var indent = string.IsNullOrWhiteSpace(scriptNamespace) ? "" : "    ";

            if (baseClass == "ScriptableObject")
            {
                return $@"using UnityEngine;

{nsOpen}{indent}[CreateAssetMenu(fileName = ""{scriptName}"", menuName = ""Custom/{scriptName}"")]
{indent}public class {scriptName} : ScriptableObject
{indent}{{
{indent}}}{nsClose}";
            }

            return $@"using UnityEngine;

{nsOpen}{indent}public class {scriptName} : {baseClass}
{indent}{{
{indent}    private void Start()
{indent}    {{
{indent}    }}

{indent}    private void Update()
{indent}    {{
{indent}    }}
{indent}}}{nsClose}";
        }
    }
}