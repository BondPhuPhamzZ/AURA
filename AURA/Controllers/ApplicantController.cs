using Microsoft.AspNetCore.Mvc;

namespace AURA.Controllers
{
    public class ApplicantController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
