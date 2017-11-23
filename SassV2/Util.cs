using Discord;
using Discord.Commands;
using Newtonsoft.Json.Linq;
using NLog;
using SassV2.Commands;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SassV2
{
	public class Util
	{
		private static JObject _localeCache;

		/// <summary>
		/// Fills template placeholders with random words.
		/// </summary>
		/// <param name="template">A template including placeholders, like {0} this {1}.</param>
		/// <param name="words">An array of words.</param>
		/// <returns>The template, filled with random words.</returns>
		public static string FillTemplate(string template, string[] words)
		{
			Random rand = new Random();
			Regex templateRegex = new Regex(@"\{(\d+)\}");
			return templateRegex.Replace(template, (match) =>
			{
				return words[rand.Next(words.Length)];
			});
		}

		/// <summary>
		/// Assembles a rude message.
		/// </summary>
		/// <returns>A rude message.</returns>
		public static string AssembleRudeMessage()
		{
			string[] templates =
			{
				"{0} up",
				"{0} off",
				"{0} the {1} off",
				"{0} the {1} up",
				"{0}"
			};

			string[] words = { "shit", "fuck", "shut", "heck", "hell", "piss", "dick" };

			var randomTemplate = templates[new Random().Next(templates.Length)];
			return FillTemplate(randomTemplate, words);
		}

		/// <summary>
		/// Assembles a rude error message.
		/// </summary>
		/// <returns>A rude error message.</returns>
		public static string AssembleRudeErrorMessage()
		{
			string[] templates =
			{
				"something {0} up",
				"you {0} something the fuck up",
				"it's {0}"
			};

			string[] words = { "fucked", "dicked", "cocked", "pissed", "hecked" };

			var randomTemplate = templates[new Random().Next(templates.Length)];
			return FillTemplate(randomTemplate, words);
		}

		/// <summary>
		/// Converts a number to ordinal form.
		/// </summary>
		/// <param name="num">The number.</param>
		/// <returns>The number in ordinal form.</returns>
		public static string CardinalToOrdinal(int num)
		{
			if(num <= 0) return num.ToString();

			switch(num % 100)
			{
				case 11:
				case 12:
				case 13:
					return num + "th";
			}

			switch(num % 10)
			{
				case 1:
					return num + "st";
				case 2:
					return num + "nd";
				case 3:
					return num + "rd";
				default:
					return num + "th";
			}
		}

		/// <summary>
		/// Capitalizes the string.
		/// </summary>
		/// <param name="input">The string to capitalize.</param>
		/// <returns>The capitalized string.</returns>
		public static string Capitalize(string input)
		{
			if(string.IsNullOrEmpty(input)) return "";
			return input.First().ToString().ToUpper() + string.Join("", input.Skip(1));
		}

		/// <summary>
		/// Finds a user, given a name.
		/// </summary>
		/// <param name="name">The search term.</param>
		/// <param name="message">The user's message.</param>
		/// <returns>A list of users matching the search.</returns>
		public static async Task<IEnumerable<IUser>> FindWithName(string name, IMessage message)
		{
			var users = await (message.Channel as IGuildChannel).Guild.GetUsersAsync();

			Regex mentionRegex = new Regex(@"<@(\d+)>");
			if(mentionRegex.IsMatch(name))
			{
				Match match = mentionRegex.Match(name);
				string id = match.Groups[1].Value;
				return users.Where(u => u.Id.ToString() == id);
			}

			string userToFind = name.ToLower().Trim();
			bool exactly = userToFind.Substring(0, 1) == "!";
			if(exactly)
				userToFind = userToFind.Substring(1);

			return users.Where(u =>
			{
				if(exactly)
					return u.NicknameOrDefault().ToLower() == userToFind;
				return u.NicknameOrDefault().ToLower().IndexOf(userToFind, StringComparison.CurrentCulture) != -1;
			});
		}

		/// <summary>
		/// Sanitizes a string for use in HTML.
		/// </summary>
		/// <param name="unsafeString">An unsafe string.</param>
		/// <returns>The sanitized version of the string.</returns>
		public static string SanitizeHTML(string unsafeString)
		{
			return
				unsafeString
				.Replace("&", "&amp;")
				.Replace("<", "&lt;")
				.Replace(">", "&gt;")
				.Replace("\"", "&quot;")
				.Replace("'", "&#039;");
		}

		/// <summary>
		/// Returns a string from locale.json, given the name and args.
		/// </summary>
		/// <param name="name">The name of the locale string.</param>
		/// <param name="args">The arguments.</param>
		/// <returns></returns>
		public static string Locale(string lang, string name, object args = null)
		{
			if(_localeCache == null)
			{
				_localeCache = JObject.Parse(File.ReadAllText("locale.json"));
				_languages = new Dictionary<string, LocaleLanguage>();
				foreach(var item in _localeCache)
				{
					if(item.Value["_base"] != null)
					{
						_languages[item.Key] = new LocaleLanguage(_languages[item.Value["_base"].Value<string>()], item.Value);
					}
					else
					{
						_languages[item.Key] = new LocaleLanguage(item.Value);
					}
				}
			}
			LocaleLanguage localeLanguage = _languages.ContainsKey(lang) ? _languages[lang] : null;
			if(localeLanguage == null)
			{
				return "[Missing Language]";
			}
			JToken jToken = localeLanguage[name];
			if(jToken == null)
			{
				return "[Missing Locale]";
			}
			if(jToken is JArray)
			{
				var array = jToken.Children().Select(c => c.Value<string>()).ToArray();
				return array[new Random().Next(array.Length)];
			}
			return FormatString(Extensions.Value<string>(jToken), args);
		}


		public static string MaybeBeRudeError(ServerConfig config)
		{
			if(config != null && config.Civility)
			{
				return Util.CivilizeString(Util.AssembleRudeErrorMessage());
			}
			return Util.AssembleRudeErrorMessage();
		}


		public static string FormatString(string str, object args)
		{
			var argsDict = new Dictionary<string, string>();
			if(args != null)
			{
				argsDict = AnonymousObjectToDictionary<string>(args);
			}

			Regex templateRegex = new Regex(@"\{(\w+)\}");
			return templateRegex.Replace(str, (match) =>
			{
				var key = match.Groups[1].Value;
				return (argsDict.ContainsKey(key) ? argsDict[key] : "[Unknown Argument]");
			});
		}

		/// <summary>
		/// Converts an anonymous object to a dictionary of strings.
		/// </summary>
		/// <param name="anon">Anonymous object.</param>
		/// <returns>A dictionary of the fields in the object.</returns>
		public static Dictionary<string, T> AnonymousObjectToDictionary<T>(object anon)
		{
			var type = anon.GetType();
			var props = type.GetProperties();
			var kv = new Dictionary<string, T>();
			foreach(PropertyInfo prop in props)
			{
				kv[prop.Name] = (T)prop.GetValue(anon, null);
			}
			return kv;
		}

		public static async Task<string> GetURL(string url)
		{
			return await new HttpClient().GetStringAsync(url);
		}

		public async static Task<string> GetURLAsync(string url)
		{
			using(var httpClient = new HttpClient())
			{
				var resp = await httpClient.GetAsync(url);
				return await resp.Content.ReadAsStringAsync();
			}
		}

		public static string[] SplitQuotedString(string input)
		{
			return Regex.Matches(input, @"[\""].*?[\""]|[^ ]+")
				.Cast<Match>()
				.Select(m => m.Value.Trim('"'))
				.ToArray();
		}

		public static bool ParseDouble(string value, out double number)
		{
			number = 0;
			if(string.IsNullOrWhiteSpace(value)) return false;
			value = value.Trim();
			if(!Char.IsLetter(value[value.Length - 1]))
				return Double.TryParse(value, out number);
			char power = value[value.Length - 1];
			value = value.Substring(0, value.Length - 1);
			if(string.IsNullOrWhiteSpace(value)) return false;
			if(!Double.TryParse(value, out var n)) return false;
			switch(power)
			{
				case 'k': // Thousand
					number = (int)(n * 1000);
					return true;
				case 'M': // Million
					number = (int)(n * 1000000);
					return true;
				case 'B': // Billion
					number = (int)(n * 1000000000);
					return true;
				default:
					return false;
			}
		}

		public static LogLevel SeverityToLevel(LogSeverity s)
		{
			switch(s)
			{
				case LogSeverity.Debug:
					return LogLevel.Debug;
				case LogSeverity.Error:
					return LogLevel.Error;
				case LogSeverity.Info:
					return LogLevel.Info;
				case LogSeverity.Verbose:
					return LogLevel.Trace;
				case LogSeverity.Warning:
					return LogLevel.Warn;
				default:
					return LogLevel.Info;
			}
		}

		public static string CommandErrorToMessage(CommandError error)
		{
			switch(error)
			{
				case CommandError.UnknownCommand:
					return "I don't know what that is.";
				case CommandError.ParseFailed:
					return "Something doesn't make sense with your command.";
				case CommandError.BadArgCount:
					return "That's not how you use that command.";
				case CommandError.ObjectNotFound:
				case CommandError.MultipleMatches:
					return "Something messed up here.";
				case CommandError.UnmetPrecondition:
					return "You can't do that.";
				case CommandError.Exception:
					return AssembleRudeErrorMessage();
				default:
					return "Something went wrong and I don't know what.";
			}
		}

		public static ExpandoObject ViewBagFromAnonObject(object anon)
		{
			return AnonymousObjectToDictionary<object>(anon).ToExpando();
		}

		/// <summary>
		/// Crypto-random string
		/// </summary>
		public static string RandomString()
		{
			using(var rng = RandomNumberGenerator.Create())
			{
				var tokenData = new byte[32];
				rng.GetBytes(tokenData);

				return Convert.ToBase64String(tokenData);
			}
		}

		public static async Task<string> SearchMessages(IGuild guild, string token, DateTime from, DateTime to)
		{
			var discordEpoch = new DateTime(2015, 1, 1);
			var fromId = (ulong)(from - discordEpoch).TotalMilliseconds << 22;
			var toId = (ulong)(to - discordEpoch).TotalMilliseconds << 22;
			using(var client = new HttpClient())
			{
				client.DefaultRequestHeaders.Add("Authorization", "Bot " + token);
				return await client.GetStringAsync($"/v6/guilds/{guild.Id}/messages/search?min_id={fromId}&max_id={toId}");
			}
		}

		public static IEnumerable<string> GetMessages(IEnumerable<string> lines, int max)
		{
			var pageAmount = 1;
			var message = "";
			foreach(var line in lines)
			{
				if(message.Length + line.Length > max)
				{
					yield return message;
					pageAmount++;
					message = $"Page {pageAmount}:\n" + line;
				}
				else
					message += "\n" + line;
			}

			yield return message;
		}

		public static string Nl2br(string str)
		{
			return str.Replace("\n", "<br>");
		}

		public static string ToSnakeCase(string str)
		{
			return str.ToLower().Replace(" ", "_");
		}

		public static DateTime FromUnixTime(ulong unixTime)
		{
			DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			return dateTime.AddSeconds(unixTime);
		}

		public static DateTime FromUnixTime(long unixTime)
		{
			DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			return dateTime.AddSeconds((double)unixTime);
		}

		public static IEnumerable<T> TakeAllButLast<T>(IEnumerable<T> source)
		{
			IEnumerator<T> it = source.GetEnumerator();
			bool hasRemainingItems = false;
			bool flag = true;
			T t = default(T);
			do
			{
				hasRemainingItems = it.MoveNext();
				if(hasRemainingItems)
				{
					if(!flag)
					{
						yield return t;
					}
					t = it.Current;
					flag = false;
				}
			}
			while(hasRemainingItems);
			yield break;
		}

		public static string NaturalArrayJoin(IEnumerable<string> values)
		{
			int num = values.Count<string>();
			if(num == 0)
			{
				return "";
			}
			if(num == 1)
			{
				return values.First<string>();
			}
			if(num == 2)
			{
				return string.Format("{0} and {1}", values.ElementAt(0), values.ElementAt(1));
			}
			return string.Join(", ", Util.TakeAllButLast<string>(values)) + " and " + values.Last<string>();
		}

		private static Dictionary<string, LocaleLanguage> _languages;

		private class LocaleLanguage
		{
			private LocaleLanguage _base;
			private JToken _values;

			public JToken this[string key]
			{
				get
				{
					if(_values[key] != null)
					{
						return _values[key];
					}
					if(_base != null)
					{
						return _base[key];
					}
					return null;
				}
			}

			public LocaleLanguage(JToken values)
			{
				this._base = null;
				this._values = values;
			}

			public LocaleLanguage(LocaleLanguage baseLang, JToken values)
			{
				_base = baseLang;
				_values = values;
			}
		}

		public static string NewLineToLineBreak(string str)
		{
			return str.Replace("\n", "<br>");
		}

		public static bool ParseBool(string str)
		{
			str = str.ToLower().Trim();
			return str == "true" || str == "on" || str == "yes";
		}

		public static string CivilizeString(string str)
		{
			return new Regex("(shit|fuck|heck|hell|piss|dick|cock)").Replace(str, (Match m) => Util.GenerateCensorship(m.Groups[1].Length));
		}

		public static string GenerateCensorship(int length)
		{
			Random rand = new Random();
			List<char> list = new List<char>();
			char[] sourceChars = new char[]
			{
				'^',
				'#',
				'@',
				'&',
				'*',
				'-',
				'%',
				'!',
				'~'
			};
			int num = 0;
			for(int i = 0; i < length; i++)
			{
				if(num == 0)
				{
					sourceChars = sourceChars.OrderBy(ch => rand.Next()).ToArray<char>();
					num = sourceChars.Length;
				}
				char c = sourceChars[num - 1];
				if(c == '*')
				{
					list.Add('\\');
				}
				list.Add(c);
				num--;
			}

			return string.Join("", list);
		}

		// Token: 0x06000138 RID: 312 RVA: 0x00006C08 File Offset: 0x00004E08
		public static List<string> ReadFormArray(Dictionary<string, object> dir, string name)
		{
			if(!dir.ContainsKey(name))
			{
				return new List<string>();
			}
			if(dir[name].GetType() == typeof(string))
			{
				return new List<string>(new string[]
				{
					dir[name].ToString()
				});
			}
			return (List<string>)dir[name];
		}
	}
}
