using System;
using System.IO;
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
						IsLocalHost(requestContext) ? null : Constants.GoogleAnalyticsId,
						GetPostContentCache(requestContext)
					);

				case "RSS":
					return new RSSController(
						new PostRepositoryFactory(requestContext.HttpContext).Get(),
						10, // maximumNumberOfPostsToPublish
						GetPostContentCache(requestContext)
					);

				case "ViewPost":
					return new ViewPostController(
						new PostRepositoryFactory(requestContext.HttpContext).Get(),
						Constants.CanonicalLinkBase,
						IsLocalHost(requestContext) ? null : Constants.GoogleAnalyticsId,
						Constants.DisqusShortName,
						GetPostContentCache(requestContext)
					);

				case "StaticContent":
					return new StaticContentController();

				default:
					throw new Exception("Unsupported Controller: " + controllerName);
			}
		}

		private ICache GetPostContentCache(RequestContext requestContext)
		{
			if (requestContext == null)
				throw new ArgumentNullException("requestContext");

			return new ASPNetCacheCache(
				requestContext.HttpContext.Cache,
				TimeSpan.FromDays(1)
			);
		}

		private bool IsLocalHost(RequestContext requestContext)
		{
			if (requestContext == null)
				throw new ArgumentNullException("requestContext");

			var httpContext = requestContext.HttpContext;
			if (httpContext == null)
				return false;
			var request = httpContext.Request;
			if (requestContext == null)
				return false;
			var url = request.Url;
			if (url == null)
				return false;
			return url.Host.Equals("localhost", StringComparison.InvariantCultureIgnoreCase);
		}

		public SessionStateBehavior GetControllerSessionBehavior(RequestContext requestContext, string controllerName)
		{
			return SessionStateBehavior.Default;
		}

		public void ReleaseController(IController controller) { }
	}
}
