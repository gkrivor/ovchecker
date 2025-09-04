#include <stdlib.h>
#include <iostream>
#include <fstream>
#include "schema_generated.h"
/*
template <typename T>
void GetIndicesVector(std::vector<uint8_t>* indices,
	const int num_indices,
	std::vector<std::vector<T>>* indices_vector) {
	// Note because TfLite will reverse the dimensions, so pad zeros upfront.
	switch (NumDimensions(indices)) {
	case 0:
	case 1: {
		const auto indices_data = GetTensorData<T>(indices);
		for (int i = 0; i < num_indices; ++i) {
			std::vector<T> index({ 0, 0, 0, indices_data[i] });
			indices_vector->push_back(index);
		}
		break;
	}
	case 2: {
		const int true_dimensions = SizeOfDimension(indices, 1);
		TF_LITE_ENSURE(context, true_dimensions <= kMaxDimensions);
		for (int i = 0; i < num_indices; ++i) {
			std::vector<T> index;
			index.reserve(kMaxDimensions);
			// Fill the index with 1 up to kMaxDimensions - true_dimensions to
			// satisfy the needs for 4-dimension index.
			for (int j = 0; j < kMaxDimensions - true_dimensions; ++j) {
				index.push_back(0);
			}
			for (int j = 0; j < true_dimensions; ++j) {
				index.push_back(GetTensorData<T>(indices)[i * true_dimensions + j]);
			}

			indices_vector->push_back(index);
		}
		break;
	}
	default:
		std::cout << "Failed\n";
		return;
	}
	return;
}
*/
float float32(uint16_t float16_value)
{
	// MSB -> LSB
	// float16=1bit: sign, 5bit: exponent, 10bit: fraction
	// float32=1bit: sign, 8bit: exponent, 23bit: fraction
	// for normal exponent(1 to 0x1e): value=2**(exponent-15)*(1.fraction)
	// for denormalized exponent(0): value=2**-14*(0.fraction)
	uint32_t sign = float16_value >> 15;
	uint32_t exponent = (float16_value >> 10) & 0x1F;
	uint32_t fraction = (float16_value & 0x3FF);
	uint32_t float32_value;
	if (exponent == 0)
	{
		if (fraction == 0)
		{
			// zero
			float32_value = (sign << 31);
		}
		else
		{
			// can be represented as ordinary value in float32
			// 2 ** -14 * 0.0101
			// => 2 ** -16 * 1.0100
			// int int_exponent = -14;
			exponent = 127 - 14;
			while ((fraction & (1 << 10)) == 0)
			{
				//int_exponent--;
				exponent--;
				fraction <<= 1;
			}
			fraction &= 0x3FF;
			// int_exponent += 127;
			float32_value = (sign << 31) | (exponent << 23) | (fraction << 13);
		}
	}
	else if (exponent == 0x1F)
	{
		/* Inf or NaN */
		float32_value = (sign << 31) | (0xFF << 23) | (fraction << 13);
	}
	else
	{
		/* ordinary number */
		float32_value = (sign << 31) | ((exponent + (127 - 15)) << 23) | (fraction << 13);
	}

	return *((float*)&float32_value);
}

template <typename T, typename U>
static void read_sparse_data(uint8_t* dest,
	uint8_t* dest_end,
	const uint8_t* values,
	const size_t row_size,
	const size_t element_size,
	const ::flatbuffers::Vector<T>* indices,
	const ::flatbuffers::Vector<U>* segments) {
	uint8_t* data = dest - row_size;  // row size will be increased at first step
	T last_idx = ~static_cast<T>(0);
	for (auto idx = indices->begin(); idx != indices->end(); ++idx) {
		if (*idx <= last_idx) {
			data += row_size;
		}
		if (data + *idx >= dest_end) {
			std::cout << "Dense data is out of bounds\n";
			return;
		}
		memcpy(static_cast<uint8_t*>(static_cast<void*>(data)) + *idx * element_size, values + *idx * element_size, element_size);
		last_idx = *idx;
	}
}

