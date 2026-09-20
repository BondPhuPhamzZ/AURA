using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AURA.Interfaces;
using AURA.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace AURA.Services
{
    public class GeminiVisionExtractorService : IVisionExtractor
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        public GeminiVisionExtractorService(HttpClient httpClient, IConfiguration config, IWebHostEnvironment env)
        {
            _httpClient = httpClient;
            _config = config;
            _env = env;
        }

        public async Task<ReceiptExtractionDto> ExtractFactsAsync(string imagePath)
        {
            var apiKey = _config["Gemini:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new Exception("Chưa cấu hình Gemini:ApiKey trong Secret Manager.");
            }

            var fullPath = Path.Combine(_env.WebRootPath, imagePath.TrimStart('/'));
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("Không tìm thấy file ảnh", fullPath);
            }

            byte[] imageArray = await File.ReadAllBytesAsync(fullPath);
            string base64Image = Convert.ToBase64String(imageArray);
            string extension = Path.GetExtension(fullPath).ToLowerInvariant();
            string mimeType = extension == ".png" ? "image/png" : "image/jpeg";

            var businessRulesPath = Path.Combine(_env.WebRootPath, "BUSINESS_RULES.md");
            string promptText;
            if (File.Exists(businessRulesPath))
            {
                promptText = await File.ReadAllTextAsync(businessRulesPath);
            }
            else
            {
                promptText = "Analyze this receipt image. Return only valid JSON. Do not include markdown or text outside the JSON object.";
            }

            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = promptText },
                            new
                            {
                                inlineData = new
                                {
                                    mimeType = mimeType,
                                    data = base64Image
                                }
                            }
                        }
                    }
                },
                generationConfig = new
                {
                    responseMimeType = "application/json"
                }
            };

            var requestContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.6-flash:generateContent?key={apiKey}";
            
            var response = await _httpClient.PostAsync(url, requestContent);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Gemini API Error: {response.StatusCode} - {responseString}");
            }

            using var doc = JsonDocument.Parse(responseString);
            var candidates = doc.RootElement.GetProperty("candidates");
            if (candidates.GetArrayLength() == 0) throw new Exception("No response from Gemini.");
            
            var text = candidates[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
            
            // Clean up potential markdown formatting if Gemini ignored the prompt instruction
            if (text.StartsWith("`json"))
            {
                text = text.Replace("`json", "").Replace("`", "").Trim();
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<ReceiptExtractionDto>(text, options);
        }
    }
}




