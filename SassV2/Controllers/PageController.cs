using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Modules;
using Nustache.Core;
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

		public PageController(DiscordBot bot)
		{
			_bot = bot;
			_logger = LogManager.GetCurrentClassLogger();
		}

		[WebApiHandler(HttpVerbs.Get, "/")]
		public bool Index(WebServer server, HttpListenerContext context)
		{
			var inviteUrl = "https://discordapp.com/oauth2/authorize?client_id=" + _bot.Config.ClientID + "&scope=bot&permissions=104061952";
			return context.HtmlReponse(Render.FileToString("template.html", new
			{
				message = "BRANBOT TWO POINT DOH<br/>If you want SASS on your server, <a href='" + inviteUrl + "'>click here</a>."
			}));
		}

		[WebApiHandler(HttpVerbs.Get, new string[] { "/servers", "/images", "/quotes" })]
		public bool ListServers(WebServer server, HttpListenerContext context)
		{
			var servers =
				_bot.ServerIds
				.Select(i =>
					_bot.Client.Servers
					.Where(s => s.Id == i)
					.FirstOrDefault()
				)
				.Where(s => s != default(Server))
				.OrderBy(s => s.Name)
				.Select(s =>
				{
					return $"<li>{WebUtility.HtmlEncode(s.Name)} - <a href='/images/{s.Id}'>images</a> / <a href='/quotes/{s.Id}'>quotes</a></li>";
				});

			return context.HtmlReponse(Render.FileToString("template.html", new
			{
				title = "Servers",
				message = "<ul>" + string.Join("\n", servers) + "</ul>"
			}));
		}

		[WebApiHandler(HttpVerbs.Get, "/images/*")]
		public bool GetImages(WebServer server, HttpListenerContext context)
		{
			var urlId = context.Request.Url.Segments.Last();
			if(urlId == "images/")
			{
				return ListServers(server, context);
			}

			ulong serverId;
			if(!ulong.TryParse(urlId, out serverId))
			{
				throw new Exception("THE NOZZLE");
			}

			var imageKeyValues = _bot.Database(serverId).GetKeysOfNamespace<string>("image");
			var images = imageKeyValues
				.OrderBy(k => k.Key)
				.Select(kv => "<li>" + 
					WebUtility.HtmlEncode(kv.Key.Substring("image:".Length)) + 
					" - <a target='_blank' href='" + kv.Value + "'>" + kv.Value + "</a></li>");

			return context.HtmlReponse(Render.FileToString("template.html", new
			{
				title = "Images",
				message = "<ul>" + string.Join("\n", images) + "</ul>"
			}));
		}

		[WebApiHandler(HttpVerbs.Get, "/quotes/*")]
		public bool GetQuotes(WebServer server, HttpListenerContext context)
		{
			var urlId = context.Request.Url.Segments.Last();
			if(urlId == "quotes/")
			{
				return ListServers(server, context);
			}

			ulong serverId;
			if(!ulong.TryParse(urlId, out serverId))
			{
				throw new Exception("THE NOZZLE");
			}

			var db = _bot.RelDatabase(serverId);
			var cmd = db.BuildCommand("SELECT id,quote,author,source FROM quotes;");
			var reader = cmd.ExecuteReader();

			var quoteList = new List<string>();
			while(reader.Read())
			{
				var id = reader.GetInt64(0);
				var quote = WebUtility.HtmlEncode(reader.GetString(1));
				var author = WebUtility.HtmlEncode(reader.GetString(2));
				var source = WebUtility.HtmlEncode(reader.GetString(3));

				quoteList.Add($"<li>#{id}: \"{quote}\" - {author}, {source}</li>");
			}

			return context.HtmlReponse(Render.FileToString("template.html", new
			{
				title = "Quotes",
				message = "<ul>" + string.Join("\n", quoteList) + "</ul>"
			}));
		}

		[WebApiHandler(HttpVerbs.Get, "/fonts")]
		public bool GetFonts(WebServer server, HttpListenerContext context)
		{
			var files = Directory.EnumerateFiles("Fonts").Select(f =>
			{
				return "<li>" + Path.GetFileNameWithoutExtension(f.ToLower().Replace(' ', '_')) + "</li>";
			});

			return context.HtmlReponse(Render.FileToString("template.html", new
			{
				title = "Fonts",
				message = "<ul>" + string.Join("\n", files) + "</ul>"
			}));
		}
	}
}
