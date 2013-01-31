using System.Web.Mvc;

namespace Blog.Controllers
{
	[HandleError]
	public class AbstractErrorLoggingController : Controller
	{
		protected override void OnException(ExceptionContext filterContext)
		{
			// TODO: Error logging here..
			base.OnException(filterContext);
		}
	}
}
