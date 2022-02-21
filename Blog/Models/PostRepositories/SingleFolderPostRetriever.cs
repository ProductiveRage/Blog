using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BlogBackEnd.Models;
using FullTextIndexer.Common.Lists;
using FullTextIndexer.Core.Indexes.TernarySearchTree;
using Microsoft.Extensions.FileProviders;

namespace Blog.Models
{
    public sealed class SingleFolderPostRetriever : ISingleFolderPostRetriever
	{
		private readonly IEnumerable<IFileInfo> _files;
		public SingleFolderPostRetriever(IEnumerable<IFileInfo> files)
		{
            _files = files ?? throw new ArgumentNullException(nameof(files));
		}

		/// <summary>
		/// This will never return null nor contain any null entries
		/// </summary>
		public async Task<NonNullImmutableList<Post>> Get()
		{
			// The redirects set contains tuples From, To slugs (blank lines and those starting with a "#" are ignored, as are any that don't have any whitespace)
			const string redirectsFilename = "Redirects.txt";
			var redirectsFile = _files.FirstOrDefault(file => file.Name.Equals(redirectsFilename, StringComparison.OrdinalIgnoreCase));
			IEnumerable<Tuple<string, string>> redirects;
			if (redirectsFile == null)
				redirects = new List<Tuple<string, string>>();
			else
			{
				redirects = (await ReadFileContents(redirectsFile))
					.Replace("\r\n", "\n")
					.Replace("\r", "\n")
					.Split('\n')
					.Select(entry => entry.Trim())
					.Where(entry => (entry != "") && !entry.StartsWith("#") && entry.Any(c => char.IsWhiteSpace(c)))
					.Select(entry => new string(entry.Select(c => char.IsWhiteSpace(c) ? ' ' : c).ToArray()))
					.Select(entry => entry.Split(new[] { ' ' }, 2))
					.Select(values => Tuple.Create(values[0], values[1]));
			}

			// The relatedPostRelationships set contains a map of Post Id to Ids of related Posts (in the order that they should appear)
			const string relatedPostsFilename = "RelatedPosts.txt";
			var relatedPostsFile = _files.FirstOrDefault(file => file.Name.Equals(relatedPostsFilename, StringComparison.OrdinalIgnoreCase));
			var relatedPostRelationships = (relatedPostsFile == null)
				? new Dictionary<int, ImmutableList<int>>()
				: await ReadRedirects(relatedPostsFile);

			// There is similar data in the AutoSuggestedRelatedPosts.txt file but the manually-created RelatedPosts.txt should take precedence in cases
			// where Post Ids appear in both
			const string autoSuggestedRelatedPostsFilename = "AutoSuggestedRelatedPosts.txt";
			var autoSuggestedRelatedPostsFile = _files.FirstOrDefault(file => file.Name.Equals(autoSuggestedRelatedPostsFilename, StringComparison.OrdinalIgnoreCase));
			var autoSuggestedRelatedPostRelationships = (autoSuggestedRelatedPostsFile == null)
				? new Dictionary<int, ImmutableList<int>>()
				: await ReadRedirects(autoSuggestedRelatedPostsFile);

			// We can use this functionality from the FullTextIndexer to generate the Post slug (it will replace accented characters, normalise whitespace,
			// remove punctuation and lower case the content - all we need to do then is replace spaces with hypens)
			var stringNormaliser = DefaultStringNormaliser.Instance;
			var posts = new List<Post>();
			foreach (var file in _files.Where(file => file.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)))
			{
				if (file.Name.Equals(redirectsFilename, StringComparison.InvariantCultureIgnoreCase))
					continue;

				var fileSummary = TryToGetFileSummaryEntry(file.Name);
				if (fileSummary != null)
				{
					var fileContents = await ReadFileContents(file);
					var title = TryToGetTitle(fileContents);
					if (title != null)
					{
						// 2014-09-17 DWR: Titles such as "C# State Machines" were being converted into "c-state-machines" which isn't as descriptive as
						// I'd like, "c-sharp-state-machines" is better. The replacement is done for "C#" and "F#" (a space is required after the
						// replacement content otherwise the "sharp" gets rolled into the next word)
						var slugBase = title.Replace("C#", "c sharp ").Replace("F#", "f sharp ");
						var slug = stringNormaliser.GetNormalisedString(slugBase).Replace(' ', '-');
						var redirectsForPost = new NonNullOrEmptyStringList(
							redirects.Where(r => r.Item2 == slug).Select(r => r.Item1)
						);

						// On this pass, set every tag's NumberOfPosts value to one since we don't have enough data to know better. After all of the
						// posts have been loaded, this can be fixed before the method terminates.
						if (!relatedPostRelationships.TryGetValue(fileSummary.Id, out var relatedPosts))
							relatedPosts = null;
						if ((relatedPosts != null) || !autoSuggestedRelatedPostRelationships.TryGetValue(fileSummary.Id, out var autoSuggestedRelatedPosts))
						{
							// Only check the autoSuggestedRelatedPostRelationships if there was no relatedPostRelationships entry - this allows for posts
							// to be specified as having no suggestions (manually-specified or auto-suggested) by having an entry in the manually-specified
							// file that has the post id but zero suggestions.
							autoSuggestedRelatedPosts = null;
						}
						posts.Add(new Post(
							fileSummary.Id,
							fileSummary.PostDate,
							file.LastModified.DateTime,
							slug,
							redirectsForPost,
							title,
							fileSummary.IsHighlight,
							fileContents,
							relatedPosts ?? new ImmutableList<int>(),
							autoSuggestedRelatedPosts ?? new ImmutableList<int>(),
							fileSummary.Tags.Select(tag => new TagSummary(tag, 1)).ToNonNullImmutableList()
						));
					}
				}
			}

