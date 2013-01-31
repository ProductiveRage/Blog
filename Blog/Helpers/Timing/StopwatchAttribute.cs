using System;
using System.Diagnostics;
using System.Web.Mvc;

namespace Blog.Helpers.Timing
{
	public class StopwatchAttribute : ActionFilterAttribute
	{
		private string _itemName, _responseHeaderName;
		public StopwatchAttribute(string itemName, string responseHeaderName)
		{
			if (string.IsNullOrWhiteSpace(itemName))
				throw new ArgumentException("Null/blank itemName specified");
			if (string.IsNullOrWhiteSpace(responseHeaderName))
				throw new ArgumentException("Null/blank responseHeaderName specified");

			_itemName = itemName;
			_responseHeaderName = responseHeaderName;
		}
		public StopwatchAttribute() : this("ActionStopwach", "X-ActionExecutionTime") { }

		public override void OnActionExecuting(ActionExecutingContext filterContext)
		{
			var stopwatch = new Stopwatch();
			filterContext.HttpContext.Items[_itemName] = stopwatch;
			stopwatch.Start();
		}

		public override void OnActionExecuted(ActionExecutedContext filterContext)
		{
			var stopwatch = filterContext.HttpContext.Items[_itemName] as Stopwatch;
			if (stopwatch != null)
			{
				stopwatch.Stop();
				if (filterContext.HttpContext.Response.IsClientConnected)
				{
					// AddHeader will fail if the Client is no longer connected, while we've tried to ensure that this doesn't
					// happen there's no guarantees so wrap in a try..catch
					try
					{
						filterContext.HttpContext.Response.AddHeader(_responseHeaderName, stopwatch.Elapsed.ToString());
					}
					catch { }
				}
			}
			filterContext.HttpContext.Items.Remove(_itemName);
		}
	}
}