bool ReadTFLiteTensor(const uint32_t idx, const tflite::Model* model, const tflite::SubGraph* subgraph, std::set<uint32_t>& printed_tensors)
{
	if (idx == ~static_cast<uint32_t>(0)) {
		std::cout << "INDEX IS WRONG!\n";
		return false;
	}
	const tflite::Tensor* it = subgraph->tensors()->Get(idx);
	std::cout << "Tensor Name: " << it->name()->c_str()
		<< " Type: " << tflite::EnumNameTensorType(it->type())
		<< std::endl
		<< " Rank: " << (it->has_rank() ? "yes" : "no")
		<< " Var: " << (it->is_variable() ? "yes" : "no")
		<< std::endl
		<< "Shape [";
	size_t shape_size = 1;
	for (auto dim : *it->shape()) {
		std::cout << dim << ",";
		shape_size *= dim;
	}
	std::cout << "] size: " << shape_size << std::endl;
	if (it->shape_signature() != nullptr) {
		std::cout << "Shape signature [";
		for (auto dim : *it->shape_signature())
			std::cout << dim << ",";
		std::cout << "]"
			<< std::endl;
	}
	auto buffer = model->buffers()->Get(it->buffer());
#if 0
	std::cout << "Buffer: #" << it->buffer();
	if (buffer && buffer->data()) {
		std::cout << " size: " << buffer->data()->size() << " [\n";
		for (auto item : *buffer->data()) {
			std::cout << std::hex << static_cast<int32_t>(item) << ",";
		}
	}
	std::cout << std::endl;
#endif
#if 1
	if (it->quantization() != nullptr) {
		std::cout << "Quantization AVAILABLE, type: " << tflite::EnumNameQuantizationDetails(it->quantization()->details_type()) << " dim " << it->quantization()->quantized_dimension() << " ";
		if (it->quantization()->min() != nullptr)
		{
			std::cout << "min: [";
			for (auto m : *it->quantization()->min())
				std::cout << m << ",";
			std::cout << "] ";
		}
		if (it->quantization()->max() != nullptr)
		{
			std::cout << "max: [";
			for (auto m : *it->quantization()->max())
				std::cout << m << ",";
			std::cout << "] ";
		}
		if (it->quantization()->scale() != nullptr)
		{
			std::cout << "\nscale: [";
			//for (auto m : *it->quantization()->scale())
			//	std::cout << m << ",";
			std::cout << "] ";
		}
		if (it->quantization()->zero_point() != nullptr)
		{
			std::cout << "\nzero_point: [";
			//for (auto m : *it->quantization()->zero_point())
			//	std::cout << m << ",";
			std::cout << "] ";
		}
		std::cout << std::endl;
	}
#endif
	if (it->variant_tensors()) {
		std::cout << "Variant Tensors AVAILABLE\n";
	}
#if 1
	if (it->sparsity() != nullptr)
	{
		if (it->sparsity()->traversal_order() != nullptr)
		{
			std::cout << "Traversal order: [";
			for (auto m = it->sparsity()->traversal_order()->begin(); m != it->sparsity()->traversal_order()->end(); ++m)
				std::cout << *m << ",";
			std::cout << "]"
				<< std::endl;

		}
		if (it->sparsity()->block_map() != nullptr)
		{
			std::cout << " Block map: [";
			for (auto m = it->sparsity()->block_map()->begin(); m != it->sparsity()->block_map()->end(); ++m)
				std::cout << *m << ",";
			std::cout << "]"
				<< std::endl;
		}
		if (it->sparsity()->dim_metadata() != nullptr)
		{
			std::cout << " Dim: [";
			for (auto m = it->sparsity()->dim_metadata()->begin(); m != it->sparsity()->dim_metadata()->end(); ++m)
			{
				std::cout << tflite::EnumNameDimensionType(m->format())
					<< " - " << m->dense_size()
					<< " (" << tflite::EnumNameSparseIndexVector(m->array_segments_type())
					<< " / " << tflite::EnumNameSparseIndexVector(m->array_indices_type())
					<< ")";
				if (m->format() == tflite::DimensionType_SPARSE_CSR) {
					std::vector<int32_t> segments;
					switch (m->array_segments_type()) {
					case tflite::SparseIndexVector_Uint8Vector: {
						auto array = m->array_segments_as_Uint8Vector()->values();
						for (auto val : *array) {
							segments.push_back(static_cast<int32_t>(val));
						}
						break;
					}
					case tflite::SparseIndexVector_Uint16Vector: {
						auto array = m->array_segments_as_Uint16Vector()->values();
						for (auto val : *array) {
							segments.push_back(static_cast<int32_t>(val));
						}
						break;
					}
					case tflite::SparseIndexVector_Int32Vector: {
						auto array = m->array_segments_as_Int32Vector()->values();
						for (auto val : *array) {
							segments.push_back(static_cast<int32_t>(val));
						}
						break;
					}
					}
					std::cout << "\nSegments: #" << segments.size() << " [ ";
					for (auto segment : segments) {
						std::cout << std::hex << static_cast<int32_t>(segment) << ",";
					}
					std::vector<int32_t> indices;
					switch (m->array_indices_type()) {
					case tflite::SparseIndexVector_Uint8Vector: {
						auto array = m->array_indices_as_Uint8Vector()->values();
						for (auto val : *array) {
							indices.push_back(static_cast<int32_t>(val));
						}
						break;
					}
					case tflite::SparseIndexVector_Uint16Vector: {
						auto array = m->array_indices_as_Uint16Vector()->values();
						for (auto val : *array) {
							indices.push_back(static_cast<int32_t>(val));
						}
						break;
					}
					case tflite::SparseIndexVector_Int32Vector: {
						auto array = m->array_indices_as_Int32Vector()->values();
						for (auto val : *array) {
							indices.push_back(static_cast<int32_t>(val));
						}
						break;
					}
					}
					std::cout << " ]\nIndices: #" << indices.size() << " [ ";
					size_t idx = 0;
					int32_t last_segment = *segments.begin();
					for (auto segment = segments.begin() + 1; segment != segments.end(); last_segment = *segment, ++segment) {
						size_t element_count = *segment - last_segment;
						for (size_t i = 0; i < element_count; ++i, ++idx) {
							auto index = indices[idx];
							const uint8_t* offset = reinterpret_cast<const uint8_t*>(reinterpret_cast<const uint16_t*>(buffer->data()->data()) + idx);
							uint16_t value = (*(offset + 1) << 8) | (*offset);
							std::cout << index << "(" << float32(value) << "), ";
						}
						if (element_count == 0) {
							std::cout << "-";
						}
						std::cout << std::endl;
					}
					std::cout << " ]\n";
				}
				else {
					std::cout << "DimMeta: " << m->array_segments() << std::endl;
				}
			}
			std::cout << "]"
				<< std::endl;
			//            exit(0);
		}
	}
#endif

	printed_tensors.insert(idx);
	return true;
}

