namespace OVChecker
{
    static public class ONNXRuntimeChecks
    {
        static public void Register()
        {
            OVChecksDescriptions.RegisterDescription(OVFrontends.ONNX, "ONNXRuntime Load model", "from onnxruntime import InferenceSession\n" +
                "sess = InferenceSession(\"%MODEL_PATH%\")\n" +
                "print(\">>> Done\")", "onnx onnxruntime");
        }
    }
}