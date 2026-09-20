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

        public VerifyController(IVisionExtractor vision, IWebHostEnvironment env)
        {
            _vision = vision;
            _env = env;
        }

        public IActionResult Index()
        {
            return View();
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
                    var imagePath = Path.Combine(_env.ContentRootPath, "test_data", "images", testCase.ImageName);
                    
                    var tempWebPath = Path.Combine(_env.WebRootPath, "temp_test", testCase.ImageName);
                    Directory.CreateDirectory(Path.Combine(_env.WebRootPath, "temp_test"));
                    if (System.IO.File.Exists(imagePath)) System.IO.File.Copy(imagePath, tempWebPath, true);

                    var facts = await _vision.ExtractFactsAsync("/temp_test/" + testCase.ImageName);
                    var decision = PolicyDecisionEngine.Evaluate(facts, 500000); // Dummy 500k for test

                    actualStatus = decision.Status;
                    reason = decision.Reason;
                    question = decision.ManagerQuestion;
                }
                catch (Exception ex)
                {
                    actualStatus = "ESCALATE_SYSTEM_ERROR";
                    reason = ex.Message;
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

        public class ExpectedResult
        {
            public string Id { get; set; }
            public string ExpectedStatus { get; set; }
            public string ImageName { get; set; }
        }
    }
}



