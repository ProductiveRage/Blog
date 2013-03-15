using System;
using System.Diagnostics;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Blog.Factories;

namespace Blog
{
	public class MvcApplication : RequestTimingApplication
	{
		public static void RegisterRoutes(RouteCollection routes)
		{
			routes.IgnoreRoute("{*staticcontent}", new { staticcontent = @".*\.(bmp|gif|ico|jpg|js|png)(/.*)?" });

			routes.RouteExistingFiles = true; // We have to set this to true so that stylesheets (for example) get processed rather than returned direct

			routes.MapRoute(
				"Stylesheets",
				"{*sylesheetextensions}",
				new { controller = "CSS", action = "Process" },
				new { sylesheetextensions = @".*\.(css|less)(/.*)?" }
			);

			routes.MapRoute(
			  "ArchiveById",
			  "Read/{id}",
			  new { controller = "ViewPost", action = "ArchiveById" }
			);

			routes.MapRoute(
			  "ArchiveByTag",
			  "Archive/Tag/{tag}",
			  new { controller = "ViewPost", action = "ArchiveByTag" }
			);

			routes.MapRoute(
			  "ArchiveByMonth",
			  "Archive/{month}/{year}",
			  new { controller = "ViewPost", action = "ArchiveByMonth" }
			);

			routes.MapRoute(
			  "Search",
			  "Search",
			  new { controller = "Search", action = "Search" }
			);

			routes.MapRoute(
			  "AutoComplete",
			  "AutoComplete.json",
			  new { controller = "Search", action = "GetAutoCompleteContent" }
			);

			routes.MapRoute(
			  "About",
			  "About",
			  new { controller = "StaticContent", action = "About" }
			);

			routes.MapRoute(
			  "HomePage",
			  "",
			  new { controller = "ViewPost", action = "ArchiveByMonthMostRecent" }
			);

			routes.MapRoute(
			  "RSSFeed",
			  "feed",
			  new { controller = "RSS", action = "Feed" }
			);

			// Even with this rule, I still had to do some configuration with my hosting environment to allow 404s to be processed here
			// (the default behaviour was to assume it was an unintentional response and to show a standard error page, these needed
			// redirecting back to here - by telling to display content from /NotFound in my case - to display the custom 404)
			routes.MapRoute(
			  "404Error",
			  "{*url}",
			  new { controller = "StaticContent", action = "ErrorPage404" }
			);
		}

		protected void Application_Start()
		{
			AreaRegistration.RegisterAllAreas();
			RegisterRoutes(RouteTable.Routes);
			ControllerBuilder.Current.SetControllerFactory(new ControllerFactory());
		}
	}

	public class RequestTimingApplication : System.Web.HttpApplication
	{
		private string _itemName, _responseHeaderName;
		public RequestTimingApplication(string itemName, string responseHeaderName)
		{
			if (string.IsNullOrWhiteSpace(itemName))
				throw new ArgumentException("Null/blank itemName specified");
			if (string.IsNullOrWhiteSpace(responseHeaderName))
				throw new ArgumentException("Null/blank responseHeaderName specified");

			this.BeginRequest += MvcApplication_BeginRequest;
			this.EndRequest += MvcApplication_EndRequest;

			_itemName = itemName;
			_responseHeaderName = responseHeaderName;
		}
		public RequestTimingApplication() : this("RequestStopwach", "X-RequestTime") { }

		private void MvcApplication_BeginRequest(object sender, EventArgs e)
		{
			var stopwatch = new Stopwatch();
			HttpContext.Current.Items[_itemName] = stopwatch;
			stopwatch.Start();
		}

		private void MvcApplication_EndRequest(object sender, EventArgs e)
		{
			var stopwatch = HttpContext.Current.Items[_itemName] as Stopwatch;
			if (stopwatch != null)
			{
				stopwatch.Stop();
				if (HttpContext.Current.Response.IsClientConnected)
				{
					// AddHeader will fail if the Client is no longer connected, while we've tried to ensure that this doesn't
					// happen there's no guarantees so wrap in a try..catch
					try
					{
						HttpContext.Current.Response.AddHeader(_responseHeaderName, stopwatch.Elapsed.ToString());
					}
					catch { }
				}
			}
			HttpContext.Current.Items.Remove(_itemName);
		}
	}
}
