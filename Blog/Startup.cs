using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Blog.Controllers;
using Blog.Factories;
using Blog.Misc;
using BlogBackEnd.Caching;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Blog
{
    internal class Startup
	{
		private readonly IWebHostEnvironment _environment;
		public Startup(IWebHostEnvironment environment)
		{
			if (environment == null)
				throw new ArgumentNullException("environment");

			_environment = environment;
		}

		public void ConfigureServices(IServiceCollection services)
		{
			services
				.AddSingleton(new PostRepositoryFactory(_environment).Get())
				.AddSingleton(new PostIndexerFactory(_environment.ContentRootFileProvider).Get())
				.AddSingleton<ICache>(new ConcurrentDictionaryCache(TimeSpan.FromDays(1)))
				.AddSingleton(_environment.ContentRootFileProvider)
				.AddSingleton(new SiteConfiguration(
					Constants.CanonicalLinkBase,
					Constants.GoogleAnalyticsId,
					Constants.DisqusShortName,
					Constants.TwitterUserName,
					Constants.TwitterImage,
					maximumNumberOfPostsToPublishInRssFeed: 10
				))
				.AddResponseCompression(options => options.ExcludedMimeTypes = new[] { "image/jpg", "image/jpeg" }) // No point compressing JPEGs, since they're already compressed
				.AddMvc(option => option.EnableEndpointRouting = false);
		}

		private static readonly Random _errorFileRandomSuffix = new();
		public void Configure(IApplicationBuilder app)
		{
			app
				.UseStatusCodePagesWithReExecute("/NotFound")
				.UseExceptionHandler(LogError)
				.UseMiddleware<SpecificStaticFileTypesMiddleware>()
				.UseResponseCompression()
				.UseMvc(DefineRoutes)
				.UseStaticFiles();
		}

		private void LogError(IApplicationBuilder app)
		{
			app.Run(async context =>
			{
				var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
				if (exceptionHandlerPathFeature?.Error is not null)
				{
					try
					{
						var errorFolder = new DirectoryInfo(_environment.ContentRootFileProvider.GetFileInfo("/App_Data/Errors").PhysicalPath);
						if (!errorFolder.Exists)
							errorFolder.Create();

						string errorFilename;
						lock (_errorFileRandomSuffix)
						{
							errorFilename = Path.Combine(
								errorFolder.FullName,
								"error " + DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss") + " " + _errorFileRandomSuffix.Next() + ".log"
							);
						}
						await File.WriteAllTextAsync(
							errorFilename,
							string.Format(
								"{0} {1}{2}{2}{3}",
								DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
								exceptionHandlerPathFeature?.Error.Message,
								Environment.NewLine,
								exceptionHandlerPathFeature?.Error.StackTrace
							)
						);
					}
					catch { }
				}
				await context.Response.WriteAsync("Oops! Something has gone terribly wrong!");
			});
		}

		private static void DefineRoutes(IRouteBuilder routes)
		{
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
				"ArchiveByTitle",
				"Archive/All",
				new { controller = "ViewPost", action = "ArchiveByTitle" }
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

			// Note: This route must be defined before ArchiveBySlug, otherwise that route will capture any routes that can't be matched (also note that we need this route
			// AND UseStatusCodePagesWithReExecute("/NotFound") in the Configure method)
			routes.MapRoute(
				"404ErrorPage",
				"NotFound",
				new { controller = "StaticContent", action = "ErrorPage404" }
			);

			routes.MapRoute(
				"ArchiveBySlug",
				"{slug}",
				new { controller = "ViewPost", action = "ArchiveBySlug" }
			);
		}

		private sealed class SpecificStaticFileTypesMiddleware
		{
			private static readonly HashSet<string> _extensionsToServeDirectly = new[] { "bmp", "gif", "ico", "jpg", "js", "png", "xslt" }.ToHashSet(StringComparer.OrdinalIgnoreCase);

			private readonly RequestDelegate _next;
			private readonly IWebHostEnvironment _env;
			public SpecificStaticFileTypesMiddleware(RequestDelegate next, IWebHostEnvironment env)
			{
				_next = next;
				_env = env;
			}

			public async Task Invoke(HttpContext context)
			{
				var extension = context.Request.Path.Value.Split('.').Last();
				if (!_extensionsToServeDirectly.Contains(extension))
				{
					await _next(context);
					return;
				}

				var file = _env.ContentRootFileProvider.GetFileInfo(context.Request.Path.Value);
				await context.Response.Body.WriteAsync(File.ReadAllBytes(file.PhysicalPath));
			}
		}
	}
}