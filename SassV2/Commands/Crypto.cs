using Discord.Commands;
using Newtonsoft.Json.Linq;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SassV2.Commands
{
	public class Crypto : ModuleBase<SocketCommandContext>
	{
		private CryptoTracker _crypto = new CryptoTracker();

		[Command("crypto list coins")]
		[SassCommand(
			name: "crypto list coins",
			desc: "lists the coins SASS supports getting information on",
			category: "Pointless"
		)]
		public async Task ListCoins()
		{
			var coins = Util.NaturalArrayJoin(
				_crypto
				.Coins
				.OrderBy(kv => kv.Value)
				.Select(kv => $"{kv.Value} ({kv.Key})"));

			await ReplyAsync("These coins are currently supported: " + coins);
		}

		[Command("crypto get price")]
		[SassCommand(
			name: "crypto get price",
			desc: "get the price of a cryptocurrency on Kraken",
			usage: "crypto get price <currency code or name>",
			category: "Pointless",
			example: "crypto get price bitcoin"
		)]
		public async Task GetPrice([Remainder] string args = "")
		{
			var coin = _crypto.FindCoin(args);
			if(coin == null)
			{
				await ReplyAsync("I don't know that cryptocurrency. Try `crypto list coins`.");
				return;
			}

			var price = await _crypto.GetCoinPrice(coin);
			await ReplyAsync($"Current price for {_crypto.Coins[coin]} ({coin}): {price.ToString("C2")}");
		}
	}

	/// <summary>
	/// Tracks prices for crypto assets we care about.
	/// Retrieves information from the Kraken API.
	/// </summary>
	internal class CryptoTracker
	{
		private Dictionary<string, TickerData> _tickerCache = new Dictionary<string, TickerData>();
		private Dictionary<string, double> _priceCache = new Dictionary<string, double>();
		private DateTime _lastPriceFetch = DateTime.MinValue;
		private ILogger _logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Maps currency codes to currency names.
		/// </summary>
		private Dictionary<string, string> _coins = new Dictionary<string, string>();

		private readonly TimeSpan PRICE_INTERVAL = TimeSpan.FromMinutes(10);
		private readonly TimeSpan TICKER_INTERVAL = TimeSpan.FromMinutes(60);
		// maximum number of latest ticker items to store
		private const int MAX_TICKER_ITEMS = 50;

		public Dictionary<string, string> Coins => _coins;

		public CryptoTracker()
		{
			var unitObj = JObject.Parse(File.ReadAllText("units.json"));
			foreach(var coin in unitObj["currency"]["crypto"])
			{
				_coins[coin["code"].Value<string>()] = coin["name"].Value<string>();
			}
		}

		/// <summary>
		/// Retrieves the current price of this asset.
		/// </summary>
		public async Task<double> GetCoinPrice(string coin)
		{
			if(_lastPriceFetch + PRICE_INTERVAL > DateTime.Now)
			{
				return _priceCache[coin];
			}

			var pairs = string.Join(",", _coins.Keys.Select(k => k + "USD"));
			// find internal names for asset pairs... XBTUSD -> XXBTZUSD?
			var internalMap = new Dictionary<string, string>();
			var pairUrl = "https://api.kraken.com/0/public/AssetPairs?pair=" + pairs;
			var pairResult = JObject.Parse(await Util.GetURLAsync(pairUrl));
			foreach(var obj in (pairResult["result"] as JObject))
			{
				internalMap[obj.Key] =
					obj.Value["altname"].Value<string>();
			}

			var apiUrl = "https://api.kraken.com/0/public/Ticker?pair=" + pairs;
			var result = JObject.Parse(await Util.GetURLAsync(apiUrl));
			foreach(var obj in (result["result"] as JObject))
			{
				// get 24hr vwap
				var newKey = internalMap[obj.Key];
				var coinName = newKey.Substring(0, newKey.Length - 3);
				_priceCache[coinName] = double.Parse(obj.Value["p"].ElementAt(1).Value<string>());
			}

			_lastPriceFetch = DateTime.Now;
			return _priceCache[coin];
		}

		/// <summary>
		/// Finds the currency code of the coin the user inputted, if it can be found.
		/// </summary>
		public string FindCoin(string input)
		{
			var clean = input.ToUpper().Trim();

			// check if it's a currency code verbatim
			if(_coins.ContainsKey(clean))
				return clean;

			// check if it's a coin name verbatim
			foreach(var val in _coins)
			{
				if(clean == val.Value.ToUpper())
					return val.Key;
			}

			// check if it's close to a coin name
			var names = _coins.OrderBy(kv => Util.LevenshteinDistance(clean, kv.Value.ToUpper()));
			if(Util.LevenshteinDistance(clean, names.First().Value.ToUpper()) < 4)
			{
				return names.First().Key;
			}

			// give up
			return null;
		}

		/// <summary>
		/// Gets the ticker data for a given coin currency code.
		/// </summary>
		public async Task<TickerData> GetTickerInformation(string coin)
		{
			if(!_coins.ContainsKey(coin))
			{
				throw new Exception($"Ticker info requested on coin {coin} but wasn't provided on setup.");
			}

			// check if there's any cached results and if cache is fresh
			if(
				_tickerCache.ContainsKey(coin) &&
				_tickerCache[coin].TimeFetched + TICKER_INTERVAL > DateTime.Now)
			{
				return _tickerCache[coin];
			}

			var apiUrl = $"https://api.kraken.com/0/public/OHLC?pair={coin}USD&interval={TICKER_INTERVAL.TotalMinutes}";
			var result = JObject.Parse(await Util.GetURLAsync(apiUrl));

			if(result["error"].HasValues)
			{
				_logger.Error("Kraken errors: " + string.Join(", ", result["error"].Values<string>()));
				throw new Exception("Failed to fetch Kraken API results: API error.");
			}

			var currentData = new TickerData
			{
				TimeFetched = DateTime.Now
			};

			var values = result["result"].First.TakeLast(MAX_TICKER_ITEMS);
			// take vwap price from each result
			currentData.Data = values.Select(v => double.Parse(v.ElementAt(5).Value<string>())).ToArray();
			_tickerCache[coin] = currentData;
			return _tickerCache[coin];
		}

		public class TickerData
		{
			public DateTime TimeFetched;
			public double[] Data;
		}
	}
}
