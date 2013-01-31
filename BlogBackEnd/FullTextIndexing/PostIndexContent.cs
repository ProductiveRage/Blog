using System;
using Common.Lists;
using FullTextIndexer.Indexes;

namespace BlogBackEnd.FullTextIndexing
{
	[Serializable]
	public class PostIndexContent
	{
		public PostIndexContent(IIndexData<int> searchIndex, NonNullOrEmptyStringList autoCompleteContent)
		{
			if (searchIndex == null)
				throw new ArgumentNullException("searchIndex");
			if (autoCompleteContent == null)
				throw new ArgumentNullException("autoCompleteContent");

			SearchIndex = searchIndex;
			AutoCompleteContent = autoCompleteContent;
		}

		/// <summary>
		/// This will never be null
		/// </summary>
		public IIndexData<int> SearchIndex { get; private set; }

		/// <summary>
		/// This will never be null
		/// </summary>
		public NonNullOrEmptyStringList AutoCompleteContent { get; private set; }
	}
}
