using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Modules;
using Unosquare.Net;
using Discord;
using NLog;
using SassV2.Commands;

namespace SassV2.Web.Controllers
{
	public class BioController : BaseController
	{
		private DiscordBot _bot;
		private Logger _logger;
		private ViewManager _viewManager;

		public BioController(DiscordBot bot, ViewManager viewManager)
			: base(viewManager)
		{
			_bot = bot;
			_logger = LogManager.GetCurrentClassLogger();
			_viewManager = viewManager;
		}

		[WebApiHandler(HttpVerbs.Post, "/bio/new")]
		public async Task<bool> CreateBio(WebServer server, HttpListenerContext context)
		{
			if (!AuthManager.IsAuthenticated(server, context))
			{
				return ForbiddenError(server, context);
			}

			var id = await Bio.CreateBio(AuthManager.GetUser(server, context, _bot), _bot.GlobalDatabase);
			return Redirect(server, context, "/bio/edit/" + id);
		}

		[WebApiHandler(HttpVerbs.Post, "/bio/edit/{id}")]
		public async Task<bool> EditBioPost(WebServer server, HttpListenerContext context, long id)
		{
			if (!AuthManager.IsAuthenticated(server, context))
			{
				return ForbiddenError(server, context);
			}

			var user = AuthManager.GetUser(server, context, _bot);
			var bio = await Bio.GetBio(_bot, id, _bot.GlobalDatabase, true);
			if (bio == null)
			{
				return Error(server, context, "The specified bio doesn't exist!");
			}
			else if(bio.Owner != user.Id && _bot.Config.GetRole(user.Id) != "admin")
			{
				return ForbiddenError(server, context);
			}

			var data = context.RequestFormDataDictionary();
			foreach(var field in bio.Fields)
			{
				if (!data.ContainsKey(field.Name) || string.IsNullOrWhiteSpace(data[field.Name].ToString()))
					continue;
				field.Value = data[field.Name].ToString();
			}
			
			if(data.ContainsKey("servers"))
			{
				IEnumerable<KeyValuePair<ulong, string>> servers;
				if (data["servers"] is string)
				{
					servers = new KeyValuePair<ulong, string>[] { new KeyValuePair<ulong, string>(ulong.Parse(data["servers"].ToString()), null) };
				}
				else
				{
					servers = (data["servers"] as List<string>).Select(s => new KeyValuePair<ulong, string>(ulong.Parse(s), null));
				}
				bio.SharedServers = servers.ToList();
			}

			await Bio.SaveBio(bio, _bot.GlobalDatabase);

			return Redirect(server, context, "/bio/edit");
		}

		[WebApiHandler(HttpVerbs.Get, "/bio/edit/{id}")]
		public async Task<bool> EditBio(WebServer server, HttpListenerContext context, long id)
		{
			if (!AuthManager.IsAuthenticated(server, context))
			{
				return ForbiddenError(server, context);
			}

			var user = AuthManager.GetUser(server, context, _bot);
			var bio = await Bio.GetBio(_bot, id, _bot.GlobalDatabase, true);
			if (bio == null)
			{
				return Error(server, context, "The specified bio doesn't exist!");
			}
			else if (bio.Owner != user.Id && _bot.Config.GetRole(user.Id) != "admin")
			{
				return ForbiddenError(server, context);
			}

			var servers = _bot.GuildsContainingUser(AuthManager.GetUser(server, context, _bot));
			var serversInt = servers
				.Select(s => new BioServerInt
				{
					Id = s.Id,
					Name = s.Name,
					Selected = bio.SharedServers.Any(kv => kv.Key == s.Id)
				}).OrderBy(s => s.Name);

			return ViewResponse(server, context, "bio/edit", bio, new { Title = "Edit Bio", Servers = serversInt });
		}

		[WebApiHandler(HttpVerbs.Get, "/bio/edit")]
		public async Task<bool> EditBio(WebServer server, HttpListenerContext context)
		{
			if(!AuthManager.IsAuthenticated(server, context))
			{
				return ForbiddenError(server, context);
			}

			var bios = await Bio.GetBios(_bot, AuthManager.GetUser(server, context, _bot), _bot.GlobalDatabase);

			return ViewResponse(server, context, "bio/index", new { Title = "Edit Bios", Bios = bios });
		}

		public class BioServerInt
		{
			public ulong Id;
			public string Name;
			public bool Selected;
		}
	}
}