# Vision input, token budget và off-load

Cập nhật: 07/10/2026. Tài liệu này phân biệt bốn nguyên nhân thường bị gộp nhầm khi
VLM đọc hóa đơn sai: ảnh đầu vào, context, output cap và CPU/GPU off-load.

## 1. AURA đang gửi ảnh gì?

`ReceiptExtractionContract.LoadInputAsync` đọc nguyên byte của file ảnh rồi base64 hóa.
Cả OpenRouter và Ollama nhận ảnh đó; AURA không resize, crop hay nén lại. Vì vậy:

- luôn upload một ảnh hóa đơn riêng, không dùng contact sheet;
- xem ảnh gốc ở 100% zoom trước khi kết luận chữ nhỏ;
- giữ đủ vùng tên người bán, mã, ngày, item và tổng cuối;
- ảnh rung, out-focus, bay mực, phản sáng hoặc chữ chỉ còn vài pixel vẫn có thể làm giảm
  độ chính xác dù file có kích thước lớn.

Contact sheet của Test Kit chỉ là mục lục. Runner v3.1 dùng từng file khoảng 1.1k x 1.5k
pixel trong `test_kit/v3_1/images` và xác minh SHA-256 trước request.

## 2. Off-load có làm giảm chất lượng không?

Nếu **cùng model weights, cùng quantization, cùng ảnh, cùng prompt và cùng context**, việc
đặt một số layer trên CPU thay vì GPU chủ yếu làm tăng latency và traffic RAM/VRAM. Nó
không phải một token threshold và không tự cắt ảnh hay câu trả lời.

Chất lượng có thể giảm **gián tiếp** khi phải đổi cấu hình để vừa VRAM:

1. thay 8B bằng model nhỏ hơn;
2. dùng quantization thấp hơn hoặc KV cache nén mạnh hơn;
3. giảm `num_ctx`, khiến prompt + policy + schema + image tokens + history/repair không còn
   đủ chỗ;
4. giảm `num_predict`, khiến JSON bị dừng giữa chừng;
5. resize/nén ảnh ở client hoặc gateway;
6. để provider áp dụng context transform/truncation.

Do đó không được kết luận “CPU/GPU split làm OCR kém” chỉ từ một output. Phải giữ mọi
biến còn lại cố định rồi đối chiếu cùng dataset, seed/temperature, model tag và manifest.

## 3. `num_ctx` khác `MaxOutputTokens`

- `Ollama:ContextTokens` (`num_ctx`) là cửa sổ context dùng cho toàn bộ input và quá trình
  sinh. Với AURA, input gồm prompt, policy, JSON schema và biểu diễn ảnh; semantic repair
  là một request mới có thêm chỉ dẫn sửa.
- `Ollama:MaxOutputTokens` (`num_predict`) và `OpenRouter:MaxOutputTokens` (`max_tokens`)
  chỉ giới hạn phần JSON được sinh. Chúng không trực tiếp hạ độ phân giải ảnh.
- Nếu output chạm giới hạn, AURA trả `AI_RESPONSE_TRUNCATED` và fail safe; không biến JSON
  cụt thành `AUTO_APPROVE`.

Backend hiện log token counts provider trả về: OpenRouter có prompt/completion/total;
Ollama có prompt-eval/completion cùng các duration. Đây là evidence đúng để biết giới hạn
có thật sự bị chạm, thay vì đoán từ latency.

## 4. Cấu hình được phép benchmark

Baseline hosted:

```text
Vision__Provider=OpenRouter
OpenRouter__Model=qwen/qwen3-vl-8b-instruct
OpenRouter__MaxOutputTokens=4096
Vision__FallbackEnabled=false
```

GPU BTC 16 GB, benchmark riêng:

```text
Vision__Provider=Ollama
Ollama__Model=qwen3-vl:8b-instruct-q4_K_M
Ollama__ContextTokens=16384
Ollama__MaxOutputTokens=4096
Ollama__KeepAlive=30m
Vision__FallbackEnabled=false
```

Không tăng/giảm context giữa một batch. Nếu đổi model, quantization, context, output cap
hoặc image preprocessing thì đó là cấu hình mới và phải chạy lại cả batch vào evidence
directory mới.

## 5. Evidence tối thiểu cho GPU/off-load

Trước batch:

```powershell
ollama show qwen3-vl:8b-instruct-q4_K_M
ollama ps
nvidia-smi
```

Trong/sau request đầu và batch:

1. chụp `ollama ps`; ghi `PROCESSOR` là GPU hay CPU/GPU split;
2. lưu model tag, context, output cap, manifest hash và image hash;
3. lưu actual facts/decision, semantic-repair flag, lỗi và latency từng ca;
4. lấy token/duration từ application log;
5. so decision exact match, critical-field match, missed escalation, over-escalation và
   system error; không chỉ so thời gian.

Chỉ bật fallback sau khi cấu hình Ollama độc lập đạt gate. Health `ok` xác nhận cấu hình,
không chứng minh model đã đọc ảnh; request ảnh mới là inference smoke thật.

## 6. Test Kit v3.1 sau bản vá

Policy C# đã bổ sung nhóm từ khóa hẹp cho `vé xem phim`, `rạp chiếu phim`, `movie ticket`
và `cinema ticket`. Prompt cũng yêu cầu giữ nguyên dấu tiếng Việt, đặc biệt không đổi
`Bìa hồ sơ` thành `Bia hồ sơ`. V3/v3.1 vẫn giữ nguyên ảnh và hash; mọi thay đổi font/layout
phải thành v3.2 và khóa ground truth/hash trước request đầu tiên.
