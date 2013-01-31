using System;
using System.Collections.Generic;
using System.IO;
using FullTextIndexer.Indexes.TernarySearchTree;

namespace BlogBackEnd.FullTextIndexing.LanguageLookups
{
	public class TextFileBasedTSTWordLookup : IWordLookup
	{
		private FileInfo _file;
		private IStringNormaliser _stringNormaliser;
		private Lazy<TernarySearchTreeDictionary<bool>> _data;
		public TextFileBasedTSTWordLookup(FileInfo file, IStringNormaliser stringNormaliser)
		{
			if (file == null)
				throw new ArgumentNullException("file");
			if (stringNormaliser == null)
				throw new ArgumentNullException("stringNormaliser");

			_file = file;
			_stringNormaliser = stringNormaliser;
			_data = new Lazy<TernarySearchTreeDictionary<bool>>(
				GenerateLookup,
				true // isThreadSafe
			);
		}

		public bool IsValid(string word)
		{
			if (word == null)
				throw new ArgumentNullException("word");

			bool value;
			return _data.Value.TryGetValue(word, out value);
		}

		private TernarySearchTreeDictionary<bool> GenerateLookup()
		{
			IEnumerable<string> words;
			using (var reader = File.OpenText(_file.FullName))
			{
				words = reader.ReadToEnd().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			}
			var data = new Dictionary<string, bool>(_stringNormaliser);
			foreach (var word in words)
			{
				if (!data.ContainsKey(word))
					data.Add(word, true);
			}
			return new TernarySearchTreeDictionary<bool>(data, _stringNormaliser);
		}
	}
}
