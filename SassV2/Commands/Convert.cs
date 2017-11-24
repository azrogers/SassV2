using Discord.Commands;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SassV2.Commands
{
	/// <summary>
	/// Convert one unit to another.
	/// </summary>
	public class ConvertCommand : ModuleBase<SocketCommandContext>
	{
		private UnitConverter _unitConverter;

		public ConvertCommand(DiscordBot bot)
		{
			_unitConverter = new UnitConverter(bot);
		}

		// convert commands used to be one single command, so we need to remind people if they try to use that one first
		[SassCommand(
			name: "convert",
			hidden: true)]
		[Command("convert")]
		[Priority(1)]
		public async Task ConvertDummy([Remainder] string args = "")
		{
			await ReplyAsync("The 'convert' command has been split into 'convert unit', 'convert currency', and 'convert timezone'.");
		}

		// convert measurement units (distance, weight, mass, etc)
		[SassCommand(
			names: new string[] { "convert unit", "unit convert", "convert units" },
			desc: "Convert one unit to another unit.",
			usage: "convert unit <thing> (to|in|at|as|=) <thing>",
			example: "convert unit 5 lbs to kg",
			category: "Useful")]
		[Command("convert unit")]
		[Alias("unit convert", "convert units")]
		[Priority(2)]
		public async Task ConvertUnits([Remainder] string args)
		{
			try
			{
				var result = _unitConverter.ConvertUnit(args);
				await ReplyAsync(result);
			}
			catch(CommandException ex)
			{
				await ReplyAsync(ex.Message);
			}
		}

		// convert currency units (USD, EUR, GBP, etc)
		[SassCommand(
			names: new string[] { "convert currency", "currency convert" },
			desc: "Convert one unit of currency to another unit.",
			usage: "convert currency <from> (to|in|at|as|=) <to>",
			example: "convert currency 50 usd to gbp",
			category: "Useful")]
		[Command("convert currency")]
		[Alias("currency convert")]
		[Priority(2)]
		public async Task ConvertCurrency([Remainder] string args)
		{
			try
			{
				var result = await _unitConverter.ConvertCurrency(args);
				await ReplyAsync(result);
			}
			catch(CommandException ex)
			{
				await ReplyAsync(ex.Message);
			}
		}

		// convert timezones (EST, PST, EDT, etc)
		[SassCommand(
			names: new string[] { "convert timezone", "timezone convert" },
			desc: "Convert one time to another time in a different timezone.",
			usage: "convert timezone <time> <timezone> (to|in|at|as|=) <timezone>",
			example: "convert timezone 5am EST to GMT",
			category: "Useful"
		)]
		[Command("convert timezone")]
		[Alias("timezone convert")]
		[Priority(2)]
		public async Task ConvertTimezone([Remainder] string args)
		{
			try
			{
				var result = _unitConverter.ConvertTimezone(args);
				await ReplyAsync(result);
			}
			catch(CommandException ex)
			{
				await ReplyAsync(ex.Message);
			}
		}
	}

	/// <summary>
	/// Actually performs unit conversion.
	/// </summary>
	public class UnitConverter
	{
		// "pivots" divide the statement into multiple parts
		// 200 meters *to* inches is divided into ["200 meters", "inches"], making it easier to parse out the units
		private string[] _pivotWords = { "to", "in", "at", "as", "=" };
		private Regex _timeRegex = new Regex(@"(\d+(:\d+)?)(\s+)?(AM|PM)?", RegexOptions.IgnoreCase);
		private DiscordBot _bot;

		private Dictionary<string, string> _unitTypes = new Dictionary<string, string>();
		private Dictionary<string, double> _unitValues = new Dictionary<string, double>();
		private Dictionary<string, string[]> _unitFormatting = new Dictionary<string, string[]>();
		private Dictionary<string, string> _unitAliases = new Dictionary<string, string>();

		private Dictionary<string, string> _currencyNames = new Dictionary<string, string>();
		private Dictionary<string, string[]> _currencyFormatting = new Dictionary<string, string[]>();
		private Dictionary<string, string> _currencySymbols = new Dictionary<string, string>();

		private Dictionary<string, string> _timezoneNames = new Dictionary<string, string>();
		private Dictionary<string, string> _timezoneFormatting = new Dictionary<string, string>();
		private Dictionary<string, string> _timezoneOffsets = new Dictionary<string, string>();

		public UnitConverter(DiscordBot bot)
		{
			_bot = bot;
			var unitObj = JObject.Parse(File.ReadAllText("units.json"));

			// read units
			var units = unitObj["units"] as JObject;
			foreach(var category in units.Properties().Select(p => p.Name))
			{
				// add default unit
				AddUnit(category, units[category]["default"] as JObject);

				// add the other units. you know, the other ones
				foreach(var unit in units[category]["units"] as JArray)
				{
					AddUnit(category, unit as JObject);
				}
			}

			// read currency
			var currencies = unitObj["currency"]["state"] as JArray;
			foreach(var currency in currencies)
			{
				var code = currency["code"].Value<string>().ToUpper();
				var name = currency["name"].Value<string>();

				_currencyNames[code.ToLower()] = code;
				_currencyNames[name.ToLower()] = code;
				if(currency["formatted"] != null)
				{
					_currencyFormatting[code] = (currency["formatted"] as JArray).ToObject<string[]>();
				}
				else
				{
					// if no plural, create one
					_currencyFormatting[code] = new string[]
					{
						name, name + "s"
					};
				}

				if(currency["short"] != null)
				{
					_currencyNames[currency["short"].Value<string>().ToLower()] = code;
				}

				if(currency["symbol"] != null)
				{
					_currencySymbols[code] = currency["symbol"].Value<string>();
				}
				else
				{
					_currencySymbols[code] = code;
				}
			}

			// read timezones
			var timezones = unitObj["timezones"] as JArray;
			foreach(var timezone in timezones)
			{
				var code = timezone["code"].Value<string>();
				var name = timezone["name"].Value<string>();
				var val = timezone["offset"].Value<string>();

				_timezoneNames[code.ToLower()] = code;
				_timezoneNames[name.ToLower()] = code;
				_timezoneNames[name.ToLower().Replace(" standard ", "")] = code;
				_timezoneFormatting[code] = name;
				_timezoneOffsets[code] = val;
			}
		}

		/// <summary>
		/// Converts a measurement unit to another.
		/// </summary>
		public string ConvertUnit(string input)
		{
			input = input.ToLower();
			var pivotFound = false;
			var pivotLocation = 0;
			var pivotWord = "";

			// test each pivot
			foreach(var pivot in _pivotWords)
			{
				if(input.Contains(pivot))
				{
					pivotFound = true;
					pivotLocation = input.IndexOf(pivot, StringComparison.CurrentCulture);
					pivotWord = pivot;
					break;
				}
			}

			if(!pivotFound)
			{
				throw new CommandException("Can't parse expression.");
			}

			// ex. "200 meters"
			var firstHalf = input.Substring(0, pivotLocation).Trim();
			// ex. "inches"
			var secondHalf = input.Substring(pivotLocation + pivotWord.Length).Trim();

			// look for the number
			var parts = firstHalf.Split(' ');
			var firstPart = parts.First();
			var unit = string.Join(" ", parts.Skip(1));
			// if the first part isn't an integer number
			if(!firstPart.ToCharArray().All(c => char.IsDigit(c) || c == '.'))
			{
				var endOfNumber = 0;
				var numberEndFound = false;

				// look for the end of the number
				for(var i = 0; i < firstPart.Length; i++)
				{
					var c = firstPart[i];
					if(!char.IsDigit(c) && c != '.')
					{
						endOfNumber = i;
						numberEndFound = true;
						break;
					}
				}

				if(numberEndFound)
				{
					unit = firstPart.Substring(endOfNumber);
					firstPart = firstPart.Substring(0, endOfNumber);
				}
			}

			if(!double.TryParse(firstPart, out var num))
			{
				throw new CommandException(parts.First() + " is not a valid number");
			}

			// search for the from unit
			var firstUnit = FindUnit(unit);
			if(firstUnit == null)
			{
				throw new CommandException("Can't find unit '" + unit + "'");
			}
			var unitType = _unitTypes[firstUnit];

			// search for the to unit
			var secondUnit = FindUnit(secondHalf);
			if(secondUnit == null)
			{
				throw new CommandException("Can't find unit '" + secondHalf + "'");
			}
			if(_unitTypes[secondUnit] != unitType)
			{
				throw new CommandException("Can't convert " + unitType + " to " + _unitTypes[secondUnit] + ".");
			}

			// actually convert values
			var finalValue = (num * _unitValues[firstUnit]) / _unitValues[secondUnit];

			// nice formatting
			string finalStr = finalValue + " ";
			if(_unitFormatting.ContainsKey(secondUnit))
			{
				var formatting = _unitFormatting[secondUnit];
				finalStr += (finalValue == 1 ? formatting[0] : formatting[1]);
			}
			else
			{
				finalStr += secondUnit + (finalValue == 1 ? "" : "s");
			}

			return firstPart + " " + (num == 1 ? _unitFormatting[firstUnit][0] : _unitFormatting[firstUnit][1]) + " = " + finalStr;
		}

		/// <summary>
		/// Converts a currency unit to another using relatively-recent currency data.
		/// </summary>
		public async Task<string> ConvertCurrency(string input)
		{
			input = input.ToLower();
			var pivotFound = false;
			var pivotLocation = 0;
			var pivotWord = "";

			foreach(var pivot in _pivotWords)
			{
				if(input.Contains(pivot))
				{
					pivotFound = true;
					pivotLocation = input.IndexOf(pivot, StringComparison.CurrentCulture);
					pivotWord = pivot;
					break;
				}
			}

			if(!pivotFound)
			{
				throw new CommandException("Can't parse expression.");
			}

			// split halves
			var firstHalf = input.Substring(0, pivotLocation).Trim().TrimStart('$');
			var secondHalf = input.Substring(pivotLocation + pivotWord.Length).Trim();

			var parts = firstHalf.Split(' ');
			if(parts.Length < 2)
			{
				throw new CommandException("Expected number and currency name for \"from\"");
			}

			if(!double.TryParse(parts[0], out var firstNum))
			{
				throw new CommandException("Invalid currency amount.");
			}

			var firstInput = string.Join(" ", parts.Skip(1));
			var firstCurrency = FindCurrency(firstInput);
			if(firstCurrency == null)
			{
				throw new CommandException("Couldn't find unit " + firstInput + ".");
			}

			var secondCurrency = FindCurrency(secondHalf);
			if(secondCurrency == null)
			{
				throw new CommandException("Couldn't find unit " + secondHalf + ".");
			}

			// get most recent value for this unit
			var key = _bot.Config.CurrencyLayerKey;
			var result = await Util.GetURLAsync($"http://apilayer.net/api/live?access_key={key}&currencies={firstCurrency},{secondCurrency}&format=1");
			var obj = JObject.Parse(result);
			if(!obj.Value<bool>("success"))
				throw new CommandException("Couldn't get currency data from Currency Layer. God, doesn't this API suck?");
			// currency layer won't let us change the source currency on the free plan - so we look up USD->firstCurrency and USD->secondCurrency
			var firstResult = obj["quotes"].Value<double>("USD" + firstCurrency);
			var secondResult = obj["quotes"].Value<double>("USD" + secondCurrency);
			var conversion = secondResult / firstResult;

			var firstSymbol = _currencySymbols[firstCurrency];
			var secondSymbol = _currencySymbols[secondCurrency];
			var finalNum = conversion * firstNum;
			var finalStr = secondSymbol + finalNum.ToString("C").Substring(1) + " ";
			if(_currencyFormatting.ContainsKey(secondCurrency))
			{
				var formatting = _currencyFormatting[secondCurrency];
				finalStr += (finalNum == 1 ? formatting[0] : formatting[1]);
			}
			else
			{
				finalStr += secondCurrency + (finalNum == 1 ? "" : "s");
			}
			return
				firstSymbol + firstNum.ToString("C").Substring(1) + " " +
				(firstNum == 1 ?
					_currencyFormatting[firstCurrency][0] :
					_currencyFormatting[firstCurrency][1])
				+ " = " + finalStr;
		}

		/// <summary>
		/// Converts one timezone to another.
		/// </summary>
		public string ConvertTimezone(string input)
		{
			input = input.ToLower();
			var pivotFound = false;
			var pivotLocation = 0;
			var pivotWord = "";

			foreach(var pivot in _pivotWords)
			{
				if(input.Contains(pivot))
				{
					pivotFound = true;
					pivotLocation = input.IndexOf(pivot, StringComparison.CurrentCulture);
					pivotWord = pivot;
					break;
				}
			}

			if(!pivotFound)
			{
				throw new CommandException("Can't parse expression.");
			}

			var firstHalf = input.Substring(0, pivotLocation).Trim().TrimStart('$');
			var secondHalf = input.Substring(pivotLocation + pivotWord.Length).Trim();

			var matches = _timeRegex.Match(firstHalf);
			if(!matches.Success)
			{
				throw new CommandException("Invalid time.");
			}

			// god time decoding SUCKS
			var time = matches.Groups[1].Value;
			var ampm = matches.Groups[4].Value;
			var isPM = false;
			if(ampm.Equals("PM", StringComparison.CurrentCultureIgnoreCase))
			{
				isPM = true;
			}
			else if(ampm.Equals("AM", StringComparison.CurrentCultureIgnoreCase) && (time == "12" || time == "12:00"))
			{
				time = "00:00";
			}

			if(!time.Contains(":"))
			{
				time = time + ":00";
			}

			var fromTimezoneInput = firstHalf.Substring(matches.Length).Trim();
			if(string.IsNullOrWhiteSpace(fromTimezoneInput))
			{
				throw new CommandException("You need to specify a 'from' timezone.");
			}

			var fromTimezone = FindTimezone(fromTimezoneInput);
			if(fromTimezone == null)
			{
				throw new CommandException("I don't know what timezone '" + fromTimezoneInput + "' is.");
			}

			var toTimezone = FindTimezone(secondHalf);
			if(toTimezone == null)
			{
				throw new CommandException("I don't know what timezone '" + secondHalf + "' is.");
			}

			var fromTime = new Time(time);
			if(isPM)
			{
				fromTime.AddOffset("+12:00");
			}

			var original12Hour = fromTime.To12Hour();

			fromTime.SubtractOffset(_timezoneOffsets[fromTimezone]);
			fromTime.AddOffset(_timezoneOffsets[toTimezone]);

			return original12Hour + " " + _timezoneFormatting[fromTimezone] + " = " + fromTime.To12Hour() + " " + _timezoneFormatting[toTimezone];
		}

		// actually search for the unit, kinda fuzzily
		private string FindUnit(string input)
		{
			if(_unitAliases.ContainsKey(input))
			{
				return _unitAliases[input];
			}

			// sq meter = square meter
			input = input.Replace("sq ", "square ");
			foreach(var ending in new string[] { "es", "s" })
			{
				if(input.Length < ending.Length) continue;
				var singular = input.Substring(0, input.Length - ending.Length);
				if(_unitAliases.ContainsKey(singular))
				{
					return _unitAliases[singular];
				}
			}

			// look for units sorted by levenshtein distance
			var closestUnits = _unitAliases.Keys.OrderBy(k => LevenshteinDistance(k, input));
			if(LevenshteinDistance(closestUnits.First(), input) < 4)
			{
				return _unitAliases[closestUnits.First()];
			}
			return null;
		}

		private string FindCurrency(string input)
		{
			if(_currencyNames.ContainsKey(input))
			{
				return _currencyNames[input];
			}

			foreach(var ending in new string[] { "es", "s" })
			{
				if(input.Length < ending.Length) continue;
				var singular = input.Substring(0, input.Length - ending.Length);
				if(_currencyNames.ContainsKey(singular))
				{
					return _currencyNames[singular];
				}
			}

			var closestCurrencies = _currencyNames.Keys.OrderBy(k => LevenshteinDistance(k, input));
			if(LevenshteinDistance(closestCurrencies.First(), input) < 4)
			{
				return _currencyNames[closestCurrencies.First()];
			}
			return null;
		}

		private string FindTimezone(string input)
		{
			if(_timezoneNames.ContainsKey(input))
			{
				return _timezoneNames[input];
			}

			var closestTimezones = _timezoneNames.Keys.OrderBy(k => LevenshteinDistance(k, input));
			if(LevenshteinDistance(closestTimezones.First(), input) < 4)
			{
				return _timezoneNames[closestTimezones.First()];
			}

			return null;
		}

		private int LevenshteinDistance(string a, string b)
		{
			if(string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0;

			int lengthA = a.Length;
			int lengthB = b.Length;
			var distances = new int[lengthA + 1, lengthB + 1];
			for(int i = 0; i <= lengthA; distances[i, 0] = i++) { }
			for(int j = 0; j <= lengthB; distances[0, j] = j++) { }

			for(int i = 1; i <= lengthA; i++)
			{
				for(int j = 1; j <= lengthB; j++)
				{
					int cost = b[j - 1] == a[i - 1] ? 0 : 1;
					distances[i, j] = Math.Min(
						Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1),
						distances[i - 1, j - 1] + cost
					);
				}
			}
			return distances[lengthA, lengthB];
		}

		// add a unit from the json object
		private void AddUnit(string category, JObject unit)
		{
			var value = 1d;
			if(unit["value"] != null)
			{
				value = double.Parse(unit["value"].Value<string>());
			}

			string[] formatted = null;
			if(unit["formatted"] != null)
			{
				formatted = (unit["formatted"] as JArray).ToObject<string[]>();
			}

			var name = unit["name"].Value<string>().ToLower();
			_unitTypes[name] = category;
			_unitValues[name] = value;
			_unitAliases[name] = name;
			if(formatted != null)
			{
				_unitFormatting[name] = formatted;
			}
			else
			{
				_unitFormatting[name] = formatted = new string[] { name, name + "s" };
			}

			var aliases = unit["aliases"] as JArray;
			foreach(var val in aliases)
			{
				if(_unitTypes.ContainsKey(val.Value<string>()))
				{
					continue;
				}
				_unitAliases[val.Value<string>()] = name;
			}

			if(unit["metric"] != null && unit["metric"].Value<bool>())
			{
				foreach(var metric in _metricPrefixes.Keys)
				{
					_unitTypes[metric + name] = category;
					_unitValues[metric + name] = value * Math.Pow(10, _metricPrefixes[metric]);
					_unitAliases[metric + name] = metric + name;
					_unitFormatting[metric + name] = new string[]
					{
						metric + formatted[0],
						metric + formatted[1]
					};
				}

				var metricShort = unit["metricShort"].Value<string>();
				foreach(var shortName in _shortMetricPrefixes.Keys)
				{
					_unitTypes[shortName + metricShort] = category;
					_unitValues[shortName + metricShort] = value * Math.Pow(10, _shortMetricPrefixes[shortName]);
					_unitAliases[shortName + metricShort] = shortName + metricShort;
					var longName = _shortToLong[shortName];
					_unitFormatting[shortName + metricShort] = new string[]
					{
						longName + formatted[0],
						longName + formatted[1]
					};
				}
			}
		}

		private Dictionary<string, int> _metricPrefixes = new Dictionary<string, int>()
		{
			{ "yotta", 24 },
			{ "zetta", 21 },
			{ "exa", 18 },
			{ "peta", 15 },
			{ "tera", 12 },
			{ "giga", 9 },
			{ "mega", 6 },
			{ "kilo", 3 },
			{ "hecto", 2 },
			{ "deca", 1 },
			{ "", 0 },
			{ "deci", -1 },
			{ "centi", -2 },
			{ "milli", -3 },
			{ "micro", -6 },
			{ "nano", -9 },
			{ "pico", -12 },
			{ "femto", -15 },
			{ "atto", -18 },
			{ "zepto", -21 },
			{ "yocto", -24 }
		};
		private Dictionary<string, int> _shortMetricPrefixes = new Dictionary<string, int>()
		{
			{ "Y", 24 },
			{ "Z", 21 },
			{ "E", 18 },
			{ "P", 15 },
			{ "T", 12 },
			{ "G", 9 },
			{ "M", 6 },
			{ "k", 3 },
			{ "h", 2 },
			{ "da", 1 },
			{ "", 0 },
			{ "d", -1 },
			{ "c", -2 },
			{ "m", -3 },
			{ "u", -6 },
			{ "n", -9 },
			{ "p", -12 },
			{ "f", -15 },
			{ "a", -18 },
			{ "z", -21 },
			{ "y", -24 }
		};

		private Dictionary<string, string> _shortToLong = new Dictionary<string, string>()
		{
			{ "Y", "yotta" },
			{ "Z", "zetta" },
			{ "E", "exa" },
			{ "P", "petta" },
			{ "T", "tera" },
			{ "G", "giga" },
			{ "M", "mega" },
			{ "k", "kilo" },
			{ "h", "hecto" },
			{ "da", "deca" },
			{ "", "" },
			{ "d", "deci" },
			{ "c", "centi" },
			{ "m", "milli" },
			{ "u", "micro" },
			{ "n", "nano" },
			{ "p", "pico" },
			{ "f", "femto" },
			{ "a", "atto" },
			{ "z", "zepto" },
			{ "y", "yocto" }
		};
	}

	// I DON'T REMEMBER WHY I WROTE MY OWN TIME CLASS!
	internal class Time
	{
		private int _hours;
		private int _minutes;

		public Time(string val)
		{
			var parts = val.Split(':');
			_hours = int.Parse(parts[0]);
			_minutes = int.Parse(parts[1]);
		}

		/// <summary>
		/// Add time offset to this time.
		/// </summary>
		public void AddOffset(string offset)
		{
			var parts = offset.TrimStart('+').Split(':');
			var hours = int.Parse(parts[0]);
			var minutes = int.Parse(parts[1]);
			if(hours < 0)
			{
				minutes = -minutes;
			}

			_minutes += minutes;
			if(_minutes < 0)
			{
				_hours -= (int)Math.Floor((double)Math.Abs(_minutes) / 60);
				_minutes = _minutes + 60;
			}
			else if(_minutes > 60)
			{
				_hours += (int)Math.Floor((double)_minutes / 60);
				_minutes = _minutes - 60;
			}

			_hours += hours;
			if(_hours < 0)
			{
				_hours = _hours + 24;
			}
			else if(_hours > 24)
			{
				_hours = _hours - 24;
			}
		}

		/// <summary>
		/// Subtract time offset from this time.
		/// </summary>
		public void SubtractOffset(string offset)
		{
			// basically just add negative offset
			if(offset.StartsWith("-", StringComparison.CurrentCultureIgnoreCase))
			{
				AddOffset(offset.Substring(1));
			}
			else
			{
				AddOffset("-" + offset.TrimStart('+'));
			}
		}

		/// <summary>
		/// Convert to twelve hour time.
		/// </summary>
		public string To12Hour()
		{
			var time = "";
			if(_hours == 0)
			{
				time = "12:" + _minutes.ToString("00");
			}
			else
			{
				time = (_hours % 12) + ":" + _minutes.ToString("00");
			}

			return time + " " + (_hours > 11 ? "PM" : "AM");
		}
	}
}
