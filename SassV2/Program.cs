using NLog;
using SassV2.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Modules;

namespace SassV2
{
	public class Program
	{

#if DEBUG
		private const string BotUrl = "http://localhost:1443/";
#else
		private const string BotUrl = "http://*:1443/";
#endif

		static void Main(string[] args)
		{
			Config config = new Config("config.json");
			DiscordBot bot = new DiscordBot(config);

			var viewManager = new ViewManager();

			var server = new WebServer(BotUrl, RoutingStrategy.Regex);
			server.RegisterModule(new WebApiModule());
			server.Module<WebApiModule>().RegisterController(() =>
			{
				return new PageController(bot, viewManager);
			});
			server.RunAsync();

			bot.Start().GetAwaiter().GetResult();
		}
	}
}
