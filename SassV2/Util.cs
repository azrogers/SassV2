using Discord;
using Discord.Commands;
using NLog;
using SassV2.Commands;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SassV2
{
	public static class Util
	{
		/// <summary>
		/// Fills template placeholders with random words.
		/// </summary>
		/// <param name="template">A template including placeholders, like {0} this {1}.</param>
		/// <param name="words">An array of words.</param>
		/// <returns>The template, filled with random words.</returns>
		public static string FillTemplate(string template, string[] words)
		{
			var rand = new Random();
			var templateRegex = new Regex(@"\{(\d+)\}");
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
			if(num <= 0)
			{
				return num.ToString();
			}

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
			if(string.IsNullOrEmpty(input))
			{
				return "";
			}

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

			var mentionRegex = new Regex(@"<@(\d+)>");
			if(mentionRegex.IsMatch(name))
			{
				var match = mentionRegex.Match(name);
				var id = match.Groups[1].Value;
				return users.Where(u => u.Id.ToString() == id);
			}

			var userToFind = name.ToLower().Trim();
			var exactly = userToFind.Substring(0, 1) == "!";
			if(exactly)
			{
				userToFind = userToFind.Substring(1);
			}

			return users.Where(u =>
			{
				if(exactly)
				{
					return u.NicknameOrDefault().ToLower() == userToFind;
				}

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
			if(unsafeString == null)
			{
				return null;
			}

			return
				unsafeString
				.Replace("&", "&amp;")
				.Replace("<", "&lt;")
				.Replace(">", "&gt;")
				.Replace("\"", "&quot;")
				.Replace("'", "&#039;");
		}

		/// <summary>
		/// Be rude? Maybe? Only if it's OK.
		/// </summary>
		public static string MaybeBeRudeError(ServerConfig config)
		{
			if(config != null && config.Civility)
			{
				return CivilizeString(AssembleRudeErrorMessage());
			}
			return AssembleRudeErrorMessage();
		}

		/// <summary>
		/// Place the values in args into the named placeholders in str.
		/// </summary>
		/// <param name="str">The string that contains placeholders in the form {name}.</param>
		/// <param name="args">An object that contains the properly named values.</param>
		public static string FormatString(string str, object args)
		{
			var argsDict = new Dictionary<string, string>();
			if(args != null)
			{
				argsDict = AnonymousObjectToDictionary<string>(args);
			}

			var templateRegex = new Regex(@"\{(\w+)\}");
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
			foreach(var prop in props)
			{
				kv[prop.Name] = (T)prop.GetValue(anon, null);
			}
			return kv;
		}

		/// <summary>
		/// Asynchronously gets the data a URL.
		/// </summary>
		public static async Task<string> GetURLAsync(string url)
		{
			using(var httpClient = new HttpClient())
			{
				var resp = await httpClient.GetAsync(url);
				return await resp.Content.ReadAsStringAsync();
			}
		}

		/// <summary>
		/// Splits a string by spaces, preserving quoted bits (like the command line does).
		/// </summary>
		/// <param name="input">An input string, like 'A "B C" D"</param>
		/// <returns>The split output, like ["A", "B C", "D"]</returns>
		public static string[] SplitQuotedString(string input)
		{
			// this used to be regex based but it turns out it didn't work that well...
			input = Regex.Replace(input, "[“”]", "\"");

			var list = new List<string>();
			var inString = false;
			var stringStartChar = ' ';
			var word = "";
			for(var i = 0; i < input.Length; i++)
			{
				if(!inString && (input[i] == '"' || input[i] == '\''))
				{
					if(word.Trim().Length > 0)
					{
						list.AddRange(word.Trim().Split(' '));
					}

					word = "";
					stringStartChar = input[i];
					inString = true;
				}
				else if(inString && input[i] == stringStartChar)
				{
					list.Add(word.Trim());
					word = "";
					inString = false;
				}
				else
				{
					word += input[i].ToString();
				}
			}

			// add last word if not finished already
			if(word.Length > 0)
			{
				list.AddRange(word.Trim().Split(' '));
			}

			return list.ToArray();
		}

		/// <summary>
		/// Parses a double from the input, supporting k, M, and B modifiers.
		/// Taken from the Rant source.
		/// </summary>
		public static bool ParseDouble(string value, out double number)
		{
			number = 0;
			if(string.IsNullOrWhiteSpace(value))
			{
				return false;
			}

			value = value.Trim();
			if(!char.IsLetter(value[value.Length - 1]))
			{
				return double.TryParse(value, out number);
			}

			var power = value[value.Length - 1];
			value = value.Substring(0, value.Length - 1);
			if(string.IsNullOrWhiteSpace(value))
			{
				return false;
			}

			if(!double.TryParse(value, out var n))
			{
				return false;
			}

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

		/// <summary>
		/// Convert a Discord.Net LogSeverity to an NLog LogLevel.
		/// </summary>
		public static LogLevel SeverityToLevel(LogSeverity s)
		{
			switch(s)
			{
				case LogSeverity.Debug:
					return LogLevel.Debug;
				case LogSeverity.Error:
					return LogLevel.Error;
				case LogSeverity.Verbose:
					return LogLevel.Trace;
				case LogSeverity.Warning:
					return LogLevel.Warn;
				case LogSeverity.Info:
				default:
					return LogLevel.Info;
			}
		}

		/// <summary>
		/// Convert a Discord.Net CommandError to a user-friendly message.
		/// </summary>
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

		/// <summary>
		/// Converts an anonymous object to an ExpandoObject for use with RazorLight ViewBags.
		/// </summary>
		public static ExpandoObject ViewBagFromAnonObject(object anon) => AnonymousObjectToDictionary<object>(anon).ToExpando();

		/// <summary>
		/// Crypto-random string, 32 bytes (256 bits)
		/// </summary>
		public static string RandomString(bool base64 = true, int numBytes = 32)
		{
			using(var rng = RandomNumberGenerator.Create())
			{
				var tokenData = new byte[numBytes];
				rng.GetBytes(tokenData);

				if(base64)
				{
					return Convert.ToBase64String(tokenData);
				}
				else
				{
					return BitConverter.ToString(tokenData).Replace("-", "").ToLower();
				}
			}
		}

		/// <summary>
		/// Searches messages. I don't think this works.
		/// </summary>
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

		/// <summary>
		/// Converts the given lines to several messages.
		/// </summary>
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
				{
					message += "\n" + line;
				}
			}

			yield return message;
		}


		/// <summary>
		/// Converts a string to snake case.
		/// </summary>
		/// <remarks>Borrowed from the Rant source at Rant/Core/Utilities/Util.cs line 318.</remarks>
		public static string ToSnakeCase(string camelName)
		{
			var name = camelName.Trim();
			if(string.IsNullOrWhiteSpace(name))
			{
				return name;
			}

			if(name.Length == 1)
			{
				return name.ToLower();
			}

			bool a, b;
			var sb = new StringBuilder();
			var last = false;
			for(var i = 0; i < name.Length - 1; i++)
			{
				a = char.IsUpper(name[i]);
				b = char.IsUpper(name[i + 1]);
				if(last && a && !b)
				{
					sb.Append('_');
				}

				sb.Append(char.ToLower(name[i]));
				if(!a && b)
				{
					sb.Append('_');
				}

				last = a;
			}

			sb.Append(char.ToLower(name[name.Length - 1]));
			return sb.ToString();
		}

		/// <summary>
		/// Creates a DateTime from the given unix time.
		/// </summary>
		public static DateTime FromUnixTime(ulong unixTime)
		{
			var dateTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			return dateTime.AddSeconds(unixTime);
		}

		/// <summary>
		/// Creates a DateTime from the given unix time.
		/// </summary>
		public static DateTime FromUnixTime(long unixTime)
		{
			var dateTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			return dateTime.AddSeconds(unixTime);
		}

		/// <summary>
		/// Returns all but the last values of an enumerator.
		/// </summary>
		public static IEnumerable<T> TakeAllButLast<T>(IEnumerable<T> source)
		{
			var it = source.GetEnumerator();
			var hasRemainingItems = false;
			var flag = true;
			var t = default(T);
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
			} while(hasRemainingItems);
			yield break;
		}

		/// <summary>
		/// Formats a list in a natural way.
		/// </summary>
		public static string NaturalArrayJoin(IEnumerable<string> values)
		{
			switch(values.Count())
			{
				case 0:
					return "";
				case 1:
					return values.First();
				case 2:
					return $"{values.First()} and {values.Last()}";
				default:
					return string.Join(", ", TakeAllButLast(values)) + " and " + values.Last();
			}
		}

		/// <summary>
		/// Replaces newlines with HTML line breaks.
		/// </summary>
		public static string NewLineToLineBreak(string str) => str.Replace("\n", "<br>");

		/// <summary>
		/// Parses a bool input.
		/// </summary>
		public static bool ParseBool(string str)
		{
			str = str.ToLower().Trim();
			return str == "true" || str == "on" || str == "yes";
		}

		/// <summary>
		/// Replaces swear words in a string with censorship.
		/// </summary>
		public static string CivilizeString(string str) => new Regex("(shit|fuck|heck|hell|piss|dick|cock)").Replace(str, (Match m) => GenerateCensorship(m.Groups[1].Length));

		/// <summary>
		/// Generates a string to replace a swear word.
		/// </summary>
		public static string GenerateCensorship(int length)
		{
			var rand = new Random();
			var list = new List<char>();
			var sourceChars = new char[] { '^', '#', '@', '&', '*', '-', '%', '!', '~' };

			var num = 0;
			for(var i = 0; i < length; i++)
			{
				// shuffle the source char array every time we've gone through it once
				if(num == 0)
				{
					sourceChars = sourceChars.OrderBy(ch => rand.Next()).ToArray();
					num = sourceChars.Length;
				}

				// escape asterisks, for discord use
				var c = sourceChars[num - 1];
				if(c == '*')
				{
					list.Add('\\');
				}

				list.Add(c);
				num--;
			}

			return string.Join("", list);
		}

		/// <summary>
		/// Reads an array from form post data.
		/// </summary>
		public static List<string> ReadFormArray(Dictionary<string, object> dir, string name)
		{
			if(dir == null || !dir.ContainsKey(name))
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

		/// <summary>
		/// Computes the levenshtein distance between two strings.
		/// </summary>
		public static int LevenshteinDistance(string a, string b)
		{
			if(string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
			{
				return 0;
			}

			var lengthA = a.Length;
			var lengthB = b.Length;
			var distances = new int[lengthA + 1, lengthB + 1];
			for(var i = 0; i <= lengthA; distances[i, 0] = i++) { }
			for(var j = 0; j <= lengthB; distances[0, j] = j++) { }

			for(var i = 1; i <= lengthA; i++)
			{
				for(var j = 1; j <= lengthB; j++)
				{
					var cost = b[j - 1] == a[i - 1] ? 0 : 1;
					distances[i, j] = Math.Min(
						Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1),
						distances[i - 1, j - 1] + cost
					);
				}
			}
			return distances[lengthA, lengthB];
		}

		/// <summary>
		/// Limits a string to the given number of characters, splitting on newlines.
		/// </summary>
		public static string SmartMaxLength(string str, int n)
		{
			if(n < 1)
			{
				return "";
			}

			var lines = str.Split('\n');
			// first line longer than max length, don't even bother
			if(lines[0].Length > n)
			{
				return lines[0].Substring(0, n - 3) + "...";
			}

			var validLines = new List<string>();
			var currentLen = 0;
			for(var i = 0; i < lines.Length; i++)
			{
				var line = lines[i];
				if(currentLen + line.Length + 1 >= n)
				{
					break;
				}

				validLines.Add(line);
				// add an extra char for newline
				currentLen += line.Length + 1;
			}

			return string.Join('\n', validLines);
		}
	}
}
