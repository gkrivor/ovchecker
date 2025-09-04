import sys

m_path = "%MODEL_PATH%"

if m_path == "%MODEL_PATH%" and len(sys.argv) < 2:
    print("Usage: script.py path/to/a/model.onnx")
    sys.exit(-1)
else:
    m_path = sys.argv[1]

import numpy as np
from onnxruntime import InferenceSession
import onnx
from onnx.helper import ValueInfoProto, np_dtype_to_tensor_dtype
import openvino as ov

sess = InferenceSession(m_path)
ie = ov.Core()
m = ie.read_model(m_path)
c = ie.compile_model(m, "CPU")
ort_inputs = sess.get_inputs()
ov_inputs = c.inputs
if (len(ort_inputs) != len(c.inputs)): raise Exception(f"Misalignment in inputs ONNXRuntime/OpenVINO: {len(ort_inputs)}/{len(ov_inputs)}")
ort_outputs = sess.get_outputs()
ov_outputs = c.outputs
if (len(ort_outputs) != len(c.outputs)): raise Exception(f"Misalignment in outputs ONNXRuntime/OpenVINO: {len(ort_outputs)}/{len(ov_outputs)}")
ov_inames = []
ov_onames = []
for item in ov_inputs: ov_inames.extend(item.names)
for item in ov_outputs: ov_onames.extend(item.names)
ort_inames = [item.name for item in ort_inputs]
ort_onames = [item.name for item in ort_outputs]
input_data = { }
for i, item in enumerate(ort_inames):
    if not item in ov_inames: raise Exception(f"OpenVINO doesn't have input named {item}")
    if not item in ov_inputs[i].names: raise Exception(f"OpenVINO has a wrong inputs order near {item}")
    if ov_inputs[i].get_partial_shape().is_static and ort_inputs[i].shape != ov_inputs[i].shape: raise Exception(f"Misalignment in shapes for {item} ONNXRuntime/OpenVINO: {ort_inputs[i].shape}/{ov_inputs[i].shape}")
    shape = [1 if dim == -1 else dim.min_length for dim in ov_inputs[i].get_partial_shape()]
    input_data[item] = np.random.randn(*shape).astype(np.dtype(ov_inputs[i].get_element_type().to_dtype()))
sess = None
c = None
m = None
ie = None

onnx_model = onnx.load(m_path)
total_nodes = len(onnx_model.graph.node)

def serialize_ov(cmd, ov_model):
    data = cmd.split(" ")
    if len(data) != 2: raise Exception("Serialize command should have a filename without spaces")
    ov.serialize(ov_model, data[1])
    print("Serialization done")

def goto_node(cmd):
    global total_nodes
    data = cmd.split(" ")
    if len(data) != 2: raise Exception("Goto command should have an index")
    try:
        idx = int(data[1])
        if idx < -1 or idx >= total_nodes:
            raise Exception(f"Node index is out of range [0..{total_nodes})")
        return idx
    except:
        raise Exception("Wrond index, index must be a number")
        
def dump_node(cmd, node, ort_onames, ort_results):
    global onnx_model
    data = cmd.split(" ")
    if len(data) != 2: raise Exception("Dump command should have a filename without spaces")
    with open(data[1], "w") as f:
        print("ir_version: " + str(onnx_model.ir_version), file=f)
        print("producer_name: \"OVChecker\"", file=f)
        print("producer_version: \"1.0\"", file=f)
        print("model_version: 1", file=f)
        print("graph {", file=f)
        print("  name: \"" + onnx_model.graph.name.replace("\"", "\\\"") + "\"", file=f)
        print("  node {", file=f)
        for line in str(node).splitlines():
            print(f"    {line}",  file=f)
        print("  }", file=f)
        for i in node.input:
            print("  initializer {", file=f)
            print("    name: \"" + i + "\"", file=f)
            ot = ort_results[ort_onames.index(i)]
            for dim in np.shape(ot):
                print("    dims: " + str(dim), file=f)
            print("    data_type: " + str(np_dtype_to_tensor_dtype(ot.dtype)), file=f)
            print("    raw_data: \"", end="", file=f)
            raw_data = ot.tobytes()
            block =""
            row_size = ot.dtype.itemsize * np.shape(ot)[-1]
            for j in range(0, len(raw_data)):
                block += f"\\x{raw_data[j]:02x}"
                if (j + 1) % row_size == 0:
                    print(block, end="", file=f)
