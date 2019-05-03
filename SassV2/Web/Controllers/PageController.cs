using Discord;
using Newtonsoft.Json.Linq;
using NLog;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Constants;
using Unosquare.Labs.EmbedIO.Modules;

namespace SassV2.Web.Controllers
{
	public class PageController : BaseController
	{
#if DEBUG
		public const string AuthAppId = "sassv2-testing";
#else
		public const string AuthAppId = "sassv2";
#endif

		private DiscordBot _bot;
		private Logger _logger;

		public PageController(DiscordBot bot, ViewManager viewManager) : base(viewManager)
		{
			_bot = bot;
			_logger = LogManager.GetCurrentClassLogger();
		}

		[WebApiHandler(HttpVerbs.Post, "^/logout")]
		public bool Logout(WebServer server, HttpListenerContext context)
		{
			AuthManager.Logout(server, context);
			return Redirect(server, context, "/");
		}

		[WebApiHandler(HttpVerbs.Post, "^/hook/civ/{urlId}/{hookId}")]
		public async Task<bool> CivWebHookCallback(WebServer server, HttpListenerContext context, string urlId, string hookId)
		{
			_logger.Info($"civ web hook: {urlId}, {hookId}");
			if(!ulong.TryParse(urlId, out var serverId) || !_bot.ServerIds.Contains(serverId))
			{
				_logger.Error("Hook failed: invalid server");
				return JsonResponse(server, context, new
				{
					status = "error",
					message = "That server doesn't exist."
				});
			}

			// get body
			var body = context.RequestBody();
			if(body == null)
			{
				_logger.Error("Hook failed: no body");
				return JsonResponse(server, context, new
				{
					status = "error",
					message = "No request body provided."
				});
			}

			// parse json
			JObject obj;
			try
			{
				obj = JObject.Parse(body);
			}
			catch(Newtonsoft.Json.JsonReaderException)
			{
				_logger.Error("Hook failed: invalid json");
				return JsonResponse(server, context, new
				{
					status = "error",
					message = "Malformed webhook body. That's not JSON."
				});
			}

			// get game info
			var gameName = obj?["value1"]?.Value<string>();
			var playerName = obj?["value2"]?.Value<string>();
			var turnNum = obj?["value3"]?.Value<string>();

			if(gameName == null || playerName == null || turnNum == null)
			{
				_logger.Error("Hook failed: missing value1, value2, or value3");
				return JsonResponse(server, context, new
				{
					status = "error",
					message = "Malformed webhook body. Should have value1, value2, value3 fields."
				});
			}

			if(!int.TryParse(turnNum, out var turn))
			{
				_logger.Error("Hook failed: turn number not int");
				return JsonResponse(server, context, new
				{
					status = "error",
					message = "Turn number must be an int."
				});
			}

			// send update
			_logger.Info("Hook sending reminder.");
			await _bot.CivHook.SendReminder(serverId, hookId, gameName, playerName, turn);

			return JsonResponse(server, context, new { status = "OK" });
		}

		[WebApiHandler(HttpVerbs.Get, "^/hook/civ/{urlId}/{hookId}")]
		public bool DummyHook(WebServer server, HttpListenerContext context, string urlId, string hookId)
		{
			_logger.Info("Dummy GET route hit.");
			return JsonResponse(server, context, new { status = "error", message = "use POST" });
		}

