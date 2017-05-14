using Discord;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace SassV2.Commands
{
	public class ConvertCommand
	{
		private static UnitConverter _unitConverter = new UnitConverter();

		[Command(
			names: new string[] { "convert", "unit convert", "convert unit" }, 
			desc: "convert one unit to another unit (not in the cloud).", 
			usage: "convert <thing> (to|in|at|as|=) <thing>", 
			category: "Useful")]
		public static string ConvertUnits(DiscordBot bot, IMessage msg, string args)
		{
			return _unitConverter.ConvertUnit(args);
		}

		[Command(
			names: new string[] { "convert currency", "currency convert" }, 
			desc: "convert one unit to another unit, kinda in the cloud.",
			usage: "convert currency <from> (to|in|at|as|=) <to>",
			category: "Useful")]
		public async static Task<string> ConvertCurrency(DiscordBot bot, IMessage msg, string args)
		{
			return await _unitConverter.ConvertCurrency(args);
		}

		[Command(
			names: new string[] { "convert timezone", "timezone convert" },
			desc: "convert one time to another time in a different timezone, not involving clouds in any way.",
			usage: "convert timezone <time> <timezone> (to|in|at|as|=) <timezone>",
			category: "Useful"	
		)]
		public static string ConvertTimezone(DiscordBot bot, IMessage msg, string args)
		{
			return _unitConverter.ConvertTimezone(args);
		}
	}

	public class UnitConverter
	{
		private string[] _pivotWords = { "to", "in", "at", "as", "=" };
		private Regex _timeRegex = new Regex(@"(\d+(:\d+)?)(\s+)?(AM|PM)?", RegexOptions.IgnoreCase);

		private Dictionary<string, string> _unitTypes = new Dictionary<string, string>();
		private Dictionary<string, double> _unitValues = new Dictionary<string, double>();
		private Dictionary<string, string[]> _unitFormatting = new Dictionary<string, string[]>();
		private Dictionary<string, string> _unitAliases = new Dictionary<string, string>();

		private Dictionary<string, string> _currencyNames = new Dictionary<string, string>();
		private Dictionary<string, string[]> _currencyFormatting = new Dictionary<string, string[]>();

		private Dictionary<string, string> _timezoneNames = new Dictionary<string, string>();
		private Dictionary<string, string> _timezoneFormatting = new Dictionary<string, string>();
		private Dictionary<string, string> _timezoneOffsets = new Dictionary<string, string>();

		public UnitConverter()
		{
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
					_currencyFormatting[code] = new string[]
					{
						name, name + "s"
					};
				}

				if(currency["short"] != null)
				{
					_currencyNames[currency["short"].Value<string>().ToLower()] = code;
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

		public string ConvertUnit(string input)
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

			var firstHalf = input.Substring(0, pivotLocation).Trim();
			var secondHalf = input.Substring(pivotLocation + pivotWord.Length).Trim();

			var parts = firstHalf.Split(' ');
			var firstPart = parts.First();
			var unit = string.Join(" ", parts.Skip(1));
			if(!firstPart.ToCharArray().All(c => char.IsDigit(c) || c == '.'))
			{
				var endOfNumber = 0;
				var numberEndFound = false;

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

			double num;
			if(!double.TryParse(firstPart, out num))
			{
				throw new CommandException(parts.First() + " is not a valid number");
			}

			var firstUnit = FindUnit(unit);
			if(firstUnit == null)
			{
				throw new CommandException("Can't find unit '" + unit + "'");
			}
			var unitType = _unitTypes[firstUnit];
			var secondUnit = FindUnit(secondHalf);
			if(secondUnit == null)
			{
				throw new CommandException("Can't find unit '" + secondHalf + "'");
			}
			if(_unitTypes[secondUnit] != unitType)
			{
				throw new CommandException("Can't convert " + unitType + " to " + _unitTypes[secondUnit] + ".");
			}

			var finalValue = (num * _unitValues[firstUnit]) / _unitValues[secondUnit];
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

			var firstHalf = input.Substring(0, pivotLocation).Trim().TrimStart('$');
			var secondHalf = input.Substring(pivotLocation + pivotWord.Length).Trim();

			var parts = firstHalf.Split(' ');
			if(parts.Length < 2)
			{
				throw new CommandException("Expected number and currency name for \"from\"");
			}

			double firstNum;
			if(!double.TryParse(parts[0], out firstNum))
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

			var result = await Util.GetURLAsync("http://finance.yahoo.com/d/quotes.csv?e=.csv&f=l1&s=" + firstCurrency + secondCurrency + "=X");
			double conversion;
			if(!double.TryParse(result, out conversion))
			{
				throw new CommandException("Unexpected result from Yahoo. Blame Marissa.");
			}

			var finalNum = conversion * firstNum;
			var finalStr = finalNum + " ";
			if(_currencyFormatting.ContainsKey(secondCurrency))
			{
				var formatting = _currencyFormatting[secondCurrency];
				finalStr += (finalNum == 1 ? formatting[0] : formatting[1]);
			}
			else
			{
				finalStr += secondCurrency + (finalNum == 1 ? "" : "s");
			}
			return firstNum + " " + (firstNum == 1 ? _currencyFormatting[firstCurrency][0] : _currencyFormatting[firstCurrency][1]) + " = " + finalStr;
		}

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

		private string FindUnit(string input)
		{
			if(_unitAliases.ContainsKey(input))
			{
				return _unitAliases[input];
			}

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

	internal class Time
	{
		private int Hours;
		private int Minutes;

		public Time(string val)
		{
			var parts = val.Split(':');
			Hours = int.Parse(parts[0]);
			Minutes = int.Parse(parts[1]);
		}

		public void AddOffset(string offset)
		{
			var parts = offset.TrimStart('+').Split(':');
			var hours = int.Parse(parts[0]);
			var minutes = int.Parse(parts[1]);
			if(hours < 0)
			{
				minutes = -minutes;
			}

			Minutes += minutes;
			if(Minutes < 0)
			{
				Hours -= (int)Math.Floor((double)Math.Abs(Minutes) / 60);
				Minutes = Minutes + 60;
			}
			else if(Minutes > 60)
			{
				Hours += (int)Math.Floor((double)Minutes / 60);
				Minutes = Minutes - 60;
			}

			Hours += hours;
			if(Hours < 0)
			{
				Hours = Hours + 24;
			}
			else if(Hours > 24)
			{
				Hours = Hours - 24;
			}
		}

		public void SubtractOffset(string offset)
		{
			if(offset.StartsWith("-", StringComparison.CurrentCultureIgnoreCase))
			{
				AddOffset(offset.Substring(1));
			}
			else
			{
				AddOffset("-" + offset.TrimStart('+'));
			}
		}

		public string To12Hour()
		{
			var time = "";
			if(Hours == 0)
			{
				time = "12:" + Minutes.ToString("00");
			}
			else
			{
				time = (Hours % 12) + ":" + Minutes.ToString("00");
			}

			return time + " " + (Hours > 11 ? "PM" : "AM");
		}
	}
}
