#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Gameplay.Demo
{
    /// <summary>
    /// 导演事件记录：每行一个 JSON 对象（JSONL）。编辑器里写到项目的 Logs/Demo，开发构建写到持久数据目录的 DemoLogs。
    /// </summary>
    public sealed class AgentDemoLog : IDisposable
    {
        private readonly StreamWriter _writer;
        private readonly StringBuilder _line = new StringBuilder(256);
        private readonly float _startTime;

        public AgentDemoLog(string sceneId)
        {
            string folder = Application.isEditor
                ? Path.Combine(Directory.GetCurrentDirectory(), "Logs", "Demo")
                : Path.Combine(Application.persistentDataPath, "DemoLogs");
            Directory.CreateDirectory(folder);
            FilePath = Path.Combine(folder, sceneId + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + ".jsonl");
            _writer = new StreamWriter(FilePath, false, new UTF8Encoding(false));
            _startTime = Time.realtimeSinceStartup;
        }

        public string FilePath { get; }

        public int LineCount { get; private set; }

        /// <summary>开始一行：kind 之后用 Field 追加字段，最后 End</summary>
        public AgentDemoLog Begin(string kind)
        {
            _line.Clear();
            _line.Append("{\"wall\":").Append((Time.realtimeSinceStartup - _startTime).ToString("0.000", CultureInfo.InvariantCulture));
            _line.Append(",\"game\":").Append(Time.timeSinceLevelLoadAsDouble.ToString("0.000", CultureInfo.InvariantCulture));
            return Field("kind", kind);
        }

        public AgentDemoLog Field(string name, string value)
        {
            _line.Append(",\"").Append(name).Append("\":\"");
            Escape(value ?? string.Empty);
            _line.Append('"');
            return this;
        }

        public AgentDemoLog Field(string name, double value)
        {
            _line.Append(",\"").Append(name).Append("\":").Append(value.ToString("0.###", CultureInfo.InvariantCulture));
            return this;
        }

        public AgentDemoLog Field(string name, bool value)
        {
            _line.Append(",\"").Append(name).Append("\":").Append(value ? "true" : "false");
            return this;
        }

        public void End()
        {
            _line.Append('}');
            _writer.WriteLine(_line.ToString());
            _writer.Flush();
            LineCount++;
        }

        public void Dispose() => _writer.Dispose();

        private void Escape(string value)
        {
            foreach (char c in value)
            {
                if (c == '"' || c == '\\') _line.Append('\\').Append(c);
                else if (c == '\n') _line.Append("\\n");
                else if (c < ' ') _line.Append(' ');
                else _line.Append(c);
            }
        }
    }
}
#endif
