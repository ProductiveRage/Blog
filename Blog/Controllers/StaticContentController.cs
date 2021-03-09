using Microsoft.AspNetCore.Mvc;

namespace Blog.Controllers
{
    public sealed class StaticContentController : Controller
	{
		public IActionResult About() => View();

		public IActionResult ErrorPage() => View("Error");

		public IActionResult ErrorPage404()
		{
			HttpContext.Response.StatusCode = 404;
			return ErrorPage();
		}
	}
}