#include <stdlib.h>
#include <iostream>
#include <fstream>
#include <string>
#include <sstream>
#include "onnx/onnx-ml.pb.h"

using namespace openvino_onnx;

#define LINE_START std::cout << std::string(indent*4, ' ')

#define PRINT_HAS_STR_FIELD(obj, field_name) \
	if (obj.has_##field_name()) \
		LINE_START << #field_name ": " << (obj.field_name().empty() ? "[empty]" : obj.field_name()) << std::endl; \
	else \
		LINE_START << "Has " #field_name ": " << obj.has_##field_name() << std::endl;
#define PRINT_HAS_INT_FIELD(obj, field_name) \
	if (obj.has_##field_name()) \
		LINE_START << #field_name ": " << obj.field_name() << std::endl; \
	else \
		LINE_START << "Has " #field_name ": " << obj.has_##field_name() << std::endl;
#define PRINT_HAS_HEX_FIELD(obj, field_name) \
	if (obj.has_##field_name()) \
		LINE_START << #field_name ": " << std::hex << obj.field_name() << std::dec << std::endl; \
	else \
		LINE_START << "Has " #field_name ": " << obj.has_##field_name() << std::endl;

std::set<std::string> known_output;

void PrintModelGraph(const GraphProto& graph, int indent) {
	LINE_START << "+++++++++++++++++++++++++++ Graph +++++++++++++++++++++++++++++++++++\n";
	PRINT_HAS_STR_FIELD(graph, doc_string);
	PRINT_HAS_STR_FIELD(graph, name);

	LINE_START << "Input size: " << graph.input_size() << std::endl;
	++indent;
	for (const auto& node : graph.input()) {
		PRINT_HAS_STR_FIELD(node, name);
		if (known_output.count(node.name()) > 0) continue;
		known_output.insert(node.name());
	}
	--indent;
	LINE_START << "Output size: " << graph.output_size() << std::endl;
	++indent;
	for (const auto& node : graph.input()) {
		PRINT_HAS_STR_FIELD(node, name);
//		if (known_output.count(node.name()) > 0) continue;
//		known_output.insert(node.name());
	}
	--indent;
#if 1
	LINE_START << "Initializer size: " << graph.initializer_size() << std::endl;
	++indent;
	for (const auto& node : graph.initializer()) {
#if 0
		PRINT_HAS_STR_FIELD(node, name);
		if (node.has_data_type()) {
			std::cout << "(" << openvino_onnx::TensorProto_DataType_Name(node.data_type()) << "), ";
		}
#endif
		if (known_output.count(node.name()) > 0) continue;
		known_output.insert(node.name());
	}
	--indent;
#endif
	LINE_START << "Quantization annotation size: " << graph.quantization_annotation_size() << std::endl;
	++indent;
	for (const auto& node : graph.quantization_annotation()) {
		PRINT_HAS_STR_FIELD(node, tensor_name);
		if (known_output.count(node.tensor_name()) > 0) continue;
		known_output.insert(node.tensor_name());
	}
	--indent;
#if 1
	std::map<std::string, std::string> value_types;
	LINE_START << "Value info size: " << graph.value_info_size() << std::endl;
	++indent;
	for (const auto& node : graph.value_info()) {
#if 0
		PRINT_HAS_STR_FIELD(node, name);
		if (node.has_type()) {
			std::cout << "(" << node.type().GetTypeName() << "), ";
		}
#endif
		std::stringstream str_type;
		str_type << node.name();
		if (node.has_type()) {
			const auto& node_type = node.type();
			if (node_type.has_denotation())				str_type << " DN";
			if (node_type.has_map_type())				str_type << " MP";
			if (node_type.has_opaque_type())			str_type << " PQ";
			if (node_type.has_optional_type())			str_type << " OP";
			if (node_type.has_sequence_type())			str_type << " SQ";
			if (node_type.has_sparse_tensor_type())		str_type << " SP";
			if (node_type.has_tensor_type())			str_type << " TN";
#if 1
			if (node_type.has_tensor_type() && node_type.tensor_type().has_shape()) {
				str_type << " [";
				for (const auto& dim : node_type.tensor_type().shape().dim()) {
					str_type << (dim.has_dim_value() ? dim.dim_value() : -123) << (dim.has_dim_param() ? dim.dim_param() : "P") << ",";
				}
				str_type << "]";
			}
#endif
		}
		else {
			str_type << "[no]";
		}
		value_types[node.name()] = str_type.str();
		LINE_START << str_type.str() << std::endl;
	}
	--indent;
#endif
#if 1
	std::vector<std::string> show_nodes{ "MatMulNBits" };

	//const auto* schema_registry = onnx::OpSchemaRegistry::Instance();
	//const auto node_op_schema = schema_registry->GetSchema(node.op_type(), opset_version, node.domain());

	LINE_START << "Node size: " << graph.node_size() << std::endl;
	for (const auto& node : graph.node()) {
		bool show_info = show_nodes.empty() || (node.has_op_type() && std::find(show_nodes.begin(), show_nodes.end(), node.op_type()) != show_nodes.end());

		if (show_info) {
			PRINT_HAS_STR_FIELD(node, name);
			PRINT_HAS_STR_FIELD(node, domain);
			PRINT_HAS_STR_FIELD(node, op_type);
		}
		++indent;
		if (show_info) {
			LINE_START << "Input size: " << node.input_size() << " [";
			for (const auto& conn : node.input()) {
				std::cout << "\"" << (known_output.count(conn) == 0 ? "!NO!" : "") << conn << "\", ";
			}
			std::cout << "]\n";
		}
		if (show_info)
			LINE_START << "Output size: " << node.output_size() << " [";
		for (const auto& conn : node.output()) {
			if (show_info)
				std::cout << "\"" << (value_types.count(conn) == 0 ? "!NO!" : value_types[conn]) << conn << "\", ";
			if (known_output.count(conn) > 0) continue;
			known_output.insert(conn);
		}
		if (show_info)
			std::cout << "]\n";
		//if (known_output.count(node.name()) > 0) {
		//	--indent;
		//	continue;
		//}
		known_output.insert(node.name());
		if (show_info)
			LINE_START << "Attributes size: " << node.attribute_size() << " [";
		for (const auto& attr : node.attribute()) {
			if (show_info) {
				if (attr.has_name())
					std::cout << "Name: " << (attr.name().empty() ? "[empty]" : attr.name());
				else
					std::cout << "Has name: " << attr.has_name();
			}
			if (attr.has_type()) {
				if (show_info) {
					std::cout << "(" << AttributeProto_AttributeType_Name(attr.type());
					switch(attr.type()) {
					case openvino_onnx::AttributeProto_AttributeType_TENSOR:
						std::cout << " - " << TensorProto_DataType_Name(attr.t().data_type());
						break;
					case openvino_onnx::AttributeProto_AttributeType_TENSORS:
							std::cout << " size: " << attr.tensors_size() << " - ";
							for (auto tensor : attr.tensors()) {
								std::cout << TensorProto_DataType_Name(tensor.data_type()) << ", ";
							}
						break;
					}
					std::cout << "), ";
				}
				if (attr.has_g()) {
					if (show_info)
						std::cout << std::endl;
					PrintModelGraph(attr.g(), indent + 1);
				}
				else if (attr.graphs_size() > 0) {
					if (show_info)
						std::cout << std::endl;
					for (const auto& g : attr.graphs()) {
						PrintModelGraph(g, indent + 1);
					}
				}
			}
			else {
				if (show_info)
					std::cout << "(NO), ";
			}
		}
		if (show_info)
			std::cout << "]\n";
		--indent;
	}
#endif
	LINE_START << "--------------------------- Graph -----------------------------------\n";
}

void PrintModelGraphUniqueNodes(const GraphProto& graph, int indent, std::set<std::string>* nodes = nullptr) {
	if (indent > 0 && nodes == nullptr) {
		LINE_START << "Wrong call of " << __FUNCTION__ << std::endl;
		return;
	}

	LINE_START << "+++++++++++++++++++++++++++ Graph +++++++++++++++++++++++++++++++++++\n";
	PRINT_HAS_STR_FIELD(graph, doc_string);
	PRINT_HAS_STR_FIELD(graph, name);

	LINE_START << "Input size: " << graph.input_size() << std::endl;
	LINE_START << "Node size: " << graph.node_size() << std::endl;

	if (indent == 0 && nodes == nullptr) {
		nodes = new std::set<std::string>();
	}

	for (const auto& node : graph.node()) {
		std::string node_name = (node.has_domain() ? node.domain() : "ai.onnx") + "." + (node.has_op_type() ? node.op_type() : "[NO_TYPE]");

		if (nodes->count(node_name) == 0) {
			nodes->insert(node_name);
		}
		for (const auto& attr : node.attribute()) {
			if (attr.has_type()) {
				if (attr.has_g()) {
					PrintModelGraphUniqueNodes(attr.g(), indent + 1, nodes);
				}
				else if (attr.graphs_size() > 0) {
					for (const auto& g : attr.graphs()) {
						PrintModelGraphUniqueNodes(g, indent + 1, nodes);
					}
				}
			}
		}
	}
	LINE_START << "--------------------------- Graph -----------------------------------\n";
	if (indent == 0) {
		LINE_START << "Found nodes:\n";
		for (auto& node_name : *nodes) {
			LINE_START << node_name << std::endl;
		}

		delete nodes;
		nodes = nullptr;
	}
}

void PrintModelInfo(const ModelProto& model) {
	int indent = 0;

	PRINT_HAS_STR_FIELD(model, domain);
	PRINT_HAS_STR_FIELD(model, doc_string);
	PRINT_HAS_INT_FIELD(model, model_version);
	PRINT_HAS_INT_FIELD(model, ir_version);
	PRINT_HAS_STR_FIELD(model, producer_name);
	PRINT_HAS_INT_FIELD(model, producer_version);

	std::cout << "Unknown fields: " << (model.unknown_fields().empty() ? "[empty]" : model.unknown_fields()) << std::endl;

	std::cout << "Functions size: " << model.functions_size() << std::endl;
	++indent;
	for (const auto& func : model.functions()) {
		PRINT_HAS_STR_FIELD(func, domain);
		PRINT_HAS_STR_FIELD(func, name);
	}
	--indent;

	std::cout << "Opset import size: " << model.opset_import_size() << std::endl;
	++indent;
	for (const auto& opset : model.opset_import()) {
		PRINT_HAS_STR_FIELD(opset, domain);
		PRINT_HAS_INT_FIELD(opset, version);
		std::cout << std::hex << opset.version() << std::endl;
	}
	--indent;

	std::cout << "Has graph: " << model.has_graph() << std::endl;
	if (model.has_graph()) {
		PrintModelGraph(model.graph(), indent);
		PrintModelGraphUniqueNodes(model.graph(), indent);
	}
}

void ReadONNXFile(const char* file_name) {
	std::ifstream model_file{ file_name, std::ios_base::binary | std::ios_base::in };

	if (!model_file.is_open() || !model_file.good()) {
		std::cout << "Cannot open file " << file_name << std::endl;
		return;
	}

	ModelProto model;
	if (!model.ParseFromIstream(&model_file)) {
		std::cout << "Cannot parse file " << file_name << std::endl;
		return;
	}

	std::cout << "Loaded model: " << file_name << std::endl;

	//onnx::shape_inference::InferShapes(model);

	PrintModelInfo(model);
}

int main(int argc, char** argv) {
	if (argc >= 2) {
		ReadONNXFile(argv[1]);
	}
	else {
        std::cout << "Usage: onnxreader.exe path/to/a/model.onnx";
	}
	return 0;
}