bool ReadTFLiteOperators(const tflite::Model* model, const tflite::SubGraph* subgraph, std::set<uint32_t>& printed_tensors)
{
	uint64_t idx = 0;

	for (auto it = subgraph->operators()->begin(); it != subgraph->operators()->end(); ++it)
	{
		std::cout << idx++ << ". ";
		std::cout << "Operator: " << tflite::EnumNameBuiltinOperator(model->operator_codes()->Get(it->opcode_index())->builtin_code())
			<< " BuiltIn Type: " << tflite::EnumNameBuiltinOptions(it->builtin_options_type())
			<< " OpCode Index: " << it->opcode_index()
			<< std::endl
			<< "Inputs offset: " << std::hex << static_cast<uint64_t>(reinterpret_cast<const uint8_t*>(it->inputs()) - reinterpret_cast<const uint8_t*>(model)) << std::dec
			<< std::endl
			<< "Inputs: " << it->inputs()->size() << " {\n";

		for (auto inp = it->inputs()->begin(); inp != it->inputs()->end(); ++inp)
		{
			std::cout << "INPUT  ";
			ReadTFLiteTensor(*inp, model, subgraph, printed_tensors);
		}
		std::cout << "}\n"
			<< "Outputs offset: " << std::hex << static_cast<uint64_t>(reinterpret_cast<const uint8_t*>(it->outputs()) - reinterpret_cast<const uint8_t*>(model)) << std::dec
			<< std::endl
			<< "Outputs: " << it->outputs()->size() << " {\n";

		for (auto outp = it->outputs()->begin(); outp != it->outputs()->end(); ++outp)
		{
			std::cout << "OUTPUT ";
			ReadTFLiteTensor(*outp, model, subgraph, printed_tensors);
		}
		std::cout << "}\n";
		switch (it->builtin_options_type()) {
		case tflite::BuiltinOptions_Conv2DOptions:
		{
			auto op = it->builtin_options_as_Conv2DOptions();
			std::cout << "H Factor: " << op->dilation_h_factor() << " W Factor: " << op->dilation_w_factor() << std::endl
				<< "Fused activation func: " << tflite::EnumNameActivationFunctionType(op->fused_activation_function()) << std::endl
				<< "Stride H: " << op->stride_h() << " Stride W: " << op->stride_w() << std::endl;
		}
		break;
		}
	}
	return true;
}

bool ReadTFLiteSubgraph(const tflite::Model* model, const tflite::SubGraph* subgraph)
{
	uint64_t idx = 0;
	std::set<uint32_t> printed_tensors;

	std::cout << "SubGraph Name: " << subgraph->name() << std::endl;

	ReadTFLiteOperators(model, subgraph, printed_tensors);

	if (printed_tensors.size() != subgraph->tensors()->size())
		std::cout << "Abandoned tensors\n";
	for (auto it = subgraph->tensors()->begin(); it != subgraph->tensors()->end(); ++it)
	{
		if (printed_tensors.find(idx) != printed_tensors.end()) continue;
		std::cout << idx++ << ". ";
		std::cout << "Tensor Name: " << it->name()->c_str()
			<< " Type: " << tflite::EnumNameTensorType(it->type())
			<< std::endl
			<< " Quantization: " << tflite::EnumNameQuantizationDetails(it->quantization()->details_type())
			<< std::endl
			<< "Shape [";
		for (auto dim = it->shape()->begin(); dim != it->shape()->end(); ++dim)
			std::cout << *dim << ",";
		std::cout << "]"
			<< std::endl;
	}


	return true;
}

bool ReadTFLiteModel(const char* filename)
{
	if (filename == nullptr) return false;
	std::ifstream model_file(filename, std::ios::binary | std::ios::in);
	if (!model_file || !model_file.is_open()) return false;

	std::vector<char> m_data = { (std::istreambuf_iterator<char>(model_file)), std::istreambuf_iterator<char>() };
	model_file.close();

	const tflite::Model* model = tflite::GetModel(m_data.data());
	if (model == nullptr) return false;

	for (auto it = model->subgraphs()->begin(); it != model->subgraphs()->end(); ++it)
	{
		for (auto op = model->operator_codes()->begin(); op != model->operator_codes()->end(); ++op)
		{
			std::cout << "Known Builtin Operator: " << tflite::EnumNameBuiltinOperator(op->builtin_code()) << std::endl;
		}

		ReadTFLiteSubgraph(model, *it);
	}

	if (model->metadata()) {
		std::cout << "Metadata is presented\n";
		for (auto it = model->metadata()->begin(); it != model->metadata()->end(); ++it) {
			std::cout << it->name()->c_str() << std::endl;
		}
	}

	return true;
}

