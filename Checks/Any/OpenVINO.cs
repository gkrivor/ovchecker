using System.Windows.Markup;

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

                script = script.Insert(pos_bc, "if (not \"os\" in sys.modules) or (not \"os\" in dir()): import os\n" +
                    "if not \"psutil\" in sys.modules: import psutil\n" +
                    "mem_proc = psutil.Process(os.getpid())\n" +
                    "mem_before = mem_proc.memory_info().rss\n");

                string str = "\nmem_after = mem_proc.memory_info().rss\n" +
                    "print(f\"Memory consumption: {mem_after - mem_before:,}\")\n";
                pos_ac = script.IndexOf(keyword_ac);
                if (pos_ac + keyword_ac.Length < script.Length)
                    script = script.Insert(pos_ac + keyword_ac.Length + 1, str);
                else
                    script += str;
            }
        };
        static private void AddCustomizations(OVCheckDescription item)
        {
            item.Customizations.Add(PrintVersionCustomization);
            item.Customizations.Add(PauseBeforeCheckCustomization);
            if (!item.Requirements.Contains("psutil")) item.Requirements += "psutil";
            item.Customizations.Add(PrintResourceConsumptionCustomization);
            item.Customizations.Add(SerializeCustomization);
            item.Customizations.Add(EnableProfilePassCustomization);
            item.Customizations.Add(EnableVisualizeTracingCustomization);
        }
        static private void AddCustomizations2(OVCheckDescription item)
        {
            item.Customizations.Add(PrintVersionCustomization);
            item.Customizations.Add(PauseBeforeCheckCustomization);
            if (!item.Requirements.Contains("psutil")) item.Requirements += "psutil";
            item.Customizations.Add(PrintResourceConsumptionCustomization);
            item.Customizations.Add(SerializeCustomization);
            item.Customizations.Add(CompilationDeviceCustomization);
            item.Customizations.Add(EnableProfilePassCustomization);
            item.Customizations.Add(EnableVisualizeTracingCustomization);
        }
        static public void Register()
        {
            AddCustomizations(OVChecksDescriptions.RegisterDescription(OVFrontends.Any, "OpenVINO Read model", "import sys\n" +
                "import openvino as ov\n" +
                "ie = ov.Core()\n" +
                "# OnBeforeCheck\n" +
                "m = ie.read_model(\"%MODEL_PATH%\")\n" +
                "# OnAfterCheck\n" +
                "print(\">>> Done\")"
                ));
            AddCustomizations2(OVChecksDescriptions.RegisterDescription(OVFrontends.Any, "OpenVINO Read model + Compile", "import sys\n" +
                "import openvino as ov\n" +
                "ie = ov.Core()\n" +
                "# OnBeforeCheck\n" +
                "m = ie.read_model(\"%MODEL_PATH%\")\n" +
                "c = ie.compile_model(m, \"%DEVICE%\")\n" +
                "# OnAfterCheck\n" +
                "print(\">>> Done\")"
                ));
            AddCustomizations(OVChecksDescriptions.RegisterDescription(OVFrontends.Any, "OpenVINO Convert model", "import sys\n" +
                "import openvino as ov\n" +
                "# OnBeforeCheck\n" +
                "m = ov.convert_model(\"%MODEL_PATH%\")\n" +
                "# OnAfterCheck\n" +
                "print(\">>> Done\")"
                ));
        }
    }
}