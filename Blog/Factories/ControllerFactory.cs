using System;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.SessionState;
using Blog.Controllers;
using Blog.Misc;
using BlogBackEnd.Caching;

namespace Blog.Factories
{
	public class ControllerFactory : IControllerFactory
	{
		public IController CreateController(RequestContext requestContext, string controllerName)
		{
			if (requestContext == null)
				throw new ArgumentNullException("requestContext");
			if (controllerName == null)
				throw new ArgumentNullException("controllerName");

			switch (controllerName)
			{
				case "CSS":
					return new CSSController();

				case "Search":
					return new SearchController(
						new PostRepositoryFactory(requestContext.HttpContext).Get(),
						new PostIndexerFactory(requestContext.HttpContext).Get(),
						Constants.CanonicalLinkBase,
						Constants.GoogleAnalyticsId,
						GetLongTermCache(requestContext)
					);

				case "ViewPost":
					return new ViewPostController(
						new PostRepositoryFactory(requestContext.HttpContext).Get(),
						Constants.CanonicalLinkBase,
						Constants.GoogleAnalyticsId,
						Constants.DisqusShortName,
						GetLongTermCache(requestContext)
					);

				case "StaticContent":
					return new StaticContentController();

				default:
					throw new Exception("Unsupported Controller: " + controllerName);
			}
		}

		private ICache GetLongTermCache(RequestContext requestContext)
		{
			if (requestContext == null)
				throw new ArgumentNullException("requestContext");

			return new ASPNetCacheCache(
				requestContext.HttpContext.Cache,
				TimeSpan.FromDays(1)
			);
		}

		public SessionStateBehavior GetControllerSessionBehavior(RequestContext requestContext, string controllerName)
		{
			return SessionStateBehavior.Default;
		}

		public void ReleaseController(IController controller) { }
	}
}
