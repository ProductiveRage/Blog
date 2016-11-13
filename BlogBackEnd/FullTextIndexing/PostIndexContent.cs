using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using FullTextIndexer.Common.Lists;
using FullTextIndexer.Core.Indexes;
using FullTextIndexer.Core.TokenBreaking;

namespace BlogBackEnd.FullTextIndexing
{
	[Serializable]
	public class PostIndexContent : ISerializable
	{
		private readonly IIndexData<int> _searchIndex;
		public PostIndexContent(IIndexData<int> searchIndex, NonNullOrEmptyStringList autoCompleteContent)
		{
			if (searchIndex == null)
				throw new ArgumentNullException("searchIndex");
			if (autoCompleteContent == null)
				throw new ArgumentNullException("autoCompleteContent");

			_searchIndex = searchIndex;
			AutoCompleteContent = autoCompleteContent;
		}

		private const string SearchIndexIsCustomSerialisedName = "BlogBackEnd.FullTextIndexing:SearchIndexIsCustomSerialised";
		private const string SearchIndexName = "BlogBackEnd.FullTextIndexing:SearchIndex";
		private const string AutoCompleteContentName = "BlogBackEnd.FullTextIndexing:AutoCompleteContent";
		protected PostIndexContent(SerializationInfo info, StreamingContext context)
		{
			if (info == null)
				throw new ArgumentNullException("info");

			var searchIndexData = (byte[])info.GetValue(SearchIndexName, typeof(byte[]));
			if (info.GetBoolean(SearchIndexIsCustomSerialisedName))
			{
				using (var memoryStream = new MemoryStream(searchIndexData))
				{
					_searchIndex = IndexDataSerialiser<int>.Deserialise(memoryStream);
				}
			}
			else
				_searchIndex = Deserialise<IIndexData<int>>(searchIndexData);

			AutoCompleteContent = Deserialise<NonNullOrEmptyStringList>(
				(byte[])info.GetValue(AutoCompleteContentName, typeof(byte[]))
			);
		}

		void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
		{
			if (info == null)
				throw new ArgumentNullException("info");

			var customSerialisableIndexData = _searchIndex as IndexData<int>;
			if (customSerialisableIndexData == null)
			{
				info.AddValue(SearchIndexIsCustomSerialisedName, false);
				info.AddValue(SearchIndexName, Serialise(_searchIndex));
			}
			else
			{
				info.AddValue(SearchIndexIsCustomSerialisedName, true);
				using (var memoryStream = new MemoryStream())
				{
					IndexDataSerialiser<int>.Serialise(customSerialisableIndexData, memoryStream);
					info.AddValue(SearchIndexName, memoryStream.ToArray());
				}
			}

			info.AddValue(AutoCompleteContentName, Serialise(AutoCompleteContent));
		}

		/// <summary>
		/// This will never return null. It will raise an exception for a null or blank term.
		/// </summary>
		public NonNullImmutableList<IndexData_Extensions_PartialMatches.WeightedEntryWithTerm<int>> Search(string term)
		{
			if (string.IsNullOrWhiteSpace(term))
				throw new ArgumentException("Null/blank term specified");

			return _searchIndex.GetPartialMatches(
				term,
				new WhiteSpaceExtendingTokenBreaker(
					new ImmutableList<char>(new[] { '<', '>', '[', ']', '(', ')', '{', '}', '.', ',' }),
					new WhiteSpaceTokenBreaker()
				)
			);
		}

		/// <summary>
		/// This will never be null
		/// </summary>
		public NonNullOrEmptyStringList AutoCompleteContent { get; private set; }

		private byte[] Serialise(object data)
		{
			if (data == null)
				throw new ArgumentNullException("data");

			using (var memoryStream = new MemoryStream())
			{
				new BinaryFormatter().Serialize(memoryStream, data);
				return memoryStream.ToArray();
			}
		}

		private T Deserialise<T>(byte[] data)
		{
			if (data == null)
				throw new ArgumentNullException("data");

			using (var memoryStream = new MemoryStream(data))
			{
				return (T)new BinaryFormatter().Deserialize(memoryStream);
			}
		}
	}
}
