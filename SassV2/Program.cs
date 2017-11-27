using NLog;
using SassV2.Web;
using SassV2.Web.Controllers;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Modules;

namespace SassV2
{
	public class Program
	{

#if DEBUG
		private const string BotUrl = "http://localhost:1443/";
		private const string AssetPath = "Content";
#else
		private const string BotUrl = "http://*:1443/";
		private const string AssetPath = "Content";
#endif

		static void Main(string[] args)
		{
			LogManager.Configuration = new NLog.Config.XmlLoggingConfiguration("NLog.Config", false);
			Config config = new Config("config.json");
			DiscordBot bot = new DiscordBot(config);

			var viewManager = new ViewManager();

			var server = new WebServer(BotUrl, Unosquare.Labs.EmbedIO.Constants.RoutingStrategy.Regex);
			server.RegisterModule(new StaticFilesModule(AssetPath));
			server.RegisterModule(new LocalSessionModule());
			server.RegisterModule(new WebApiModule());
			server.Module<WebApiModule>().RegisterController(() =>
			{
				return new BankController(bot, viewManager);
			});
			server.Module<WebApiModule>().RegisterController(() =>
			{
				return new BioController(bot, viewManager);
			});
			server.Module<WebApiModule>().RegisterController(() =>
			{
				return new DocsController(bot, viewManager);
			});
			server.Module<WebApiModule>().RegisterController(() =>
			{
				return new AdminController(bot, viewManager);
			});
			// page controller should be last!
			server.Module<WebApiModule>().RegisterController(() =>
			{
				return new PageController(bot, viewManager);
			});
			server.RunAsync();

			bot.Start().GetAwaiter().GetResult();

			LogManager.GetCurrentClassLogger().Error("Program should never get to this point.");
		}
	}
}
