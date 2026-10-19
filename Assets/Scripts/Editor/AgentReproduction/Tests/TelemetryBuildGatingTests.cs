using System;
using System.IO;
using System.Text;
using AgentReproduction.Reporting;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build.Player;

namespace AgentReproduction.Tests
{
    /// <summary>
    /// 正式构建里没有遥测、面板、叠加层、导演和旧行为树；开发构建里有遥测。
    /// 只编译玩家脚本（不打资源），在编译出的 Assembly-CSharp.dll 元数据里查类型名。
    /// </summary>
    public sealed class TelemetryBuildGatingTests
    {
        private static readonly string[] ReleaseMustNotContain =
        {
            "AgentBrainTelemetryHub", "AgentBrainRecorder", "ExplainScore", "AgentApproachProbeLog", "AgentLegacyBrainTrees", "EngageEnemyActionNode",
            "AgentBrainPanel", "AgentBrainVectorGraphic", "TelemetryConfig", "TelemetryNextAttackTime"
        };

        private static readonly string[] DevelopmentMustContain = { "AgentBrainTelemetryHub", "AgentBrainRecorder", "ExplainScore", "AgentBrainPanel" };

        private static readonly string[] BothMustContain = { "AgentBrainTrees", "AgentEngageMemory", "AgentSearchMemory" };

        [Test, Timeout(1800000)]
        public void ReleaseBuildExcludesTelemetryAndLegacyTrees()
        {
            string root = Path.Combine(Path.GetTempPath(), "brain-build-gating-" + Guid.NewGuid().ToString("N"));
            try
            {
                string release = Compile(Path.Combine(root, "release"), ScriptCompilationOptions.None);
                string development = Compile(Path.Combine(root, "development"), ScriptCompilationOptions.DevelopmentBuild);
                foreach (string name in ReleaseMustNotContain)
                    Assert.That(release.Contains(name), Is.False, "正式构建不应包含 " + name);
                foreach (string name in DevelopmentMustContain)
                    Assert.That(development.Contains(name), Is.True, "开发构建应包含 " + name);
                foreach (string name in BothMustContain)
                {
                    Assert.That(release.Contains(name), Is.True, "正式构建应包含 " + name);
                    Assert.That(development.Contains(name), Is.True, "开发构建应包含 " + name);
                }

                Assert.That(development.Contains("AgentLegacyBrainTrees"), Is.False, "旧行为树只在编辑器中编译");
                CaseArtifactWriter.Trace("build-gating", "release=" + release.Length + " bytes; development=" + development.Length + " bytes");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static string Compile(string output, ScriptCompilationOptions options)
        {
            Directory.CreateDirectory(output);
            var settings = new ScriptCompilationSettings
            {
                target = BuildTarget.StandaloneWindows64,
                group = BuildTargetGroup.Standalone,
                options = options
            };
            ScriptCompilationResult result = PlayerBuildInterface.CompilePlayerScripts(settings, output);
            Assert.That(result.assemblies, Is.Not.Null.And.Not.Empty, "玩家脚本编译失败：" + options);
            string assembly = Path.Combine(output, "Assembly-CSharp.dll");
            Assert.That(File.Exists(assembly), Is.True, "没有生成 Assembly-CSharp.dll：" + options);
            // 类型名和成员名以 UTF-8 存在元数据字符串堆里
            return Encoding.UTF8.GetString(File.ReadAllBytes(assembly));
        }
    }
}
