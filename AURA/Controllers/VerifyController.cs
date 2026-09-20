using AURA.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AURA.Interfaces;
using AURA.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace AURA.Controllers
{
    public class VerifyController : Controller
    {
        private readonly IVisionExtractor _vision;
        private readonly IWebHostEnvironment _env;
        private readonly IReimbursementRepository _repo;
        private readonly IAuditLogger _audit;

        public VerifyController(IVisionExtractor vision, IWebHostEnvironment env, IReimbursementRepository repo, IAuditLogger audit)
        {
            _vision = vision;
            _env = env;
            _repo = repo;
            _audit = audit;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunHarness()
        {
            var results = new List<object>();
            var expectedJsonPath = Path.Combine(_env.WebRootPath, "test_data", "expected-results.json");
            
            if (!System.IO.File.Exists(expectedJsonPath)) return Json(new { error = "Không tìm thấy expected-results.json" });

            var expectedData = JsonSerializer.Deserialize<List<ExpectedResult>>(System.IO.File.ReadAllText(expectedJsonPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            foreach (var testCase in expectedData)
            {
                var sw = Stopwatch.StartNew();
                string actualStatus;
                string reason = "";
                string question = "";

                try 
                {
                    var imagePath = Path.Combine(_env.WebRootPath, "test_data", "images", testCase.ImageName);
                    
                    var tempWebPath = Path.Combine(_env.WebRootPath, "temp_test", testCase.ImageName);
                    Directory.CreateDirectory(Path.Combine(_env.WebRootPath, "temp_test"));
                    if (System.IO.File.Exists(imagePath)) System.IO.File.Copy(imagePath, tempWebPath, true);

                    var facts = await _vision.ExtractFactsAsync("/temp_test/" + testCase.ImageName);
                    var decision = PolicyDecisionEngine.Evaluate(facts, 500000);

                    actualStatus = decision.Status;
                    reason = decision.Reason;
                    question = decision.ManagerQuestion;
                    
                    // Save to DB
                    var req = new ReimbursementRequest {
                        Id = Guid.NewGuid().ToString(),
                        ClaimedAmount = 500000,
                        ImageUrl = "/temp_test/" + testCase.ImageName,
                        CreatedAt = DateTime.UtcNow,
                        Status = actualStatus,
                        AiReasoning = reason,
                        ManagerQuestion = question,
                        ProcessingLatencyMs = sw.ElapsedMilliseconds
                    };
                    await _repo.AddRequestAsync(req);
                    await _audit.LogActionAsync(req.Id, $"AI_PROCESSED_{actualStatus}", $"[HARNESS] Reason: {reason}");
                }
                catch (Exception ex)
                {
                    actualStatus = "ESCALATE_SYSTEM_ERROR";
                    reason = ex.Message;
                    
                    var reqId = Guid.NewGuid().ToString();
                    await _audit.LogActionAsync(reqId, $"AI_PROCESSED_SYSTEM_ERROR", $"[HARNESS] Error: {ex.Message}");
                }

                sw.Stop();
                
                results.Add(new {
                    CaseId = testCase.Id,
                    Image = testCase.ImageName,
                    Expected = testCase.ExpectedStatus,
                    Actual = actualStatus,
                    Pass = (testCase.ExpectedStatus == actualStatus),
                    Reason = reason,
                    Question = question,
                    LatencyMs = sw.ElapsedMilliseconds,
                    Timestamp = DateTime.UtcNow
                });
            }

            return Json(results);
        }
    }
}
