using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AURA.Interfaces;
using AURA.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace AURA.Services
{
    public class Gpt4oValidatorService : IVisualValidator
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        public Gpt4oValidatorService(HttpClient httpClient, IConfiguration config, IWebHostEnvironment env)
        {
            _httpClient = httpClient;
            _config = config;
            _env = env;
        }

        public async Task<ReimbursementRequest> ValidateReceiptAsync(ReimbursementRequest request)
        {
            var sw = Stopwatch.StartNew();
            var apiKey = _config["OpenAI:ApiKey"];
            
            if (string.IsNullOrEmpty(apiKey) || apiKey == "sk-mock-key-replace-me") { request.Status = "ESCALATE - ERROR"; request.AiReasoning = "Chưa cấu hình API Key thật!"; sw.Stop(); request.ProcessingLatencyMs = sw.ElapsedMilliseconds; return request; }

            try
            {
                // 1. Get Physical path of the image
                var filePath = Path.Combine(_env.WebRootPath, request.ImageUrl.TrimStart('/'));
                if (!File.Exists(filePath))
                {
                    request.Status = "ESCALATE - ERROR";
                    request.AiReasoning = "File ảnh không tồn tại trên server.";
                    return request;
                }

                // 2. Read to Base64
                byte[] imageArray = await File.ReadAllBytesAsync(filePath);
                string base64ImageRepresentation = Convert.ToBase64String(imageArray);
                
                string extension = Path.GetExtension(filePath).ToLowerInvariant();
                string mediaType = extension == ".png" ? "image/png" : "image/jpeg";

                // 3. Build OpenAI Request Payload
                var payload = new
                {
                    model = "gpt-4o",
                    messages = new object[]
                    {
                        new
                        {
                            role = "system",
                            content = "You are an AI financial auditor. Analyze the receipt image. Output MUST be valid JSON with strictly two fields: 'status' (must be exactly 'AUTO_APPROVE' or 'ESCALATE') and 'reasoning' (a brief explanation in Vietnamese). Rule: Reject if missing Tax ID, or if it contains alcohol."
                        },
                        new
                        {
                            role = "user",
                            content = new object[]
                            {
                                new { type = "text", text = "Analyze this receipt based on the system rules." },
                                new
                                {
                                    type = "image_url",
                                    image_url = new
                                    {
                                        url = $"data:{mediaType};base64,{base64ImageRepresentation}"
                                    }
                                }
                            }
                        }
                    },
                    max_tokens = 300,
                    response_format = new { type = "json_object" }
                };

                var requestContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                // 4. Call OpenAI API
                var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", requestContent);
                var responseString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(responseString);
                    var contentStr = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                    
                    if (!string.IsNullOrEmpty(contentStr))
                    {
                        using var resultDoc = JsonDocument.Parse(contentStr);
                        request.Status = resultDoc.RootElement.GetProperty("status").GetString() ?? "ESCALATE - ERROR";
                        request.AiReasoning = resultDoc.RootElement.GetProperty("reasoning").GetString() ?? "Lỗi bóc tách JSON từ GPT";
                        
                        if (request.Status.Contains("ESCALATE"))
                        {
                            request.ManagerQuestion = "Hệ thống phát hiện rủi ro. Sếp có duyệt ngoại lệ không? [CÓ/KHÔNG]";
                        }
                    }
                }
                else
                {
                    request.Status = "ESCALATE - ERROR";
                    request.AiReasoning = $"API Error: {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                request.Status = "ESCALATE - ERROR";
                request.AiReasoning = $"System Exception: {ex.Message}";
            }

            sw.Stop();
            request.ProcessingLatencyMs = sw.ElapsedMilliseconds;
            return request;
        }

        


