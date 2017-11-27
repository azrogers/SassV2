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
		/// Returns a string from locale.json, given the name and args.
		/// </summary>
		/// <param name="name">The name of the locale string.</param>
		/// <param name="args">The arguments.</param>
		/// <returns></returns>
		public static string GetString(string lang, string name, object args = null)
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

			var localeLanguage = _languages.ContainsKey(lang) ? _languages[lang] : null;
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

			return Util.FormatString(Extensions.Value<string>(jToken), args);
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
