namespace BlogBackEnd.Caching
{
    public interface ICache
	{
		/// <summary>
		/// The getter will return null if there is no cached data matching the specified key. The setter will only write the data if the key is not already present in the data
		/// (so that the cache can implement its own expiration handling and callers can make push requests to the data without having to worry about checking whether it's already
		/// there or not - if a caller really wants to overwrite any present data, the Remove method may be called first). Both getter and setter will throw an exception for a null
		/// or empty key. The setter will throw an exception if a null value is specified.
		/// </summary>
		object this[string key] { get; set; }

		/// <summary>
		/// This will do nothing if the key is not present in the cache. It will throw an exception for an null or empty key.
		/// </summary>
		void Remove(string key);
	}
}