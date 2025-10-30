using Microsoft.AspNetCore.Mvc;

namespace StudyRoom.Controllers
{
public class HomeController : Controller
{
public IActionResult Index() => RedirectToAction("Index", "Rooms");
}
}