#define TFLITE_SCHEMA_VERSION (3)

uint8_t* AlignPointerUp(uint8_t* data, size_t alignment) {
	std::uintptr_t data_as_uintptr_t = reinterpret_cast<std::uintptr_t>(data);
	uint8_t* aligned_result = reinterpret_cast<uint8_t*>(
		((data_as_uintptr_t + (alignment - 1)) / alignment) * alignment);
	return aligned_result;
}

constexpr int MicroArenaBufferAlignment() { return 16; }

class StackAllocator : public flatbuffers::Allocator {
public:
	StackAllocator(size_t alignment) : data_size_(0) {
		data_ = AlignPointerUp(data_backing_, alignment);
	}

	uint8_t* allocate(size_t size) override {
		assert((data_size_ + size) <= kStackAllocatorSize);
		uint8_t* result = data_;
		data_ += size;
		data_size_ += size;
		return result;
	}

	void deallocate(uint8_t* p, size_t) override {}

	static StackAllocator& instance(size_t alignment = 1) {
		// Avoid using true dynamic memory allocation to be portable to bare metal.
		static char inst_memory[sizeof(StackAllocator)];
		static StackAllocator* inst = new (inst_memory) StackAllocator(alignment);
		return *inst;
	}

	static constexpr size_t kStackAllocatorSize = 8192;

private:
	uint8_t data_backing_[kStackAllocatorSize];
	uint8_t* data_;
	int data_size_;

	//	TF_LITE_REMOVE_VIRTUAL_DELETE
};

flatbuffers::FlatBufferBuilder* BuilderInstance() {
	static char inst_memory[sizeof(flatbuffers::FlatBufferBuilder)];
	static flatbuffers::FlatBufferBuilder* inst =
		new (inst_memory) flatbuffers::FlatBufferBuilder(
			StackAllocator::kStackAllocatorSize,
			&StackAllocator::instance(MicroArenaBufferAlignment()));
	return inst;
}


// A wrapper around FlatBuffer API to help build model easily.
class ModelBuilder {
public:
	typedef int32_t Tensor;
	typedef int Operator;
	typedef int Node;

	// `builder` needs to be available until BuildModel is called.
	explicit ModelBuilder(flatbuffers::FlatBufferBuilder* builder)
		: builder_(builder) {}

	// Registers an operator that will be used in the model.
	Operator RegisterCustomOp(tflite::BuiltinOperator op, const char* custom_code);
	Operator RegisterOp(tflite::BuiltinOperator op, const char* custom_code);

	// Adds a tensor to the model.
	Tensor AddTensor(tflite::TensorType type, std::initializer_list<int32_t> shape) {
		return AddTensorImpl(type, /* is_variable */ false, shape);
	}
	// Adds a quantized tensor to the model.
	Tensor AddQuantizedTensor(tflite::TensorType type,
		std::initializer_list<int32_t> shape, float scale, int64_t zero_point);
	// Adds a dequantized tensor to the model.
	Tensor AddDequantizedTensor(tflite::TensorType type,
		std::initializer_list<int32_t> shape);

	// Adds a variable tensor to the model.
	Tensor AddVariableTensor(tflite::TensorType type,
		std::initializer_list<int32_t> shape) {
		return AddTensorImpl(type, /* is_variable */ true, shape);
	}

	// Adds a node to the model with given input and output Tensors.
	Node AddNode(Operator op, std::initializer_list<Tensor> inputs,
		std::initializer_list<Tensor> outputs,
		std::initializer_list<Tensor> intermediates =
		std::initializer_list<Tensor>{});
	Node AddNodeQuantize(Operator op, std::initializer_list<Tensor> inputs,
		std::initializer_list<Tensor> outputs,
		std::initializer_list<Tensor> intermediates =
		std::initializer_list<Tensor>{});
	Node AddNodeDequantize(Operator op, std::initializer_list<Tensor> inputs,
		std::initializer_list<Tensor> outputs,
		std::initializer_list<Tensor> intermediates =
		std::initializer_list<Tensor>{});

	void AddMetadata(const char* description_string,
		const int32_t* metadata_buffer_data, size_t num_elements);

	// Constructs the flatbuffer model using `builder_` and return a pointer to
	// it. The returned model has the same lifetime as `builder_`.
	// Note the default value of 0 for num_subgraph_inputs means all tensor inputs
	// are in subgraph input list.
	const tflite::Model* BuildModel(std::initializer_list<Tensor> inputs,
		std::initializer_list<Tensor> outputs,
		size_t num_subgraph_inputs = 0, const char* filename = nullptr);

private:
	// Adds a tensor to the model.
	Tensor AddTensorImpl(tflite::TensorType type, bool is_variable,
		std::initializer_list<int32_t> shape);

