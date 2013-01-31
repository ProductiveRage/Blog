namespace BlogBackEnd.FullTextIndexing.CachingPostIndexers
{
	public interface IPostIndexCache
	{
		/// <summary>
		/// This will return null if unable to deliver the data
		/// </summary>
		CachedPostIndexContent TryToRetrieve();

		/// <summary>
		/// If an entry already exists in the cache, it will be overwritten. It will throw an exception for a null data reference.
		/// </summary>
		void Store(CachedPostIndexContent data);
	}
}
