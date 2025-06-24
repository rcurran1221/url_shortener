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
	// because these are relatively small data items, storage/ram tradeoff may be acceptable,
	// but if it is a constraint, i'd need to figure out a more optimal way to store this data
	// you could also consider compression, or using protobufs instead of a json string to save on storage/ram costs
	private var shortToLongMap = new Dictionary<string, string>();

    static void Main(string[] args)
    {
		// data validation
		// incoming long url is valid
		
		// todo, take from args and validate
		string validLongUrl = "https://google.com";
		bool longUrlInput = true;
		string validShortUrl = "https://tinyurl.com/01234567";
		bool shortUrlInput = true;

		string reqType = string.Empty;
		switch (reqType)
		{
			case "GET":
				// short in, long out
				if (shortUrlInput)
				{
					GetLongUrlFromShort(validShortUrl);
				}
			case "POST":
				// if only long in
				// generate new short from long
				if (longUrlInput && !shortUrlInput)
				{
					GetShortUrlsFromLong(validLongUrl);
				}
				else if (longUrlInput && shortUrlInput)
				{
					// short and long in, attempt to reserve short url if not in use
					// otherwise generate a new short one and return it
					TryReserveSpecificShort(validShortUrl, validLongUrl);
				}
				else
				{
					Console.WriteLine($"bad request: invalid user input, expected both short and long, or just long, use GET if you want to get associated long from short");
				}
			case "DELETE":
				if (shortUrlInput)
				{
					// todo - short in, remove it if it exists
					RemoveShortUrl(validShortUrl);
				}
				else
				{
					Console.WriteLine($"bad request: expecting short url input in order to delete")
				}
			default:
				Console.WriteLine($"unknown req type of: {reqType}");
		}

		return;
    }

	private void GetShortUrlsFromLong(string longUrl) 
	{
		if (longToShortMap.TryGetValue(longUrl, out List<string> shortUrls))
		{
			Console.WriteLine($"short urls exist for long url {longUrl}:")
			Console.WriteLine($"result: {JsonConvert.SerializeObject(shortUrls)}");
		}
		else
		{
			Console.WriteLine($"no short urls exist for long url {longUrl}, generating:");
			GenerateShortAndStore(longUrl);
		}
	}

	private void GenerateShortAndStore(string longUrl)
	{
		(bool success, string shortUrl) = GenerateShortFromLong(longUrl);
		if (success)
		{
			longToShortMap[longUrl] = shortUrl;
			shortToLongMap[shortUrl] = longUrl;
			Console.WriteLine($"successfully generated short: {shortUrl} from {longUrl}");
			Console.WriteLine($"result: {shortUrl}"); 
		}
		else
		{
			// could be we used up our entire set of 8 character shortened urls, or some lower level error 
			// prevented us from generated the unique id
			Console.WriteLine($"internal service error - unable to generate shortened url, please try again later");
		}
	}

	private void GetLongUrlFromShort(string shortUrl)
	{
		if (shortToLongMap.TryGetValue(shortUrl, out string longUrl))
		{
			Console.WriteLine($"received long: {longUrl} from short: {shortUrl}");
		}
		else
		{
			Console.WriteLine($"no long found for short: {shortUrl}");
		}
	}

	private void TryReserveSpecificShort(string desiredShortUrl, string longUrl)
	{
		if (shortToLongMap.TryGetValue(desiredShortUrl, out _))
		{
			Console.WriteLine($"desiredShortUrl: {desiredShortUrl} is already taken, generating new url");
			GenerateShortAndStore(longUrl);
		}
		else
		{
			shortToLongMap[desiredShortUrl] = longUrl;

		}

	}

	private void RemoveShortUrl(string shortUrl)
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