	flatbuffers::FlatBufferBuilder* builder_;

	static constexpr int kMaxOperatorCodes = 10;
	flatbuffers::Offset<tflite::OperatorCode> operator_codes_[kMaxOperatorCodes];
	int next_operator_code_id_ = 0;

	static constexpr int kMaxOperators = 50;
	flatbuffers::Offset<tflite::Operator> operators_[kMaxOperators];
	int next_operator_id_ = 0;

	static constexpr int kMaxTensors = 50;
	flatbuffers::Offset<tflite::Tensor> tensors_[kMaxTensors];

	static constexpr int kMaxMetadataBuffers = 10;

	static constexpr int kMaxMetadatas = 10;
	flatbuffers::Offset<tflite::Metadata> metadata_[kMaxMetadatas];

	flatbuffers::Offset<tflite::Buffer> metadata_buffers_[kMaxMetadataBuffers];

	int nbr_of_metadata_buffers_ = 0;

	int next_tensor_id_ = 0;
};

ModelBuilder::Operator ModelBuilder::RegisterCustomOp(tflite::BuiltinOperator op,
	const char* custom_code) {
	assert(next_operator_code_id_ <= kMaxOperatorCodes);
	operator_codes_[next_operator_code_id_] = tflite::CreateOperatorCodeDirect(
		*builder_, /*deprecated_builtin_code=*/0, custom_code, /*version=*/TFLITE_SCHEMA_VERSION, op);
	next_operator_code_id_++;
	return next_operator_code_id_ - 1;
}

ModelBuilder::Operator ModelBuilder::RegisterOp(tflite::BuiltinOperator op,
	const char* custom_code) {
	assert(next_operator_code_id_ <= kMaxOperatorCodes);
	int8_t deprecated_builtin_code =
		static_cast<int8_t>(tflite::BuiltinOperator_PLACEHOLDER_FOR_GREATER_OP_CODES);
	if (op < tflite::BuiltinOperator_PLACEHOLDER_FOR_GREATER_OP_CODES) {
		deprecated_builtin_code = static_cast<int8_t>(op);
	}
	operator_codes_[next_operator_code_id_] = tflite::CreateOperatorCode(
		*builder_, deprecated_builtin_code, custom_code != nullptr ? builder_->CreateString(custom_code) : 0, /*version=*/TFLITE_SCHEMA_VERSION, op);
	next_operator_code_id_++;
	return next_operator_code_id_ - 1;
}

ModelBuilder::Node ModelBuilder::AddNode(
	ModelBuilder::Operator op,
	std::initializer_list<ModelBuilder::Tensor> inputs,
	std::initializer_list<ModelBuilder::Tensor> outputs,
	std::initializer_list<ModelBuilder::Tensor> intermediates) {
	assert(next_operator_id_ <= kMaxOperators);
	operators_[next_operator_id_] = tflite::CreateOperator(
		*builder_, op, builder_->CreateVector(inputs.begin(), inputs.size()),
		builder_->CreateVector(outputs.begin(), outputs.size()),
		tflite::BuiltinOptions_NONE,
		/*builtin_options=*/0,
		/*custom_options=*/0, tflite::CustomOptionsFormat_FLEXBUFFERS,
		/*mutating_variable_inputs =*/0,
		builder_->CreateVector(intermediates.begin(), intermediates.size()));
	next_operator_id_++;
	return next_operator_id_ - 1;
}

ModelBuilder::Node ModelBuilder::AddNodeQuantize(
	ModelBuilder::Operator op,
	std::initializer_list<ModelBuilder::Tensor> inputs,
	std::initializer_list<ModelBuilder::Tensor> outputs,
	std::initializer_list<ModelBuilder::Tensor> intermediates) {
	//assert(next_operator_id_ <= kMaxOperators);
	flatbuffers::Offset<tflite::QuantizeOptions> options = tflite::CreateQuantizeOptions(*builder_);
	operators_[next_operator_id_] = tflite::CreateOperator(
		*builder_, op, builder_->CreateVector(inputs.begin(), inputs.size()),
		builder_->CreateVector(outputs.begin(), outputs.size()),
		tflite::BuiltinOptions_QuantizeOptions,
		/*builtin_options=*/ options.Union(),
		/*custom_options=*/ 0,
		tflite::CustomOptionsFormat_FLEXBUFFERS,
		/*mutating_variable_inputs =*/0,
		/*intermediate*/ /*builder_->CreateVector(intermediates.begin(), intermediates.size())*/ 0);
	next_operator_id_++;
	return next_operator_id_ - 1;
}