#                    print("    raw_data: \"" + block + "\"", file=f)
                    block = ""
            if block != "":
                print("    raw_data: \"" + block + "\"", file=f)
            print("\"", file=f)
            print("  }", file=f)
        for o in node.output:
            print("  output {", file=f)
            print("    name: \"" + o + "\"", file=f)
            print("    type {", file=f)
            print("      tensor_type {", file=f)
            print("        elem_type: " + str(np_dtype_to_tensor_dtype(ot.dtype)), file=f)
#            print("        shape {", file=f)
#            ot = ort_results[ort_onames.index(i)]
#            for dim in np.shape(ot):
#                print("          dim { dim_value: " + str(dim) + " }", file=f)
#            print("        }", file=f)
            print("      }", file=f)
            print("    }", file=f)
            print("  }", file=f)
        print("}", file=f)
        for opset in onnx_model.opset_import:
            print("opset_import {", file=f)
            print("  version: " + str(opset.version), file=f)
            print("}", file=f)
        print("Dump has been prepared")
    with open(data[1], "r") as f:
        try:
            from google.protobuf import text_format
            proto = onnx.ModelProto()
            text_format.Parse(f.read(), proto, allow_field_number=True)
            s = onnx.serialization.registry.get("protobuf").serialize_proto(proto)
            onnx._save_bytes(s, data[1] + ".onnx")
            #proto = onnx.serialization.registry.get("protobuf").serialize_proto(proto, onnx.ModelProto())
            print("Prototxt has been converted to binary onnx")
        except Exception as ex:
            print(ex)
    

help_str = """Interactive debugging console
Available commands:
dump filename.prototxt - dumps current node into a prototxt file
goto index - jump to node with index
next - continue execution
serialize filename.xml - serialize OpenVINO model to filename.xml
break - break execution and exit
help - print this help

If you enter a wrong command - you get a message
"""

