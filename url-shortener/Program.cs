namespace url_shortener;

internal class Program
{
	// key = long url, value = set of short urls
	// production architecture - probably redis key-value store, with json obj string representing short urls
	// using redis allows for high volume concurrent reads/writes without worrying about thread safety in the application layer
	// it also allows you to move state out of the application layer, allowing for horizontal scalability
	// if we never wanted to scale this or persist to disk, while also supporting concurrency, ConcurrentDictionary is the better structure
	// if we expect 1000s of short urls to be mapped to a long url, a hashset or something with a index to help with removal is probably better than the list value
	private static Dictionary<string, List<string>> longToShortMap = new Dictionary<string, List<string>>();

	// inverse data structure to ensure fast lookups in both directions
	// trade off is duplication of data, there is probably a more optimal way to do this like having 
	// multiple indexes on the table if it was sql, one clustered, one nonclustered
	// redis may have a similar idea/concept
	// because these are relatively small data items, storage/ram tradeoff may be acceptable,
	// but if it is a constraint, i'd need to figure out a more optimal way to store this data
	// you could also consider compression, or using protobufs instead of a json string to save on storage/ram costs
	// you could also consider using string pointers/string interning to make sure there is only a single instance of a given string in memory
	private static Dictionary<string, string> shortToLongMap = new Dictionary<string, string>();

	// these two related data structures are probably better living together in their own class 
	// with access functions that ensure they remain in sync
	//

	//example usage:
	// dotnet run "POST" "" "https://google.long" -> generates a unique short from long
	// dotnet run "POST" "https://roburl.com/01234567" "https://google.long" -> trys to use the requested short, generates a new one if already in use
	// dotnet run "GET" "https://roburl.com/01234567" "" -> gets long from short
	// dotnet run "DELETE" "https://roburl.com/01234567" "" -> trys to remove the short from usage
    static void Main(string[] args)
    {
		// take from args and validate
		// probably would want a data validation layer to make sure you have valid urls
		if (args.Length == 0)
		{
			// run test scenario...
			return;
		}

		//
		string validLongUrl = args[2];
		string validShortUrl = args[1];
		string reqType = args[0];
		foreach (var arg in args)
		{
			Console.WriteLine(arg);
		}

		RunProgram(reqType, validShortUrl, validLongUrl);
    }

	// main program logic, kinda not great for unit testing because results are written to console..
	public static void RunProgram(string reqType, string validShortUrl, string validLongUrl)
	{
		bool shortUrlInput = !string.IsNullOrWhiteSpace(validShortUrl);
		bool longUrlInput = !string.IsNullOrWhiteSpace(validLongUrl);
		switch (reqType)
		{
			case "GET":
				// short in, long out
				if (shortUrlInput)
				{
					GetLongUrlFromShort(validShortUrl);
				}
				break;
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
				break;
			case "DELETE":
				if (shortUrlInput)
				{
					// short in, remove it if it exists
					RemoveShortUrl(validShortUrl);
				}
				else
				{
					Console.WriteLine($"bad request: expecting short url input in order to delete");
				}
				break;
			default:
				Console.WriteLine($"unknown req type of: {reqType}");
				break;
		}

	}

	private static void GetShortUrlsFromLong(string longUrl) 
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

	private static void GenerateShortAndStore(string longUrl)
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

	private static void GetLongUrlFromShort(string shortUrl)
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

	private static void TryReserveSpecificShort(string desiredShortUrl, string longUrl)
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
			Console.WriteLine($"result: successfully mapped desiredShortUrl: {desiredShortUrl} to long: {longUrl}");
		}

	}

	private static void RemoveShortUrl(string shortUrl)
	{
		if (shortToLongMap.TryGetValue(shortUrl, out string longUrl))
		{
			shortToLongMap.Remove(shortUrl);
			if (longToShortMap.TryGetValue(longUrl, out List<string> shortUrls))
			{
				shortUrls.Remove(shortUrl);
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
	//	this requires additional disk/ram, but makes the compution straight forward, so you can make that tradeoff if you want
	//	but if you go into longer than 8 character short urls, the memory complexity increases significantly
	// i personally like the guid approach, avoids needing global state meaning we can scale horizontally and require less cordination across threads/processes
	private static (bool, string) GenerateShortFromLong(string _)
	{
		const string baseUrl = "https://roburl.com/";

		// probably want to put a max attempts here in case there is a bug, wouldnt want an infinite loop
		while(true)
		{
			string g = Guid.NewGuid().ToString("N").Substring(0, 8);
			string url = baseUrl + g;
			if (!shortToLongMap.ContainsKey(url))
			{
				return (true, url);
			}
		}
	}
}