ModelBuilder::Node ModelBuilder::AddNodeDequantize(
	ModelBuilder::Operator op,
	std::initializer_list<ModelBuilder::Tensor> inputs,
	std::initializer_list<ModelBuilder::Tensor> outputs,
	std::initializer_list<ModelBuilder::Tensor> intermediates) {
	//assert(next_operator_id_ <= kMaxOperators);
	flatbuffers::Offset<tflite::DequantizeOptions> options = tflite::CreateDequantizeOptions(*builder_);
	operators_[next_operator_id_] = tflite::CreateOperator(
		*builder_, op, builder_->CreateVector(inputs.begin(), inputs.size()),
		builder_->CreateVector(outputs.begin(), outputs.size()),
		tflite::BuiltinOptions_DequantizeOptions,
		/*builtin_options=*/ options.Union(),
		/*custom_options=*/ 0,
		tflite::CustomOptionsFormat_FLEXBUFFERS,
		/*mutating_variable_inputs =*/0,
		/*intermediate*/ /*builder_->CreateVector(intermediates.begin(), intermediates.size())*/ 0);
	next_operator_id_++;
	return next_operator_id_ - 1;
}

void ModelBuilder::AddMetadata(const char* description_string,
	const int32_t* metadata_buffer_data,
	size_t num_elements) {
	metadata_[ModelBuilder::nbr_of_metadata_buffers_] =
		tflite::CreateMetadata(*builder_, builder_->CreateString(description_string),
			1 + ModelBuilder::nbr_of_metadata_buffers_);

	metadata_buffers_[nbr_of_metadata_buffers_] = tflite::CreateBuffer(
		*builder_, builder_->CreateVector((uint8_t*)metadata_buffer_data,
			sizeof(uint32_t) * num_elements));

	ModelBuilder::nbr_of_metadata_buffers_++;
}

const tflite::Model* ModelBuilder::BuildModel(
	std::initializer_list<ModelBuilder::Tensor> inputs,
	std::initializer_list<ModelBuilder::Tensor> outputs,
	size_t num_subgraph_inputs,
	const char* filename) {
	// Model schema requires an empty buffer at idx 0.
	size_t buffer_size = 1 + ModelBuilder::nbr_of_metadata_buffers_;
	flatbuffers::Offset<tflite::Buffer> buffers[kMaxMetadataBuffers];
	buffers[0] = tflite::CreateBuffer(*builder_);

	// Place the metadata buffers first in the buffer since the indices for them
	// have already been set in AddMetadata()
	for (int i = 1; i < ModelBuilder::nbr_of_metadata_buffers_ + 1; ++i) {
		buffers[i] = metadata_buffers_[i - 1];
	}

	// Default to single subgraph model.
	constexpr size_t subgraphs_size = 1;

	// Find out number of subgraph inputs.
	if (num_subgraph_inputs == 0) {
		// This is the default case.
		num_subgraph_inputs = inputs.size();
	}
	else {
		// A non-zero value of num_subgraph_inputs means that some of
		// the operator input tensors are not subgraph inputs.
		assert(num_subgraph_inputs <= inputs.size());
	}

	const flatbuffers::Offset<tflite::SubGraph> subgraphs[subgraphs_size] = {
		tflite::CreateSubGraph(
			*builder_, builder_->CreateVector(tensors_, next_tensor_id_),
			builder_->CreateVector(inputs.begin(), num_subgraph_inputs),
			builder_->CreateVector(outputs.begin(), outputs.size()),
			builder_->CreateVector(operators_, next_operator_id_),
			builder_->CreateString("test_subgraph")) };

	flatbuffers::Offset<tflite::Model> model_offset;
	if (ModelBuilder::nbr_of_metadata_buffers_ > 0) {
		model_offset = tflite::CreateModel(
			*builder_, TFLITE_SCHEMA_VERSION, /*version*/
			builder_->CreateVector(operator_codes_, next_operator_code_id_),
			builder_->CreateVector(subgraphs, subgraphs_size),
			builder_->CreateString("test_model"),
			builder_->CreateVector(buffers, buffer_size), 0,
			builder_->CreateVector(metadata_,
				ModelBuilder::nbr_of_metadata_buffers_));
	}
	else {
		model_offset = tflite::CreateModel(
			*builder_, TFLITE_SCHEMA_VERSION, /*version*/
			builder_->CreateVector(operator_codes_, next_operator_code_id_),
			builder_->CreateVector(subgraphs, subgraphs_size),
			builder_->CreateString("test_model"),
			builder_->CreateVector(buffers, buffer_size));
	}

	tflite::FinishModelBuffer(*builder_, model_offset);
	void* model_pointer = builder_->GetBufferPointer();

	if (filename) {
		size_t data_size = builder_->GetSize();
		auto fl = fopen(filename, "wb");
		fwrite(model_pointer, data_size, 1, fl);
		fclose(fl);
	}
	const tflite::Model* model = flatbuffers::GetRoot<tflite::Model>(model_pointer);
	return model;
}

ModelBuilder::Tensor ModelBuilder::AddTensorImpl(
	tflite::TensorType type, bool is_variable, std::initializer_list<int32_t> shape) {
	assert(next_tensor_id_ <= kMaxTensors);
	tensors_[next_tensor_id_] = tflite::CreateTensor(
		*builder_, builder_->CreateVector(shape.begin(), shape.size()), type,
		/* buffer */ 0,
		/* name */ builder_->CreateString(std::string("tensor_") + std::to_string(next_tensor_id_)),
		/* quantization */ 0,
		/* is_variable */ is_variable,
		/* sparsity */ 0);
	next_tensor_id_++;
	return next_tensor_id_ - 1;
}

