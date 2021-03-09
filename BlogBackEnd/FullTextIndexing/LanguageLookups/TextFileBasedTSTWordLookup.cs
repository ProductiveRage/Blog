using System;
using System.Collections.Generic;
using System.IO;
using FullTextIndexer.Core.Indexes.TernarySearchTree;

namespace BlogBackEnd.FullTextIndexing.LanguageLookups
{
    public sealed class TextFileBasedTSTWordLookup : IWordLookup
	{
		private readonly FileInfo _file;
		private readonly IStringNormaliser _stringNormaliser;
		private readonly Lazy<TernarySearchTreeDictionary<bool>> _data;
		public TextFileBasedTSTWordLookup(FileInfo file, IStringNormaliser stringNormaliser)
		{
            _file = file ?? throw new ArgumentNullException(nameof(file));
			_stringNormaliser = stringNormaliser ?? throw new ArgumentNullException(nameof(stringNormaliser));
			_data = new Lazy<TernarySearchTreeDictionary<bool>>(
				GenerateLookup,
				true // isThreadSafe
			);
		}

		public bool IsValid(string word)
		{
			if (word == null)
				throw new ArgumentNullException(nameof(word));

            return _data.Value.TryGetValue(word, out bool _);
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
