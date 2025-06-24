namespace url_shortener;

internal class Program
{
	// key = long url, value = set of short urls
	// production architecture - probably redis key-value store, with json obj string representing short urls
	// using redis allows for high volume concurrent reads/writes without worrying about thread safety in the application layer
	// it also allows you to move state out of the application layer, allowing for horizontal scalability
	// if we never wanted to scale this or persist to disk, while also supporting concurrency, ConcurrentDictionary is the better structure
	// if we expect 1000s of short urls to be mapped to a long url, a hashset or something with a index to help with removal is probably better than the list value
	private var longToShortMap = new Dictionary<string, List<string>>();

	// inverse data structure to ensure fast lookups in both directions
	// trade off is duplication of data, there is probably a more optimal way to do this like having 
	// multiple indexes on the table if it was sql, one clustered, one nonclustered
	// redis may have a similar idea/concept
	// because these are relatively small data items, storage/ram tradeoff may be acceptable,
	// but if it is a constraint, i'd need to figure out a more optimal way to store this data
	// you could also consider compression, or using protobufs instead of a json string to save on storage/ram costs
	// you could also consider using string pointers/string interning to make sure there is only a single instance of a given string in memory
	private var shortToLongMap = new Dictionary<string, string>();

	// these two related data structures are probably better living together in their own class 
	// with access functions that ensure they remain in sync

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
					// short in, remove it if it exists
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
			Console.WriteLine($"short urls exist for long url {longUrl}, generating an adding new short url");
			// this method will output generated shorturl
			GenerateShortAndStore(longUrl);
		}
		else
		{
			Console.WriteLine($"no short urls exist for long url {longUrl}, generating:");
			// these two branches share some logic, candidate for refactor
			GenerateShortAndStore(longUrl);
		}
	}

	private void GenerateShortAndStore(string longUrl)
	{
		(bool success, string shortUrl) = GenerateShortFromLong(longUrl);
		if (success)
		{
			if (longToShortMap.TryGetValue(longUrl, out List<string> shortUrls))
			{
				shortUrls.Add(shortUrl);
			}
			else 
			{
				longToShortMap[longUrl] = new List<string> {shortUrl};
			}
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
		// check if short url is in use
		if (shortToLongMap.TryGetValue(desiredShortUrl, out _))
		{
			Console.WriteLine($"desiredShortUrl: {desiredShortUrl} is already taken, generating new url");
			GenerateShortAndStore(longUrl);
		}
		else
		{
			// store long short mapping
			shortToLongMap[desiredShortUrl] = longUrl;
			if (longToShortMap.TryGetValue(longUrl, out List<string> shortUrls))
			{
				shortUrls.Add(desiredShortUrl);
			}
			else 
			{
				longToShortMap[longUrl] = new List<string> {desiredShortUrl};
			}
			Console.WriteLine($"result: successfully mapped desiredShortUrl: {desiredShortUrl} to long: {longUrl}")
		}

	}

	private void RemoveShortUrl(string shortUrl)
	{
		if (shortToLongMap.TryGetValue(shortUrl, out string longUrl))
		{
			shortToLongMap.Remove(shortUrl);
			if (longToShortMap.TryGetValue(longUrl, out List<string> shortUrls))
			{
				shortUrls.Remove(x => string.Equals(x, shortUrl));
			}
			else
			{
				Console.WriteLine($"internal server error: data structures in disagreement");
			}
		}
		else
		{
			Console.WriteLine($"result: did not find shorturl: {shortUrl}, no action");
		}

	}

	// this is the core short url generation logic
	// couple of options:
	// 1. hash the long using SHA-1, grab first 8 characters, check if exists
	// 	if there is a collision, add some constant string and rehash
	// 		repeat until there is no collisions
	// personally, not a huge fan of repeatedly hashing and appending strings, seems a bit resource intensive and complex
	//2. generate a guid, grab the first 8 (convienently before first dash), check if exists, 
	//	if there is a collision, just retry
	//	this is a good solution, less complex, doesn't require keeping track of a global set of available shorturls
	//	potentially less resource intensive
	//3.keep full set of urls in their own map or set, mark them as in use as they are used, check for unused ones
	//	this requires additional disk/ram, but makes the compution straight forward
	// i personally like the guid approach, avoids needing global state meaning we can scale horizontally and require less cordination across threads/processes
	private (bool, string) GenerateShortFromLong(string long)
	{
		// generate guid
		// truncate to 8
		// append to end of base url
		// check if exists
		// 	repeat if exists
		// store
	}
}