ModelBuilder::Tensor ModelBuilder::AddQuantizedTensor(
	tflite::TensorType type, std::initializer_list<int32_t> shape, float scale, int64_t zero_point) {
	assert(next_tensor_id_ <= kMaxTensors);
	constexpr size_t quant_params_size = 1;
	//const float min_array[quant_params_size] = { 0.1f };
	//const float max_array[quant_params_size] = { 0.2f };
	const float scale_array[quant_params_size] = { scale };
	const int64_t zero_point_array[quant_params_size] = { zero_point };

	const flatbuffers::Offset<tflite::QuantizationParameters> quant_params =
		tflite::CreateQuantizationParameters(
			*builder_,
			/*min=*/ 0, //builder_->CreateVector<float>(min_array, quant_params_size),
			/*max=*/ 0, //builder_->CreateVector<float>(max_array, quant_params_size),
			/*scale=*/
			builder_->CreateVector<float>(scale_array, quant_params_size),
			/*zero_point=*/
			builder_->CreateVector<int64_t>(zero_point_array, quant_params_size));

	tensors_[next_tensor_id_] = tflite::CreateTensor(
		*builder_, builder_->CreateVector(shape.begin(), shape.size()), type,
		/* buffer */ 0,
		/* name */ builder_->CreateString(std::string("tensor_") + std::to_string(next_tensor_id_)),
		/* quantization */ quant_params,
		/* is_variable */ false,
		/* sparsity */ 0);
	next_tensor_id_++;
	return next_tensor_id_ - 1;
}

ModelBuilder::Tensor ModelBuilder::AddDequantizedTensor(
	tflite::TensorType type, std::initializer_list<int32_t> shape) {
	assert(next_tensor_id_ <= kMaxTensors);
	constexpr size_t quant_params_size = 1;
	//const float min_array[quant_params_size] = { 0.1f };
	//const float max_array[quant_params_size] = { 0.2f };
	//const float scale_array[quant_params_size] = { scale };
	//const int64_t zero_point_array[quant_params_size] = { zero_point };

	const flatbuffers::Offset<tflite::QuantizationParameters> quant_params =
		tflite::CreateQuantizationParameters(
			*builder_,
			/*min=*/ 0, //builder_->CreateVector<float>(min_array, quant_params_size),
			/*max=*/ 0, //builder_->CreateVector<float>(max_array, quant_params_size),
			/*scale=*/
			0, //builder_->CreateVector<float>(scale_array, quant_params_size),
			/*zero_point=*/
			0 //builder_->CreateVector<int64_t>(zero_point_array, quant_params_size)
			);

	tensors_[next_tensor_id_] = tflite::CreateTensor(
		*builder_, builder_->CreateVector(shape.begin(), shape.size()), type,
		/* buffer */ 0,
		/* name */ builder_->CreateString(std::string("tensor_") + std::to_string(next_tensor_id_)),
		/* quantization */ quant_params,
		/* is_variable */ false,
		/* sparsity */ 0);
	next_tensor_id_++;
	return next_tensor_id_ - 1;
}

const tflite::Model* WriteTFLModel1(const char* filename) {
	using flatbuffers::Offset;
	flatbuffers::FlatBufferBuilder* fb_builder = BuilderInstance();

	ModelBuilder model_builder(fb_builder);

	const int op_id =
		model_builder.RegisterCustomOp(tflite::BuiltinOperator_CUSTOM, "simple_stateful_op");
	const int input_tensor = model_builder.AddTensor(tflite::TensorType_INT8, { 3 });
	const int median_tensor = model_builder.AddTensor(tflite::TensorType_INT8, { 3 });
	const int invoke_count_tensor =
		model_builder.AddTensor(tflite::TensorType_INT32, { 1 });
	const int intermediate_tensor =
		model_builder.AddTensor(tflite::TensorType_FLOAT32, { 0 });

	model_builder.AddNode(op_id, { input_tensor },
		{ median_tensor, invoke_count_tensor },
		{ intermediate_tensor });

	return model_builder.BuildModel({ input_tensor },
		{ median_tensor, invoke_count_tensor }, 0, filename);
}

const tflite::Model* WriteTFLModel2(const char* filename) {
	using flatbuffers::Offset;
	flatbuffers::FlatBufferBuilder* fb_builder = BuilderInstance();

	ModelBuilder model_builder(fb_builder);

	const int op_id =
		model_builder.RegisterOp(tflite::BuiltinOperator_QUANTIZE, nullptr);
	const int input_tensor = model_builder.AddTensor(tflite::TensorType_FLOAT32, { 12 });
	//const int output_tensor = model_builder.AddTensor(tflite::TensorType_INT8, { 3 });
	const int output_tensor = model_builder.AddQuantizedTensor(tflite::TensorType_INT8, { 12 }, 0.25, 16);

	model_builder.AddNodeQuantize(op_id, { input_tensor },
		{ output_tensor },
		{ });

	return model_builder.BuildModel({ input_tensor },
		{ output_tensor }, 0, filename);
}

