namespace OVChecker
{
    static public class CommonOVCChecks
    {
        static public OVCheckCustomization SerializeCustomization = new()
        {
            Name = "Serialize OVC to IR",
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
        static private void AddCustomizations(OVCheckDescription item)
        {
            item.Customizations.Add(SerializeCustomization);
        }
        static public void Register()
        {
            AddCustomizations(OVChecksDescriptions.RegisterDescription(OVFrontends.Any, "OpenVINO OVC Convert model", "import openvino as ov\n" +
                "import openvino.tools.ovc as ovc\n" +
                "# OnBeforeCheck\n" +
                "m = ovc.convert_model(\"%MODEL_PATH%\")\n" +
                "# OnAfterCheck\n" +
                "print(\">>> Done\")"
                ));
        }
    }
}