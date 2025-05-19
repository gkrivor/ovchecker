using System.Windows.Markup;
using System.Windows.Shapes;

namespace OVChecker
{
    static public class CommonOpenVINOChecks
    {
        static public OVCheckCustomization PrintVersionCustomization = new()
        {
            Name = "Print OpenVINO version",
            Group = "OpenVINO Debug",
            Value = false,
            Handler = (OVCheckCustomization source, object? value, ref string script, ref string custom_env) =>
                {
                    if (bool.Parse(value!.ToString()!) != true) return;
                    const string keyword = "# OnBeforeCheck";
                    var pos = script.IndexOf(keyword);
                    if (pos == -1) { return; }
                    script = script.Insert(pos, "print(ov.get_version())\n");
                }
        };
        static public OVCheckCustomization SerializeCustomization = new()
        {
            Name = "Serialize to IR",
            Group = "OpenVINO Debug",
            Value = "",
            Handler = (OVCheckCustomization source, object? value, ref string script, ref string custom_env) =>
            {
                if (!(value is string) || value.ToString() == "") return;
                const string keyword = "# OnAfterCheck";
                var pos = script.IndexOf(keyword);
                if (pos == -1) { return; }
                string str = "\nov.serialize(m, \"" + value + "\")\n";
                if (pos + keyword.Length < script.Length)
                    script = script.Insert(pos + keyword.Length + 1, str);
                else
                    script += str;
            }
        };
        static public OVCheckCustomization CompilationDeviceCustomization = new()
        {
            Name = "Compilation device",
            Group = "OpenVINO Compile",
            Value = "CPU",
            Handler = (OVCheckCustomization source, object? value, ref string script, ref string custom_env) =>
            {
                if (!(value is string) || value.ToString() == "") return;
                script = script.Replace("%DEVICE%", value.ToString());
            }
        };
        static public OVCheckCustomization PauseBeforeCheckCustomization = new()
        {
            Name = "Pause before OpenVINO check",
            Group = "OpenVINO Debug",
            Value = false,
            Handler = (OVCheckCustomization source, object? value, ref string script, ref string custom_env) =>
            {
                if (bool.Parse(value!.ToString()!) != true) return;
                const string keyword = "# OnBeforeCheck";
                var pos = script.IndexOf(keyword);
                if (pos == -1) { return; }
                script = script.Insert(pos, "print(\"Press Enter to continue...\")\ninput(\"\")\n");
            }
        };
        static public OVCheckCustomization EnableProfilePassCustomization = new()
        {
            Name = "Enable OpenVINO Profile Pass",
            Group = "OpenVINO Debug",
            Value = false,
            Handler = (OVCheckCustomization source, object? value, ref string script, ref string custom_env) =>
            {
                if (bool.Parse(value!.ToString()!) != true) return;
                if (!custom_env.Contains("OV_ENABLE_PROFILE_PASS"))
                    custom_env += "OV_ENABLE_PROFILE_PASS=true\n";
            }
        };
        static public OVCheckCustomization EnableVisualizeTracingCustomization = new()
        {
            Name = "Enable OpenVINO SVG Dump Pass",
            Group = "OpenVINO Debug",
            Value = false,
            Handler = (OVCheckCustomization source, object? value, ref string script, ref string custom_env) =>
            {
                if (bool.Parse(value!.ToString()!) != true) return;
                if (!custom_env.Contains("OV_ENABLE_VISUALIZE_TRACING"))
                    custom_env += "OV_ENABLE_VISUALIZE_TRACING=true\n";
            }
        };
        static public OVCheckCustomization PrintResourceConsumptionCustomization = new()
        {
            Name = "Print OpenVINO mem consumption",
            Group = "OpenVINO Debug",
            Value = false,
            Handler = (OVCheckCustomization source, object? value, ref string script, ref string custom_env) =>
            {
                if (bool.Parse(value!.ToString()!) != true) return;
                const string keyword_bc = "# OnBeforeCheck";
                var pos_bc = script.IndexOf(keyword_bc);
                if (pos_bc == -1) { return; }
                const string keyword_ac = "# OnAfterCheck";
                var pos_ac = script.IndexOf(keyword_ac);
                if (pos_ac == -1) { return; }

                string script_end = script.Substring(pos_ac);
                string[] check_lines = script.Substring(pos_bc + keyword_bc.Length, pos_ac - pos_bc - keyword_bc.Length).Split("\n");
                script = script.Remove(pos_bc + keyword_bc.Length);
                if (!script.EndsWith("\n")) script += "\n";

                script = script.Insert(pos_bc, "if not \"psutil\" in sys.modules: import psutil\n" +
                    "mem_proc = psutil.Process(os.getpid())\n" +
                    "mem_1st = mem_proc.memory_info().rss\n" +
                    "mem_prev = mem_1st\n" +
                    "print(f\"{'Line':<67}|{'Change':<16}|{'Total':<16}\")\n" +
                    "def mem_print(line):\n" +
                    "  global mem_proc, mem_1st, mem_prev, mem_last\n" +
                    "  mem_last = mem_proc.memory_info().rss\n" +
                    "  print(f\"{line:<67}|{mem_last - mem_prev:<16,}|{mem_last - mem_1st:<16,}\")\n" +
                    "  mem_prev = mem_last\n");

                foreach (string line in check_lines)
                {
                    string safe_line = line.Trim().Replace("\"", "\\\"");
                    if (safe_line.Length == 0 || safe_line.Contains("no-check")) continue;
                    if (safe_line.Length > 64) safe_line = safe_line.Substring(0, 64) + "...";
                    script += line.EndsWith("\n") ? line : line + "\n";
                    script += "mem_print(\"" + safe_line + "\") # no-check\n";
                }

                script += script_end;
            }
        };
        static public OVCheckCustomization EnableMatcherLoggingCustomization = new()
        {
            Name = "Enable OpenVINO Matcher Logging",
            Group = "OpenVINO Debug",
            Value = false,
            Handler = (OVCheckCustomization source, object? value, ref string script, ref string custom_env) =>
            {
                if (bool.Parse(value!.ToString()!) != true) return;
                if (!custom_env.Contains("OV_MATCHER_LOGGING"))
                    custom_env += "OV_MATCHER_LOGGING=true\n";
            }
        };
        static public OVCheckCustomization SpecifyMatcherLoggingCustomization = new()
        {
            Name = "Specify Matcher Logging Transformation",
            Group = "OpenVINO Debug",
            Value = "",
            Handler = (OVCheckCustomization source, object? value, ref string script, ref string custom_env) =>
            {
                if (!(value is string) || value.ToString() == "") return;
                if (!custom_env.Contains("OV_MATCHERS_TO_LOG"))
                    custom_env += "OV_MATCHERS_TO_LOG=" + value!.ToString() + "\n";
            }
        };
        static public OVCheckCustomization EnableTransformationsVerboseLoggingCustomization = new()
        {
            Name = "Enable Transformations Verbose Logging",
            Group = "OpenVINO Debug",
            Value = false,
            Handler = (OVCheckCustomization source, object? value, ref string script, ref string custom_env) =>
            {
                if (bool.Parse(value!.ToString()!) != true) return;
                if (!custom_env.Contains("OV_VERBOSE_LOGGING"))
                    custom_env += "OV_VERBOSE_LOGGING=true\n";
            }
        };
        static public OVCheckCustomization EnableGPUDumpMemoryPoolCustomization = new()
        {
            Name = "Enable GPU Dump Memory Pool",
            Group = "OpenVINO Debug",
            Value = false,
            Handler = (OVCheckCustomization source, object? value, ref string script, ref string custom_env) =>
            {
                if (bool.Parse(value!.ToString()!) != true) return;
                if (!custom_env.Contains("OV_VERBOSE"))
                    custom_env += "OV_VERBOSE=4\n";
                if (!custom_env.Contains("OV_GPU_DUMP_MEMORY_POOL"))
                    custom_env += "OV_GPU_DUMP_MEMORY_POOL=1\n";
            }
        };
        static private void AddCustomizations(OVCheckDescription item)
        {
            item.Customizations.Add(PrintVersionCustomization);
            item.Customizations.Add(PauseBeforeCheckCustomization);
            if (!item.Requirements.Contains("psutil")) item.Requirements += "psutil";
            item.Customizations.Add(PrintResourceConsumptionCustomization);
            item.Customizations.Add(EnableGPUDumpMemoryPoolCustomization);
            item.Customizations.Add(SerializeCustomization);
            item.Customizations.Add(EnableProfilePassCustomization);
            item.Customizations.Add(EnableVisualizeTracingCustomization);
            item.Customizations.Add(EnableMatcherLoggingCustomization);
            item.Customizations.Add(SpecifyMatcherLoggingCustomization);
            item.Customizations.Add(EnableTransformationsVerboseLoggingCustomization);
        }
        static private void AddCustomizations2(OVCheckDescription item)
        {
            item.Customizations.Add(PrintVersionCustomization);
            item.Customizations.Add(PauseBeforeCheckCustomization);
            if (!item.Requirements.Contains("psutil")) item.Requirements += "psutil";
            item.Customizations.Add(PrintResourceConsumptionCustomization);
            item.Customizations.Add(EnableGPUDumpMemoryPoolCustomization);
            item.Customizations.Add(SerializeCustomization);
            item.Customizations.Add(CompilationDeviceCustomization);
            item.Customizations.Add(EnableProfilePassCustomization);
            item.Customizations.Add(EnableVisualizeTracingCustomization);
            item.Customizations.Add(EnableMatcherLoggingCustomization);
            item.Customizations.Add(SpecifyMatcherLoggingCustomization);
            item.Customizations.Add(EnableTransformationsVerboseLoggingCustomization);
        }
        static public void Register()
        {
            AddCustomizations(OVChecksDescriptions.RegisterDescription(OVFrontends.Any, "OpenVINO Read model", "import sys\n" +
                "import os\n" +
                "import openvino as ov\n" +
                "ie = ov.Core()\n" +
                "# OnBeforeCheck\n" +
                "m = ie.read_model(\"%MODEL_PATH%\")\n" +
                "# OnAfterCheck\n" +
                "print(\">>> Done\")"
                ));
            AddCustomizations2(OVChecksDescriptions.RegisterDescription(OVFrontends.Any, "OpenVINO Read model + Compile", "import sys\n" +
                "import os\n" +
                "import openvino as ov\n" +
                "ie = ov.Core()\n" +
                "# OnBeforeCheck\n" +
                "m = ie.read_model(\"%MODEL_PATH%\")\n" +
                "c = ie.compile_model(m, \"%DEVICE%\")\n" +
                "# OnAfterCheck\n" +
                "print(\">>> Done\")"
                ));
            AddCustomizations(OVChecksDescriptions.RegisterDescription(OVFrontends.Any, "OpenVINO Convert model", "import sys\n" +
                "import os\n" +
                "import openvino as ov\n" +
                "# OnBeforeCheck\n" +
                "m = ov.convert_model(\"%MODEL_PATH%\")\n" +
                "# OnAfterCheck\n" +
                "print(\">>> Done\")"
                ));
        }
    }
}