const tflite::Model* WriteTFLModel3(const char* filename) {
	using flatbuffers::Offset;
	flatbuffers::FlatBufferBuilder* fb_builder = BuilderInstance();

	ModelBuilder model_builder(fb_builder);

	const int op_id =
		model_builder.RegisterOp(tflite::BuiltinOperator_QUANTIZE, nullptr);
	const int input_tensor = model_builder.AddTensor(tflite::TensorType_FLOAT32, { 12 });
	//const int output_tensor = model_builder.AddTensor(tflite::TensorType_INT8, { 3 });
	const int output_tensor = model_builder.AddQuantizedTensor(tflite::TensorType_UINT8, { 12 }, 0.25, 16);

	model_builder.AddNodeQuantize(op_id, { input_tensor },
		{ output_tensor },
		{ });

	return model_builder.BuildModel({ input_tensor },
		{ output_tensor }, 0, filename);
}

const tflite::Model* WriteTFLModel4(const char* filename) {
	using flatbuffers::Offset;
	flatbuffers::FlatBufferBuilder* fb_builder = BuilderInstance();

	ModelBuilder model_builder(fb_builder);

	const int op_id =
		model_builder.RegisterOp(tflite::BuiltinOperator_DEQUANTIZE, nullptr);
	const int input_tensor = model_builder.AddQuantizedTensor(tflite::TensorType_INT8, { 12 }, 0.25, 16);
	const int output_tensor = model_builder.AddTensor(tflite::TensorType_FLOAT32, { 12 });

	model_builder.AddNodeDequantize(op_id, { input_tensor },
		{ output_tensor },
		{ });

	return model_builder.BuildModel({ input_tensor },
		{ output_tensor }, 0, filename);
}

const tflite::Model* WriteTFLModel5(const char* filename) {
	using flatbuffers::Offset;
	flatbuffers::FlatBufferBuilder* fb_builder = BuilderInstance();

	ModelBuilder model_builder(fb_builder);

	const int op_id =
		model_builder.RegisterOp(tflite::BuiltinOperator_DEQUANTIZE, nullptr);
	const int input_tensor = model_builder.AddQuantizedTensor(tflite::TensorType_UINT8, { 12 }, 0.25, 16);
	const int output_tensor = model_builder.AddTensor(tflite::TensorType_FLOAT32, { 12 });

	model_builder.AddNodeDequantize(op_id, { input_tensor },
		{ output_tensor },
		{ });

	return model_builder.BuildModel({ input_tensor },
		{ output_tensor }, 0, filename);
}

const tflite::Model* WriteTFLModel6(const char* filename) {
	using flatbuffers::Offset;
	flatbuffers::FlatBufferBuilder* fb_builder = BuilderInstance();

	ModelBuilder model_builder(fb_builder);

	const int q_id =
		model_builder.RegisterOp(tflite::BuiltinOperator_QUANTIZE, nullptr);
	const int dq_id =
		model_builder.RegisterOp(tflite::BuiltinOperator_DEQUANTIZE, nullptr);
	const int input_tensor = model_builder.AddTensor(tflite::TensorType_FLOAT32, { 12 });
	const int mid_tensor = model_builder.AddQuantizedTensor(tflite::TensorType_UINT8, { 12 }, 0.25, 16);
	const int output_tensor = model_builder.AddTensor(tflite::TensorType_FLOAT32, { 12 });

	model_builder.AddNodeQuantize(q_id, { input_tensor },
		{ mid_tensor },
		{ });
	model_builder.AddNodeDequantize(dq_id, { mid_tensor },
		{ output_tensor },
		{ });

	return model_builder.BuildModel({ input_tensor },
		{ output_tensor }, 0, filename);
}

const tflite::Model* WriteTFLModel7(const char* filename) {
	using flatbuffers::Offset;
	flatbuffers::FlatBufferBuilder* fb_builder = BuilderInstance();

	ModelBuilder model_builder(fb_builder);

	const int q_id =
		model_builder.RegisterOp(tflite::BuiltinOperator_QUANTIZE, nullptr);
	const int dq_id =
		model_builder.RegisterOp(tflite::BuiltinOperator_DEQUANTIZE, nullptr);
	const int input_tensor = model_builder.AddTensor(tflite::TensorType_FLOAT32, { 12 });
	const int mid_tensor = model_builder.AddQuantizedTensor(tflite::TensorType_INT8, { 12 }, 0.25, 16);
	const int output_tensor = model_builder.AddTensor(tflite::TensorType_FLOAT32, { 12 });

	model_builder.AddNodeQuantize(q_id, { input_tensor },
		{ mid_tensor },
		{ });
	model_builder.AddNodeDequantize(dq_id, { mid_tensor },
		{ output_tensor },
		{ });

	return model_builder.BuildModel({ input_tensor },
		{ output_tensor }, 0, filename);
}

int main(int argc, const char** argv)
{
    //Writing a test models from a scratch
	//WriteTFLModel7("./model.tflite");
	if (argc >= 2)
	{
        ReadTFLiteModel(argv[1]);
	} else {
        std::cout << "Usage: TFLiteReader.exe path/to/a/model.tflite";
        return 0;
    }
	return 0;
}
