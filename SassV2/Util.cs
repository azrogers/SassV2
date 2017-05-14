﻿using Discord;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Net;
using System.Net.Http;
using NLog;

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
		public static string Locale(string name, object args = null)
		{
			if(_localeCache == null)
			{
				_localeCache = JObject.Parse(File.ReadAllText("locale.json"));
			}

			var argsDict = new Dictionary<string, string>();
			if(args != null)
			{
				argsDict = AnonymousObjectToDictionary(args);
			}

			JToken entry = _localeCache[name];
			if(entry == null)
			{
				return "[Missing Locale]";
			}

			if(entry is JArray)
			{
				var children = entry.Children().Select(t => t.Value<string>()).ToArray();
				var rand = new Random();
				return children[rand.Next(children.Length)];
			}
			string str = entry.Value<string>();

			Regex templateRegex = new Regex(@"\{(\d+)\}");
			return templateRegex.Replace(str, (match) =>
			{
				var key = match.Groups[1].Value;
				return (argsDict.ContainsKey(key) ? "[Unknown Argument]" : argsDict[key]);
			});
		}

		/// <summary>
		/// Converts an anonymous object to a dictionary of strings.
		/// </summary>
		/// <param name="anon">Anonymous object.</param>
		/// <returns>A dictionary of the fields in the object.</returns>
		public static Dictionary<string, string> AnonymousObjectToDictionary(object anon)
		{
			var type = anon.GetType();
			var props = type.GetProperties();
			var kv = new Dictionary<string, string>();
			foreach(PropertyInfo prop in props)
			{
				kv[prop.Name] = prop.GetValue(anon, null).ToString();
			}
			return kv;
		}

		public static string GetURL(string url)
		{
			return new WebClient().DownloadString(url);
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
			return Regex.Matches(input, @"[\""].+?[\""]|[^ ]+")
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
			double n;
			if(!Double.TryParse(value, out n)) return false;
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
	}
}
