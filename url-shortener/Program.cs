namespace url_shortener;

internal class Program
{
	// key = long url, value = set of short urls
	// production architecture - probably redis key-value store, with json obj string representing short urls
	// using redis allows for high volume concurrent reads/writes without worrying about thread safety in the application layer
	// it also allows you to move state out of the application layer, allowing for horizontal scalability
	// if we never wanted to scale this or persist to disk, while also supporting concurrency, ConcurrentDictionary is the better structure
	private var longToShortMap = new Dictionary<string, List<string>>();

	// inverse data structure to ensure fast lookups in both directions
	// trade off is duplication of data, there is probably a more optimal way to do this like having 
	// multiple indexes on the table if it was sql, one clustered, one nonclustered
	// redis may have a similar idea/concept
	private var shortToLongMap = new Dictionary<string, string>();

    static void Main(string[] args)
    {
		// three user workflows
		// full length in, shortened url out
		// short url in, long url out if exists, or error
		// short url and long url in, user looking to reserve this short url
		// 	if already exists, return error
		// short url in, user requesting to delete it
		//
		// data validation
		// incoming long url is valid
		// shorturl unique part is 8 characters
		// 
		
		// todo, take from args and validate
		string validLongUrl = "https://google.com";
		bool longUrlInput = true;
		string validShortUrl = "https://tinyurl.com/01234567";
		bool shortUrlInput = true;

		string reqType = string.Empty;
		switch (reqType)
		{
			case "GET":
				// todo - short in, long out
			case "POST":
				// if only long in
				if (longUrlInput && !shortUrlInput)
				{
					GetShortUrlsFromLong(validLongUrl);
				}
				else if (longUrlInput && shortUrlInput)
				{
					// todo - short and long in, attempt to reserve short url if not in use
					// otherwise generate a new short one and return it
				}
				else
				{
					Console.WriteLine($"bad request: invalid user input, expected both short and long, or just long, use GET if you want to get associated long from short")
				};
			case "DELETE":
				if (shortUrlInput)
				{
					// todo - short in, remove it if it exists
				}
				else
				{
					Console.WriteLine($"bad request: expecting short url input in order to delete")
				}
		}

		return;
    }

	private void GetShortUrlsFromLong(string longUrl) 
	{
		if (longToShortMap.TryGetValue(longUrl, out List<string> shortUrls))
		{
			Console.WriteLine($"short urls exist for long url {longUrl}:")
			Console.WriteLine(JsonConvert.SerializeObject(shortUrls));
		}
		else
		{
			Console.WriteLine($"no short urls exist for long url {longUrl}, generating:");
			(bool success, string shortUrl) = GenerateShortFromLong(long);
			if (success)
			{
				longToShortMap[longUrl] = shortUrl;
			}
			else
			{
				// could be we used up our entire set of 8 character shortened urls, or some lower level error 
				// prevented us from generated the unique id
				Console.WriteLine($"internal service error - unable to generate shortened url, please try again later");
			}
		}
	}

	private void GetLongUrlFromShort(string shortUrl)
	{

	}

	private (bool, string) GenerateShortFromLong(string long)
	{
		// base url
		// 	todo - optimization, only store the unqiue bits of the shortened url,
		// 	keep the base url as a const and concat strings before returning to user
		// core functionality
		// avoid collisions
		// hasing algo
		//

	}
}
