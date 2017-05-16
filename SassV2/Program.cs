﻿using NLog;
using SassV2.Web;
using SassV2.Web.Controllers;
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
		private const string AssetPath = "../../Content";
#else
		private const string BotUrl = "http://*:1443/";
		private const string AssetPath = "Content";
#endif

		static void Main(string[] args)
		{
			Config config = new Config("config.json");
			DiscordBot bot = new DiscordBot(config);

			var viewManager = new ViewManager();

			var server = new WebServer(BotUrl, RoutingStrategy.Regex);
			server.RegisterModule(new StaticFilesModule(AssetPath));
			server.RegisterModule(new LocalSessionModule());
			server.RegisterModule(new WebApiModule());
			server.Module<WebApiModule>().RegisterController(() =>
			{
				return new BioController(bot, viewManager);
			});
			server.Module<WebApiModule>().RegisterController(() =>
			{
				return new PageController(bot, viewManager);
			});
			server.RunAsync();

			bot.Start().GetAwaiter().GetResult();
		}
	}
}
