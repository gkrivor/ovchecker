using System.Windows.Markup;

namespace OVChecker
{
    static public class TFOpenVINOChecks
    {
        static private void AddCustomizations(OVCheckDescription item)
        {
            item.Customizations.Add(CommonOpenVINOChecks.PrintVersionCustomization);
            item.Customizations.Add(CommonOpenVINOChecks.PauseBeforeCheckCustomization);
            item.Customizations.Add(CommonOpenVINOChecks.SerializeCustomization);
            item.Customizations.Add(CommonOpenVINOChecks.EnableProfilePassCustomization);
            item.Customizations.Add(CommonOpenVINOChecks.EnableVisualizeTracingCustomization);
        }
        static public void Register()
        {
            AddCustomizations(OVChecksDescriptions.RegisterDescription(OVFrontends.TF, "OpenVINO Frontend API Convert Partially", "import sys\n" +
                "import os\n" +
                "import openvino as ov\n" +
                "import openvino.frontend as of\n" +
                "mngr = of.FrontEndManager()\n" +
                "f = mngr.load_by_framework(\"tf\")\n" +
                "# OnBeforeCheck\n" +
                "l = f.load(\"%MODEL_PATH%\")\n" +
                "m = f.convert_partially(l)\n" +
                "# OnAfterCheck\n" +
                "print(\">>> Done\")"
                ));
        }
    }
}