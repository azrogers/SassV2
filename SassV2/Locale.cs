using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SassV2
{
	public static class Locale
	{
		private static Dictionary<string, LocaleLanguage> _languages;
		private static JObject _localeCache;

		/// <summary>
		/// Returns a string from locale.json, with the default (English) locale.
		/// </summary>
		public static string GetString(string name, object args = null)
		{
			return GetString("eng", name, args);
		}

		/// <summary>
		/// Returns a string from locale.json, given the name and args.
		/// </summary>
		/// <param name="name">The name of the locale string.</param>
		/// <param name="args">The arguments.</param>
		/// <returns></returns>
		public static string GetString(string lang, string name, object args = null)
		{
			// load all locale if not done
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

			var localeLanguage = _languages.ContainsKey(lang) ? _languages[lang] : null;
			if(localeLanguage == null)
			{
				return "[Missing Language]";
			}

			var jToken = localeLanguage[name];
			if(jToken == null)
			{
				return "[Missing Locale]";
			}

			// choose a random value from array
			if(jToken is JArray)
			{
				var array = jToken.Children().Select(c => c.Value<string>()).ToArray();
				return array[new Random().Next(array.Length)];
			}

			return Util.FormatString(Extensions.Value<string>(jToken), args);
		}

		/// <summary>
		/// Clears the locale cache, forcing a reload on next access.
		/// </summary>
		public static void ReloadLocale()
		{
			// this reloads locale, in a way
			_localeCache = null;
		}

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
	}
}
