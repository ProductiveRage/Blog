using System;
using Markdig;
using Markdig.Parsers.Inlines;
using Markdig.Renderers;
using Markdig.Renderers.Html.Inlines;
using Markdig.Syntax.Inlines;

namespace BlogBackEnd.Models
{
    /// <summary>
    /// Helper class for transforming Markdown into HTML
    /// </summary>
    public static class MarkdownTransformations
	{
		private static readonly MarkdownPipeline _markdigPipeline = new MarkdownPipelineBuilder()
			.UseSingleAsteriskBold()
			.UseSoftlineBreakAsHardlineBreak()
			.Build();

		public static string ToHtml(string markdown)
		{
			if (markdown == null)
				throw new ArgumentNullException("text");

			return Markdown.ToHtml(markdown, _markdigPipeline);
		}

		/// <summary>
		/// This will enable single asterisks to indicate bold text (which is not part of the CommonMark specification that Markdig implements by default)
		/// </summary>
		private static MarkdownPipelineBuilder UseSingleAsteriskBold(this MarkdownPipelineBuilder pipeline)
		{
			pipeline.Extensions.AddIfNotAlready<SingleAsteriskBoldExtension>();
			return pipeline;
		}

		private sealed class SingleAsteriskBoldExtension : IMarkdownExtension
		{
			public void Setup(MarkdownPipelineBuilder pipeline) => pipeline.InlineParsers.AddIfNotAlready<EmphasisInlineParser>();

			public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
			{
				var htmlRenderers = (renderer as HtmlRenderer)?.ObjectRenderers;
				if (htmlRenderers is null)
					return;

				if (!htmlRenderers.TryFind<EmphasisInlineRenderer>(out var emphasisInlineRenderer))
				{
					emphasisInlineRenderer = new EmphasisInlineRenderer();
					htmlRenderers.Add(emphasisInlineRenderer);
				}
				emphasisInlineRenderer.GetTag = GetTagName;
			}

			private static string GetTagName(EmphasisInline obj)
			{
                // The default configuration for EmphasisInlineRenderer asserts that the DelimiterCount will less than or equals to two and so we can make the same assumption here
                // ^ See https://github.com/xoofx/markdig/blob/e523dfd7f490fb3ee6f35b2a566f4d8d5710e856/src/Markdig/Renderers/Html/Inlines/EmphasisInlineRenderer.cs#L60
                //
                // 2021-03-08 DWR: I previousld used "MarkdownSharp" to translate the markdown files into HTML, which supported single-underscore-wrapping for italic and single-asterisk-wrapping for bold. However, this isn't
                // part of the CommonMark specification that Markdig (that I now use supports and so we need to do this). We can ignore the DelimiterCount value and just look at the DelimiterChar when deciding what to do.
                return obj.DelimiterChar switch
                {
                    '*' => "strong",
                    '_' => "em",
                    _ => null,
                };
            }
		}
	}
}