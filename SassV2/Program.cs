using NLog;
using SassV2.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Log;
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

			var server = new WebServer(BotUrl, new NLogLogger());
			server.RegisterModule(new WebApiModule());
			server.Module<WebApiModule>().RegisterController<PageController>(() =>
			{
				return new PageController(bot);
			});
			server.RunAsync();

			bot.Start().GetAwaiter().GetResult();
		}
	}

	public class NLogLogger : ILog
	{
		private Logger _logger;

		public NLogLogger()
		{
			_logger = LogManager.GetCurrentClassLogger();
		}

		public void Info(object message)
		{
			_logger.Info(message);
		} 

		public void Error(object message)
		{
			_logger.Error(message);
		}

		public void Error(object message, Exception ex)
		{
			_logger.Error(ex);
		}

		public void InfoFormat(string format, params object[] args)
		{
			_logger.Info(string.Format(format, args));
		}

		public void DebugFormat(string format, params object[] args)
		{
			_logger.Debug(string.Format(format, args));
		}

		public void WarnFormat(string format, params object[] args)
		{
			_logger.Warn(string.Format(format, args));
		}

		public void ErrorFormat(string format, params object[] args)
		{
			_logger.Error(string.Format(format, args));
		}
	}
}
