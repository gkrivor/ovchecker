using OVChecker;

namespace OVChecker
{
    public static class OVChecksRegistry
    {
        public static void RegisterChecks()
        {
            ONNXRuntimeChecks.Register();
            CommonOpenVINOChecks.Register();
            CommonOVCChecks.Register();
            ONNXOpenVINOChecks.Register();
            TFOpenVINOChecks.Register();
            TFLiteOpenVINOChecks.Register();
        }
    }
}