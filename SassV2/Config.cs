using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace SassV2
{
	public class Config
	{
		/// <summary>
		/// The Discord API token. Very much required.
		/// </summary>
		public string Token;

		/// <summary>
		/// A list of botwide roles assigned to users. Only one is supported - admin.
		/// Should be in the config file in the form "userid": "role"
		/// </summary>
		public Dictionary<string, string> Roles = new Dictionary<string, string>();

		/// <summary>
		/// The URL that the web panel is hosted at.
		/// </summary>
		public string URL;

		/// <summary>
		/// The Discord Client ID of the bot, used for invite links.
		/// </summary>
		public string ClientID;

		/// <summary>
		/// An Imgur API client ID, for listing images from Imgur subreddits.
		/// </summary>
		public string ImgurClientId;

		/// <summary>
		/// A list of servers whose messages will be monitored in DEBUG. 
		/// These should be set to the IDs of the servers you use for testing.
		/// This is to avoid printing every single message from every single server
		/// to console while debugging (if you have a lot of servers).
		/// </summary>
		public string[] DebugServers;

		/// <summary>
		/// The API key for CurrencyLayer, used to lookup current currency values.
		/// </summary>
		public string CurrencyLayerKey;

		/// <summary>
		/// API key for the Steam web API.
		/// </summary>
		public string SteamAPIKey;

		public Config(string configFile)
		{
			var file = JObject.Parse(File.ReadAllText(configFile));

			Token = ReadKey<string>(file, "token");
			URL = ReadKey<string>(file, "url");
			ClientID = ReadKey<string>(file, "client_id");
			ImgurClientId = ReadKey<string>(file, "imgur_client_id");
			CurrencyLayerKey = ReadKey<string>(file, "currency_layer");
			SteamAPIKey = ReadKey<string>(file, "steam_api");

			// read roles
			foreach(JProperty prop in file["roles"].ToObject<JObject>().Properties())
			{
				Roles[prop.Name] = prop.Value.ToString();
			}

			// read debug ignore servers
			DebugServers = file["debug_servers"].ToArray().Select(a => a.Value<string>()).ToArray();
		}

		/// <summary>
		/// Gets the role of a user set in this file. Returns "user" if none is set.
		/// </summary>
		public string GetRole(ulong userId)
		{
			if(Roles.ContainsKey(userId.ToString()))
				return Roles[userId.ToString()];
			return "user";
		}

		// reads a key from the JSON file.
		private T ReadKey<T>(JObject file, string name)
		{
			string val = Environment.GetEnvironmentVariable(name);
			if(val == null)
			{
				return file[name].Value<T>();
			}

			var converter = TypeDescriptor.GetConverter(typeof(T));
			if(converter != null)
			{
				return (T)converter.ConvertFromString(val);
			}
			return default(T);
		}
	}
}
