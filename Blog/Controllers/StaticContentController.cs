using System;
using System.Web.Mvc;
namespace Blog.Controllers
{
	public class StaticContentController : AbstractErrorLoggingController
	{
		public ActionResult About()
		{
			return View();
		}

		public ActionResult ComingSoon()
		{
			return View("ComingSoon");
		}

		public ActionResult ErrorPage()
		{
			return View("Error");
		}

		public ActionResult ErrorPage404()
		{
			HttpContext.Response.StatusCode = 404;
			return ErrorPage();
		}
	}
}
