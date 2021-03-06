﻿using System.Threading.Tasks;

namespace BlogBackEnd.Models
{
    public interface IRetrievePostSlugs
	{
		/// <summary>
		/// This will never return null or blank, it will raise an exception if the id is invalid or if otherwise unable to satisfy the request
		/// </summary>
		Task<string> GetSlug(int postId);
	}
}