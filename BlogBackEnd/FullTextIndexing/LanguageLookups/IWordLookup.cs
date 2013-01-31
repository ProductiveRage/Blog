namespace BlogBackEnd.FullTextIndexing.LanguageLookups
{
	public interface IWordLookup
	{
		/// <summary>
		/// This will perform a lookup in source data as to whether a specified string is a valid word. The logic will depend upon the implementation (there
		/// may be different instances for different languages or different string comparers). This will throw an exception for null input.
		/// </summary>
		bool IsValid(string word);
	}
}
