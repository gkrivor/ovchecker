namespace OVChecker
{
    static public class CommonOVCChecks
    {
        static public OVCheckCustomization SerializeCustomization = new()
        {
            Name = "Serialize OVC to IR",
            Group = "OpenVINO Common Debug",
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
        static private void AddCustomizations(OVCheckDescription item)
        {
            item.Customizations.Add(SerializeCustomization);
        }
        static private void AddCustomizations2(OVCheckDescription item)
        {
            item.Customizations.Add(CommonOpenVINOChecks.CompilationDeviceCustomization);
            item.Customizations.Add(SerializeCustomization);
        }
        static public void Register()
        {
            AddCustomizations(OVChecksDescriptions.RegisterDescription(OVFrontends.Any, "OpenVINO OVC Convert model", "import sys\n" +
                "import os\n" +
                "import openvino as ov\n" +
                "import openvino.tools.ovc as ovc\n" +
                "# OnBeforeCheck\n" +
                "m = ovc.convert_model(\"%MODEL_PATH%\")\n" +
                "# OnAfterCheck\n" +
                "print(\">>> Done\")"
                ));
            AddCustomizations2(OVChecksDescriptions.RegisterDescription(OVFrontends.Any, "OpenVINO OVC Convert model + Compile", "import sys\n" +
                "import os\n" +
                "import openvino as ov\n" +
                "import openvino.tools.ovc as ovc\n" +
                "ie = ov.Core()\n" +
                "# OnBeforeCheck\n" +
                "m = ovc.convert_model(\"%MODEL_PATH%\")\n" +
                "c = ie.compile_model(m, \"%DEVICE%\")\n" +
                "m = None\n" +
                "# OnAfterCheck\n" +
                "print(\">>> Done\")"
                ));
        }
    }
}