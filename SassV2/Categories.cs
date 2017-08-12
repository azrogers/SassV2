using System;
using System.Collections.Generic;
using System.IO;

namespace SassV2
{
	public static class Categories
	{
		private static Dictionary<string, string> _categories = new Dictionary<string, string>()
		{
			{
				"Administration",
				"These commands are only accessible to users with the 'Administrator' permission in the server they're used on."
			},
			{
				"Animal",
				"Here's some really nice animals."
			},
			{
				"Bio",
				File.ReadAllText("Doc/category/bio.md")
			},
			{
				"Dumb",
				"These commands aren't really that useful."
			},
			{
				"General",
				"These are commands that don't fit into any other category."
			},
			{
				"Image",
				File.ReadAllText("Doc/category/image.md")
			},
			{
				"Quote",
				File.ReadAllText("Doc/category/quote.md")
			},
			{
				"Spam",
				"These commands aren't very useful and will probably do more harm than good, in the long run. That's why the [[ban]] command exists."
			},
			{
				"Useful",
				"These commands might actually help you."
			}
		};
		private static Dictionary<string, string> _lowercased = new Dictionary<string, string>();

		public static IEnumerable<string> Names => _categories.Keys;
		
		static Categories()
		{
			foreach(var key in _categories.Keys)
			{
				_lowercased[key.ToLower()] = key;
			}
		}

		public static string Uppercased(string key)
		{
			return _lowercased[key];
		}

		public static bool Exists(string key)
		{
			return _categories.ContainsKey(key) || _lowercased.ContainsKey(key);
		}

		public static string Description(string key)
		{
			if(_categories.ContainsKey(key))
				return _categories[key];
			return _categories[_lowercased[key]];
		}
	}
}
