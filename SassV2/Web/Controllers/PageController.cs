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
using NLog;

namespace SassV2.Web.Controllers
{
	public class PageController : BaseController
	{
		private DiscordBot _bot;
		private Logger _logger;

		public PageController(DiscordBot bot, ViewManager viewManager) : base(viewManager)
		{
			_bot = bot;
			_logger = LogManager.GetCurrentClassLogger();
		}

		[WebApiHandler(HttpVerbs.Post, "/logout")]
		public bool Logout(WebServer server, HttpListenerContext context)
		{
			AuthManager.Logout(server, context);
			return Redirect(server, context, "/");
		}

		[WebApiHandler(HttpVerbs.Get, "/auth")]
		public async Task<bool> Auth(WebServer server, HttpListenerContext context)
		{
			if(AuthManager.IsAuthenticated(server, context))
			{
				return Redirect(server, context, context.QueryString("after"));
			}

			var code = context.QueryString("code");
			if(code == null)
			{
				return Error(server, context, "No authentication code provided.");
			}

			var user = await AuthCodeManager.GetUser(_bot, code, _bot.GlobalDatabase);
			if(user == null)
			{
				return Error(server, context, "The authentication code is invalid.");
			}

			AuthManager.SaveUser(server, context, user);
			return Redirect(server, context, context.QueryString("after"));
		}

		[WebApiHandler(HttpVerbs.Get, "/images/{urlId}")]
		public bool GetImages(WebServer server, HttpListenerContext context, string urlId)
		{
			ulong serverId;
			if(!ulong.TryParse(urlId, out serverId) || !_bot.ServerIds.Contains(serverId))
			{
				return Error(server, context, "That server doesn't exist.");
			}

			var imageKeyValues = _bot.Database(serverId).GetKeysOfNamespace<string>("image");
			var images = imageKeyValues.OrderBy(k => k.Key);

			return ViewResponse(server, context, "images", new { Title = "Images", Images = images });
		}

		[WebApiHandler(HttpVerbs.Get, "/quotes/{urlId}")]
		public async Task<bool> GetQuotes(WebServer server, HttpListenerContext context, string urlId)
		{
			ulong serverId;
			if (!ulong.TryParse(urlId, out serverId) || !_bot.ServerIds.Contains(serverId))
			{
				return Error(server, context, "That server doesn't exist.");
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

			return ViewResponse(server, context, "quotes", quoteList, new { Title = "Quotes" });
		}

		[WebApiHandler(HttpVerbs.Get, "/fonts")]
		public bool GetFonts(WebServer server, HttpListenerContext context)
		{
			var files = Directory.EnumerateFiles("Fonts").Select(f =>
			{
				return Path.GetFileNameWithoutExtension(f.ToLower().Replace(' ', '_'));
			}).OrderBy(f => f);

			return ViewResponse(server, context, "fonts", new { Title = "Fonts", Fonts = files });
		}

		[WebApiHandler(HttpVerbs.Get, "/servers")]
		public bool ListServers(WebServer server, HttpListenerContext context)
		{
			var botGuilds = _bot.Client.Guilds;

			var servers =
				_bot.ServerIds
				.Select(i =>
					botGuilds.Where(s => s.Id == i).FirstOrDefault()
				)
				.Where(s => s != default(IGuild))
				.OrderBy(s => s.Name);

			return ViewResponse(server, context, "servers", new { Title = "Servers", Servers = servers });
		}

		[WebApiHandler(HttpVerbs.Get, new string[] { "/images", "/quotes" })]
		public bool ImageQuotesRedirect(WebServer server, HttpListenerContext context)
		{
			return Redirect(server, context, "/servers");
		}

		// this is our 404 handler
		[WebApiHandler(HttpVerbs.Get, "/")]
		public bool Index(WebServer server, HttpListenerContext context)
		{
			if(context.RequestPath() != "/")
			{
				context.Response.StatusCode = 404;
				return Error(server, context, "The specified path could not be found.");
			}

			return ViewResponse(server, context, "index", new { ClientID = _bot.Config.ClientID });
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