		// handle callback from auth gateway
		[WebApiHandler(HttpVerbs.Get, "^/auth/callback")]
		public async Task<bool> AuthDiscordCallback(WebServer server, HttpListenerContext context)
		{
			var key = context.QueryString("userkey");
			if(key == null)
			{
				return await Error(server, context, "No userkey provided.");
			}

			// get user data
			var result = await Util.GetURLAsync("https://auth.anime.lgbt/auth/verify?userkey=" + key);
			var obj = JObject.Parse(result);
			if(!obj.Value<bool>("valid"))
			{
				return await Error(server, context, "Auth error: " + obj.Value<string>("error"));
			}

			// retrieve user id from bot
			var id = obj["data"].Value<string>("id");
			if(id == null || !ulong.TryParse(id, out var userId))
			{
				return await Error(server, context, "Invalid user ID returned from auth gateway.");
			}

			var user = _bot.Client.GetUser(userId);
			if(user == null)
			{
				return await Error(server, context, "User not found. Are you in a guild the bot is connected to?");
			}

			AuthManager.SaveUser(server, context, user);
			return Redirect(server, context, "/");
		}

		[WebApiHandler(HttpVerbs.Get, "^/auth/discord")]
		public bool AuthDiscord(WebServer server, HttpListenerContext context) =>
			Redirect(server, context, "https://auth.anime.lgbt/auth?appid=" + AuthAppId);

		[WebApiHandler(HttpVerbs.Get, "^/auth")]
		public async Task<bool> Auth(WebServer server, HttpListenerContext context)
		{
			if(AuthManager.IsAuthenticated(server, context))
			{
				return Redirect(server, context, context.QueryString("after"));
			}

			var code = context.QueryString("code");
			if(code == null)
			{
				return await Error(server, context, "No authentication code provided.");
			}

			var user = await AuthCodeManager.GetUser(_bot, code, _bot.GlobalDatabase);
			if(user == null)
			{
				return await Error(server, context, "The authentication code is invalid.");
			}

			AuthManager.SaveUser(server, context, user);
			return Redirect(server, context, context.QueryString("after"));
		}

		[WebApiHandler(HttpVerbs.Get, "^/images/{urlId}")]
		public Task<bool> GetImages(WebServer server, HttpListenerContext context, string urlId)
		{
			if(!ulong.TryParse(urlId, out var serverId) || !_bot.ServerIds.Contains(serverId))
			{
				return Error(server, context, "That server doesn't exist.");
			}

			var imageKeyValues = _bot.Database(serverId).GetKeysOfNamespace<string>("image");
			var images = imageKeyValues.OrderBy(k => k.Key);

			return ViewResponse(server, context, "images", new { Title = "Images", Images = images });
		}

		[WebApiHandler(HttpVerbs.Get, "^/quotes/{urlId}")]
		public async Task<bool> GetQuotes(WebServer server, HttpListenerContext context, string urlId)
		{
			if(!ulong.TryParse(urlId, out var serverId) || !_bot.ServerIds.Contains(serverId))
			{
				return await Error(server, context, "That server doesn't exist.");
			}

			var db = _bot.RelDatabase(serverId);
			var quotes = await Commands.Quote.All(db);

			var quoteList = quotes.Select((q) =>
				new Quote { Id = q.Id.Value, Content = q.Body, Author = q.Author, Source = q.Source });

			return await ViewResponse(server, context, "quotes", quoteList, new { Title = "Quotes" });
		}

		[WebApiHandler(HttpVerbs.Get, "^/fonts")]
		public Task<bool> GetFonts(WebServer server, HttpListenerContext context)
		{
			var files = Directory.EnumerateFiles("Fonts").Select(f =>
			{
				return Path.GetFileNameWithoutExtension(f.ToLower().Replace(' ', '_'));
			}).OrderBy(f => f);

			return ViewResponse(server, context, "fonts", new { Title = "Fonts", Fonts = files });
		}

		[WebApiHandler(HttpVerbs.Get, "^/servers")]
		public Task<bool> ListServers(WebServer server, HttpListenerContext context)
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

		[WebApiHandler(HttpVerbs.Get, new string[] { "^/images", "^/quotes" })]
		public bool ImageQuotesRedirect(WebServer server, HttpListenerContext context) => Redirect(server, context, "/servers");

		// this is our 404 handler
		[WebApiHandler(HttpVerbs.Get, "/")]
		public Task<bool> Index(WebServer server, HttpListenerContext context)
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
