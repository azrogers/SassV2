using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SassV2
{
	public class Config
	{
		public string Token;
		public Dictionary<string, string> Roles = new Dictionary<string, string>();
		public string URL;
		public string ClientID;
		public string ClientSecret;
		public string ImgurClientId;
		public string[] DebugServers;
		public string CurrencyLayerKey;

		public Config(string configFile)
		{
			var file = JObject.Parse(File.ReadAllText(configFile));

			Token = ReadKey<string>(file, "token");
			URL = ReadKey<string>(file, "url");
			ClientID = ReadKey<string>(file, "client_id");
			ClientSecret = ReadKey<string>(file, "client_secret");
			ImgurClientId = ReadKey<string>(file, "imgur_client_id");
			CurrencyLayerKey = ReadKey<string>(file, "currency_layer");

			// read roles
			foreach (JProperty prop in file["roles"].ToObject<JObject>().Properties())
			{
				Roles[prop.Name] = prop.Value.ToString();
			}

			// read debug ignore servers
			DebugServers = file["debug_servers"].ToArray().Select(a => a.Value<string>()).ToArray();
		}

		public string GetRole(ulong userId)
		{
			if(Roles.ContainsKey(userId.ToString()))
				return Roles[userId.ToString()];
			return "user";
		}

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
