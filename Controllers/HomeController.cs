using Acoes_Fiis.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Acoes_Fiis.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        // GET: Home/Index
        public IActionResult Index(string visao)
        {
            if (string.IsNullOrEmpty(visao))
            {
                visao = "Gabriel";
            }

            ViewBag.VisaoAtual = visao;

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}