def check_node(node_name):
    global m_path, onnx_model, input_data, node_idx
    node = None
    using_inputs = []
    for n in onnx_model.graph.node:
        if n.name == node_name:
            node = n
        for i in n.input:
            using_inputs.append(i)
    if node is None: raise Exception(f"Node with name {node_name} isn't found in the graph")
    new_outs = []
    check_outs = [] # We don't need to check inputs, only layer outputs
    for o in node.output:
        if not o in using_inputs:
            print(f"{o} isn't connected, skipped")
            continue
        check_outs.append(o)
        new_outs.append(o)
        val_nfo = ValueInfoProto()
        val_nfo.name = o
        onnx_model.graph.output.append(val_nfo)
    for i in node.input:
        if i in new_outs: continue
        new_outs.append(i)
        val_nfo = ValueInfoProto()
        val_nfo.name = i
        onnx_model.graph.output.append(val_nfo)
    rem_out = []
    for o in onnx_model.graph.output:
        if not o.name in new_outs and not o in rem_out:
            rem_out.append(o)
    for o in rem_out:
        onnx_model.graph.output.remove(o)
    onnx.save(onnx_model, "onnxruntime_vs_ov_output.onnx")
    sess = InferenceSession("onnxruntime_vs_ov_output.onnx")
    ie = ov.Core()
    m = ie.read_model("onnxruntime_vs_ov_output.onnx")
    c = ie.compile_model(m, "CPU")
    ort_inputs = sess.get_inputs()
    ov_inputs = c.inputs
    if (len(ort_inputs) != len(c.inputs)): raise Exception(f"Misalignment in inputs ONNXRuntime/OpenVINO: {len(ort_inputs)}/{len(ov_inputs)}")
    ort_outputs = sess.get_outputs()
    ov_outputs = c.outputs
    if (len(ort_outputs) != len(c.outputs)): raise Exception(f"Misalignment in outputs ONNXRuntime/OpenVINO: {len(ort_outputs)}/{len(ov_outputs)}")
    ov_inames = []
    ov_onames = []
    for item in ov_inputs: ov_inames.extend(item.names)
    for item in ov_outputs: ov_onames.extend(item.names)
    ort_inames = [item.name for item in ort_inputs]
    ort_onames = [item.name for item in ort_outputs]
    for i, item in enumerate(ort_inames):
        if not item in ov_inames: raise Exception(f"OpenVINO doesn't have input named {item}")
        if not item in ov_inputs[i].names: raise Exception(f"OpenVINO has a wrong inputs order near {item}")
    for i, item in enumerate(ort_onames):
        if not item in check_outs: continue
        if not item in ov_onames: raise Exception(f"OpenVINO doesn't have output named {item}")
        if not item in ov_outputs[i].names: raise Exception(f"OpenVINO has a wrong outputs order near {item}")
    ort_results = sess.run(ort_onames, input_data)
    i = c.create_infer_request()
    ov_results = i.infer(input_data)
    is_failed = False
    for i, item in enumerate(ort_results):
        if not ort_outputs[i].name in check_outs: continue
        if ort_results[i].dtype != np.bool:
            diff = np.abs(ort_results[i] - ov_results[i]).max()
            if diff > 0:
                print(f"Output \"{ort_onames[i]}\" abs diff is {diff}")
            if diff > 0.00001:
                print("Analysis...", end="")
                sys.stdout.flush()
                ort_iter = np.nditer(ort_results[i])
                ov_iter = np.nditer(ov_results[i])
                stat = {'min':np.inf, 'max':-np.inf, 'vals':[], 'info': []}
                idx = 0
                try:
                    while(True):
                        ort_val = next(ort_iter)
                        ov_val = next(ov_iter)
                        diff = abs(ort_val - ov_val)
                        if diff > 0.00001:
                            if stat['min'] > diff: stat['min'] = diff
                            if stat['max'] < diff: stat['max'] = diff
                            inserted = False
                            for val_idx, val in enumerate(stat['vals']):
                                if diff > val:
                                    stat['vals'].insert(val_idx, diff)
                                    stat['info'].insert(val_idx, f"[{idx}] {ort_val:<20} ~ {ov_val:<20} = {diff:.15f}")
                                    inserted = True
                                    while len(stat['vals']) > 30:
                                        stat['vals'].pop()
                                        stat['info'].pop()
                                    break
                            if not inserted and len(stat['vals']) < 30:
                                    stat['vals'].append(diff)
                                    stat['info'].append(f"[{idx}] {ort_val:<20} ~ {ov_val:<20} = {diff:.15f}")
                        idx += 1
                        if (idx % 100000) == 0:
                            print('.', end='')
                            sys.stdout.flush()
                except:
                    pass
                print(f"\nMin diff: {stat['min']:<20} Max diff: {stat['max']:<20}")
                for item in stat['info']:
                    print(item)
                is_failed |= True
        else:
            diff = ort_results == ov_results
            if np.all(diff) == True:
                print(f"\"{ort_onames[i]}\" has different results {diff}")
                is_failed |= True
    if is_failed == True:
        print("Diff for one of outputs more than 0.00001")
        print(help_str)
        cmd_list = ['help', 'break', 'next', 'serialize', 'dump', 'goto']
        while(True):
            usr = input("cmd> ")
            if len(usr) <= 0: continue
            cmd = usr.split(" ")
            cmd = [c for c in cmd_list if c.startswith(cmd[0])]
            if len(cmd) == 0:
                print(f"Command \"{usr}\" not found")
                continue
            elif len(cmd) > 1:
                print(f"Command \"{usr}\" is ambiguous: {', '.join(cmd)}")
                continue
            cmd = cmd[0]
            if cmd == "help":
                print(help_str)
                continue
            elif cmd == "break": raise Exception("Execution stopped by user")
            elif cmd == "next": break
            try:
                if cmd == "serialize": serialize_ov(usr, m)
                elif cmd == "goto": node_idx = goto_node(usr)
                elif cmd == "dump": dump_node(usr, node, ort_onames, ort_results)
                else: raise Exception(f"\"{usr}\" is a wrong command")
                continue
            except Exception as ex:
                print(f"Error: {ex=}")
    print("OK")

def get_arg_value(arg_name, default_value, type_id = None):
    try:
        idx = sys.argv.index(arg_name) + 1
        if idx == len(sys.argv): return default_value
        if not type_id is None: return type_id(sys.argv[idx])
        return sys.argv[idx]
    except:
        return default_value

node_idx = get_arg_value("--goto", 0, int)
if node_idx >= total_nodes: raise Exception(f"Node index is out of range [0..{total_nodes})")
while(node_idx < total_nodes):
    node = onnx_model.graph.node[node_idx]
    print(f"{node_idx}/{total_nodes} Checking node {node.name}... ")
    check_node(node.name)
    node_idx += 1

print(">>> Done")