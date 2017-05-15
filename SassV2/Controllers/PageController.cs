using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Unosquare.Labs.EmbedIO;
using Unosquare.Net;
using Unosquare.Labs.EmbedIO.Modules;
using Discord;
using System.Reactive.Linq;
using System.Web;
using NLog;

namespace SassV2.Controllers
{
	public class PageController : WebApiController
	{
		private DiscordBot _bot;
		private Logger _logger;
		private ViewManager _viewManager;

		public PageController(DiscordBot bot, ViewManager viewManager)
		{
			_bot = bot;
			_logger = LogManager.GetCurrentClassLogger();
			_viewManager = viewManager;
		}

		[WebApiHandler(HttpVerbs.Get, "/images/{urlId}")]
		public bool GetImages(WebServer server, HttpListenerContext context, string urlId)
		{
			ulong serverId;
			if(!ulong.TryParse(urlId, out serverId) || !_bot.ServerIds.Contains(serverId))
			{
				return Error(context, "That server doesn't exist.");
			}

			var imageKeyValues = _bot.Database(serverId).GetKeysOfNamespace<string>("image");
			var images = imageKeyValues.OrderBy(k => k.Key);

			return context.HtmlResponse(_viewManager.RenderView("images", new { Title = "Images", Images = images }));
		}

		[WebApiHandler(HttpVerbs.Get, "/quotes/{urlId}")]
		public async Task<bool> GetQuotes(WebServer server, HttpListenerContext context, string urlId)
		{
			ulong serverId;
			if (!ulong.TryParse(urlId, out serverId) || !_bot.ServerIds.Contains(serverId))
			{
				return Error(context, "That server doesn't exist.");
			}

			var db = _bot.RelDatabase(serverId);
			await Commands.QuoteCommand.InitializeDatabase(db);
			var cmd = db.BuildCommand("SELECT id,quote,author,source FROM quotes;");
			var reader = cmd.ExecuteReader();

			var quoteList = new List<Quote>();
			while(reader.Read())
			{
				var id = reader.GetInt64(0);
				var quote = reader.GetString(1);
				var author = reader.GetString(2);
				var source = reader.GetString(3);

				quoteList.Add(new Quote { Id = id, Content = quote, Author = author, Source = source });
			}

			return context.HtmlResponse(_viewManager.RenderView<List<Quote>>("quotes", quoteList, new { Title = "Quotes" }));
		}

		[WebApiHandler(HttpVerbs.Get, "/fonts")]
		public bool GetFonts(WebServer server, HttpListenerContext context)
		{
			var files = Directory.EnumerateFiles("Fonts").Select(f =>
			{
				return Path.GetFileNameWithoutExtension(f.ToLower().Replace(' ', '_'));
			});

			return context.HtmlResponse(_viewManager.RenderView("fonts", new { Title = "Fonts", Fonts = files }));
		}

		[WebApiHandler(HttpVerbs.Get, "/servers")]
		public async Task<bool> ListServers(WebServer server, HttpListenerContext context)
		{
			var botGuilds = await _bot.Client.GetGuildsAsync();

			var servers =
				_bot.ServerIds
				.Select(i =>
					botGuilds.Where(s => s.Id == i).FirstOrDefault()
				)
				.Where(s => s != default(IGuild))
				.OrderBy(s => s.Name);

			return context.HtmlResponse(_viewManager.RenderView("servers", new { Title = "Servers", Servers = servers }));
		}

		[WebApiHandler(HttpVerbs.Get, new string[] { "/images", "/quotes" })]
		public bool ImageQuotesRedirect(WebServer server, HttpListenerContext context)
		{
			return context.Redirect("/servers");
		}

		[WebApiHandler(HttpVerbs.Get, "/")]
		public bool Index(WebServer server, HttpListenerContext context)
		{
			return context.HtmlResponse(_viewManager.RenderView("index", new { ClientID = _bot.Config.ClientID }));
		}

		private bool Error(HttpListenerContext context, string message)
		{
			return context.HtmlResponse(_viewManager.RenderView("error", new { Title = "Error!", Message = message }));
		}

		public class Quote
		{
			public long Id;
			public string Content;
			public string Author;
			public string Source;
		}
	}
}