			var tagCounts = posts
				.SelectMany(post => post.Tags)
				.Select(tag => tag.Tag)
				.GroupBy(tag => tag, StringComparer.OrdinalIgnoreCase)
				.ToDictionary(groupedTag => groupedTag.Key, groupedTag => groupedTag.Count(), StringComparer.OrdinalIgnoreCase);
			return new NonNullImmutableList<Post>(posts.Select(post =>
				new Post(
					post.Id,
					post.Posted,
					post.LastModified,
					post.Slug,
					post.RedirectFromSlugs,
					post.Title,
					post.IsHighlight,
					post.MarkdownContent,
					post.RelatedPosts,
					post.AutoSuggestedRelatedPosts,
					post.Tags.Select(tag => new TagSummary(tag.Tag, tagCounts[tag.Tag])).ToNonNullImmutableList()
				)
			));
		}

		private static async Task<Dictionary<int, ImmutableList<int>>> ReadRedirects(IFileInfo redirectsFile)
		{
			return (await ReadFileContents(redirectsFile))
				.Replace("\r\n", "\n")
				.Replace("\r", "\n")
				.Split('\n')
				.Select(entry => entry.Trim())
				.Where(entry => (entry != "") && !entry.StartsWith("#") && entry.Contains(":"))
				.Select(entry =>
				{
					if (!int.TryParse(entry.Split(':').First(), out int sourcePostId))
						return default;
					var relatedPostIds = entry.Split(':').Skip(1).First().Split(',')
						.Select(commaSeparatedValue => int.TryParse(commaSeparatedValue, out int relatedPostId) ? (int?)relatedPostId : null)
						.Where(id => id != null)
						.Select(id => id.Value)
						.ToImmutableList();
					return (sourcePostId, relatedPostIds);
				})
				.Where(entry => entry != default)
				.ToDictionary(entry => entry.sourcePostId, entry => entry.relatedPostIds);
		}

		private static async Task<string> ReadFileContents(IFileInfo file)
		{
			if (file == null)
				throw new ArgumentNullException(nameof(file));

            using var stm = file.CreateReadStream();
			using var reader = new StreamReader(stm);
            return await reader.ReadToEndAsync();
        }

		private static string TryToGetTitle(string fileContents)
		{
			if (fileContents == null)
				throw new ArgumentNullException(nameof(fileContents));

			fileContents = fileContents.Trim();
			if (fileContents == "")
				return null;

			var breakPoint = fileContents.IndexOfAny(new[] { '\r', '\n' });
			if (breakPoint != -1)
				fileContents = fileContents.Substring(0, breakPoint).Trim().TrimStart('#').Trim();

			return fileContents;
		}

		private static FileSummaryEntry TryToGetFileSummaryEntry(string filename)
		{
			filename = (filename ?? "").Trim();
			if (filename == "")
				throw new ArgumentException("Null/empty file specified");

			if (!filename.EndsWith(".txt", StringComparison.InvariantCultureIgnoreCase))
				return null;

			filename = filename[0..^4];
			var segments = filename.Split(',');
			if (segments.Length < 8)
				return null;

            if (!int.TryParse(segments[0], out int id)
            || !int.TryParse(segments[1], out int year)
            || !int.TryParse(segments[2], out int month)
            || !int.TryParse(segments[3], out int date)
            || !int.TryParse(segments[4], out int hour)
            || !int.TryParse(segments[5], out int minute)
            || !int.TryParse(segments[6], out int second))
                return null;

            if (!DateTime.TryParse(String.Format("{0}-{1}-{2} {3}:{4}:{5}", year, month, date, hour, minute, second), out DateTime postDate))
                return null;

            bool isHighlight;
			if (segments[7] == "0")
				isHighlight = false;
			else if (segments[7] == "1")
				isHighlight = true;
			else
				return null;

			return new FileSummaryEntry(
				id,
				postDate,
				isHighlight,
				new NonNullOrEmptyStringList(
					segments.Skip(8).Select(t => t.Trim()).Where(t => t != "").Distinct()
				)
			);
		}

		private class FileSummaryEntry
		{
			public FileSummaryEntry(int id, DateTime postDate, bool isHighlight, NonNullOrEmptyStringList tags)
			{
				if (tags == null)
					throw new ArgumentNullException(nameof(tags));
				if (tags.Any(t => t.Trim() == ""))
					throw new ArgumentException("Blank tag specified");

				Id = id;
				PostDate = postDate;
				IsHighlight = isHighlight;
				Tags = tags;
			}
			public int Id { get; private set; }
			public DateTime PostDate { get; private set; }
			public NonNullOrEmptyStringList Tags { get; private set; }
			public bool IsHighlight { get; private set; }
		}
	}
}
