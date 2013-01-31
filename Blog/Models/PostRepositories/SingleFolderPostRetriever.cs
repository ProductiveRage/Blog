using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BlogBackEnd.Models;
using Common.Lists;

namespace Blog.Models
{
	public class SingleFolderPostRetriever : ISingleFolderPostRetriever
	{
		private DirectoryInfo _folder;
		public SingleFolderPostRetriever(DirectoryInfo folder)
		{
			if (folder == null)
				throw new ArgumentNullException("folder");
			_folder = folder;
		}

		/// <summary>
		/// This will never return null nor contain any null entries
		/// </summary>
		public NonNullImmutableList<Post> Get()
		{
			var posts = new List<Post>();
			foreach (var file in _folder.EnumerateFiles("*.txt", SearchOption.AllDirectories))
			{
				var fileSummary = tryToGetFileSummaryEntry(file.Name);
				if (fileSummary != null)
				{
					var fileContents = readFileContents(file);
					var title = tryToGetTitle(fileContents);
					if (title != null)
					{
						posts.Add(new Post(
						  fileSummary.Id,
						  fileSummary.PostDate,
						  file.LastWriteTime,
						  title,
						  fileSummary.IsHighlight,
						  fileContents,
						  fileSummary.Tags
						));
					}
				}
			}
			return new NonNullImmutableList<Post>(posts);
		}

		private string readFileContents(FileInfo file)
		{
			if (file == null)
				throw new ArgumentNullException("file");

			using (var stm = file.OpenText())
			{
				return stm.ReadToEnd();
			}
		}

		private string tryToGetTitle(string fileContents)
		{
			if (fileContents == null)
				throw new ArgumentNullException("fileContent");

			fileContents = fileContents.Trim();
			if (fileContents == "")
				return null;

			var breakPoint = fileContents.IndexOf("\n");
			if (breakPoint != -1)
				fileContents = fileContents.Substring(0, breakPoint - 1).Trim();

			while (fileContents.StartsWith("#"))
				fileContents = fileContents.Substring(1);

			return fileContents;
		}

		private FileSummaryEntry tryToGetFileSummaryEntry(string filename)
		{
			filename = (filename ?? "").Trim();
			if (filename == "")
				throw new ArgumentException("Null/empty file specified");

			if (!filename.EndsWith(".txt", StringComparison.InvariantCultureIgnoreCase))
				return null;

			filename = filename.Substring(0, filename.Length - 4);
			var segments = filename.Split(',');
			if (segments.Length < 8)
				return null;

			int id, year, month, date, hour, minute, second;
			if (!int.TryParse(segments[0], out id)
			|| !int.TryParse(segments[1], out year)
			|| !int.TryParse(segments[2], out month)
			|| !int.TryParse(segments[3], out date)
			|| !int.TryParse(segments[4], out hour)
			|| !int.TryParse(segments[5], out minute)
			|| !int.TryParse(segments[6], out second))
				return null;

			DateTime postDate;
			if (!DateTime.TryParse(String.Format("{0}-{1}-{2} {3}:{4}:{5}", year, month, date, hour, minute, second), out postDate))
				return null;

			bool isHighlight;
			if (segments[7] == "0")
				isHighlight = false;
			else if (segments[7] == "1")
				isHighlight = true;
			else
				return null;

			var tags = segments.ToList();
			tags.RemoveRange(0, 8);

			return new FileSummaryEntry(
				id,
				postDate,
				isHighlight,
				tags.Select(t => t.Trim()).Where(t => t != "").ToNonNullImmutableList()
			);
		}

		private class FileSummaryEntry
		{
			private int _id;
			private DateTime _postDate;
			private bool _isHighlight;
			private NonNullImmutableList<string> _tags;
			public FileSummaryEntry(int id, DateTime postDate, bool isHighlight, NonNullImmutableList<string> tags)
			{
				if (tags == null)
					throw new ArgumentNullException("tags");
				if (tags.Any(t => t.Trim() == ""))
					throw new ArgumentException("Blank tag specified");

				_id = id;
				_postDate = postDate;
				_isHighlight = isHighlight;
				_tags = tags;
			}
			public int Id { get { return _id; } }
			public DateTime PostDate { get { return _postDate; } }
			public NonNullImmutableList<string> Tags { get { return _tags; } }
			public bool IsHighlight { get { return _isHighlight; } }
		}
	}
}
