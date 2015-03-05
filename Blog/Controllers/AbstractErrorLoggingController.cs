    using System;
using System.IO;
using System.Web.Mvc;

namespace Blog.Controllers
{
	[HandleError]
	public class AbstractErrorLoggingController : Controller
	{
        private static Random _rnd = new Random();
		protected override void OnException(ExceptionContext filterContext)
		{
            if (filterContext.Exception != null)
            {
                try
                {
                    var errorFolder = new DirectoryInfo(Server.MapPath("~/App_Data/Errors"));
                    if (!errorFolder.Exists)
                        errorFolder.Create();
                    string errorFilename;
                    lock (_rnd)
                    {
                        errorFilename = Path.Combine(
                            errorFolder.FullName,
                            "error " + DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss") + " " + _rnd.Next() + ".log"
                        );
                    }
                    System.IO.File.WriteAllText(
                        errorFilename,
                        string.Format(
                            "{0} {1}{2}{2}{3}",
                            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            filterContext.Exception.Message,
                            Environment.NewLine,
                            filterContext.Exception.StackTrace
                        )
                    );
                }
                catch { }
            }

			base.OnException(filterContext);
		}
	}
}
