namespace OVChecker
{
    static public class ONNXRuntimeChecks
    {
        static private void AddCustomizations(OVCheckDescription item)
        {
            item.Customizations.Add(CommonOpenVINOChecks.CompilationDeviceCustomization);
        }
        static public void Register()
        {
            OVChecksDescriptions.RegisterDescription(OVFrontends.ONNX, "ONNXRuntime Load model", "import sys\n" +
                "from onnxruntime import InferenceSession\n" +
                "sess = InferenceSession(\"%MODEL_PATH%\")\n" +
                "print(\">>> Done\")", "onnxruntime");
            AddCustomizations(OVChecksDescriptions.RegisterDescription(OVFrontends.ONNX, "ONNXRuntime vs OV output", "import sys\n" +
                "import numpy as np\n" +
                "from onnxruntime import InferenceSession\n" +
                "import openvino as ov\n" +
                "m_path = \"%MODEL_PATH%\"\n" +
                "sess = InferenceSession(m_path)\n" +
                "ie = ov.Core()\n" +
                "m = ie.read_model(m_path)\n" +
                "c = ie.compile_model(m, \"%DEVICE%\")\n" +
                "ort_inputs = sess.get_inputs()\n" +
                "ov_inputs = c.inputs\n" +
                "if (len(ort_inputs) != len(c.inputs)): raise Exception(f\"Misalignment in inputs ONNXRuntime/OpenVINO: {len(ort_inputs)}/{len(ov_inputs)}\")\n" +
                "ort_outputs = sess.get_outputs()\n" +
                "ov_outputs = c.outputs\n" +
                "if (len(ort_outputs) != len(c.outputs)): raise Exception(f\"Misalignment in outputs ONNXRuntime/OpenVINO: {len(ort_outputs)}/{len(ov_outputs)}\")\n" +
                "ov_inames = []\n" +
                "ov_onames = []\n" +
                "for item in ov_inputs: ov_inames.extend(item.names)\n" +
                "for item in ov_outputs: ov_onames.extend(item.names)\n" +
                "ort_inames = [item.name for item in ort_inputs]\n" +
                "ort_onames = [item.name for item in ort_outputs]\n" +
                "input_data = { }\n" +
                "for i, item in enumerate(ort_inames):\n" +
                "    if not item in ov_inames: raise Exception(f\"OpenVINO doesn't have input named {item}\")\n" +
                "    if not item in ov_inputs[i].names: raise Exception(f\"OpenVINO has a wrong inputs order near {item}\")\n" +
                "    if ov_inputs[i].get_partial_shape().is_static and ort_inputs[i].shape != ov_inputs[i].shape: raise Exception(f\"Misalignment in shapes for {item} ONNXRuntime/OpenVINO: {ort_inputs[i].shape}/{ov_inputs[i].shape}\")\n" +
                "    shape = [1 if dim == -1 else dim.min_length for dim in ov_inputs[i].get_partial_shape()]\n" +
                "    input_data[item] = np.random.randn(*shape).astype(np.dtype(ov_inputs[i].get_element_type().to_dtype()))\n" +
                "for i, item in enumerate(ort_onames):\n" +
                "    if not item in ov_onames: raise Exception(f\"OpenVINO doesn't have output named {item}\")\n" +
                "    if not item in ov_outputs[i].names: raise Exception(f\"OpenVINO has a wrong outputs order near {item}\")\n" +
                "    if ov_outputs[i].get_partial_shape().is_static and ort_outputs[i].shape != ov_outputs[i].shape: raise Exception(f\"Misalignment in shapes for {item} ONNXRuntime/OpenVINO: {ort_outputs[i].shape}/{ov_outputs[i].shape}\")\n" +
                "ort_results = sess.run(ort_onames, input_data)\n" +
                "i = c.create_infer_request()\n" +
                "ov_results = i.infer(input_data)\n" +
                "is_failed = False\n" +
                "for i, item in enumerate(ort_results):\n" +
                "    diff = np.abs(ort_results[i] - ov_results[i]).max()\n" +
                "    print(f\"\\\"{ort_onames[i]}\\\" abs diff is {diff}\")\n" +
                "    is_failed |= diff > 0.00001\n" +
                "if is_failed == True: raise Exception(\"Diff for one of outputs more than 0.00001\")\n" +
                "print(\">>> Done\")", "onnxruntime"));
        }
    }
}