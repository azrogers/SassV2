using NLog;
using SassV2.Commands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Constants;
using Unosquare.Labs.EmbedIO.Modules;

namespace SassV2.Web.Controllers
{
	public class AdminController : BaseController
	{
		private DiscordBot _bot;
		private Logger _logger;

		public AdminController(DiscordBot bot, ViewManager viewManager) : base(viewManager)
		{
			_bot = bot;
			_logger = LogManager.GetCurrentClassLogger();
		}

		// edit the stack
		[WebApiHandler(HttpVerbs.Post, "/admin/stack")]
		public async Task<bool> EditStack(WebServer server, HttpListenerContext context)
		{
			if(!AuthManager.IsAdmin(server, context, _bot))
			{
				return await ForbiddenError(server, context);
			}

			var data = context.RequestFormDataDictionary();
			if(!data.ContainsKey("stack"))
			{
				return await Error(server, context, "No stack posted.");
			}

			if(File.Exists("requests.txt"))
			{
				File.WriteAllText("requests-bkp.txt", File.ReadAllText("requests.txt"));
			}

			File.WriteAllText("requests.txt", data["stack"].ToString().Trim() + "\n");
			return Redirect(server, context, "/admin/");
		}

		// print the stack
		[WebApiHandler(HttpVerbs.Get, "/admin/stack")]
		public Task<bool> ViewStack(WebServer server, HttpListenerContext context)
		{
			if(!AuthManager.IsAdmin(server, context, _bot))
			{
				return ForbiddenError(server, context);
			}

			string stack;
			if(!File.Exists("requests.txt"))
			{
				stack = "";
			}
			else
			{
				stack = File.ReadAllText("requests.txt");
			}

			return ViewResponse(server, context, "admin/stack", new { Title = "Edit Stack", Stack = stack.Trim() });
		}

		[WebApiHandler(HttpVerbs.Post, "/admin/quotes/{serverId}")]
		public async Task<bool> DeleteQuotes(WebServer server, HttpListenerContext context, ulong serverId)
		{
			if(!AuthManager.IsAdminOfServer(server, context, _bot, serverId))
			{
				return await ForbiddenError(server, context);
			}

			var guild = _bot.Client.GetGuild(serverId);
			if(guild == null)
			{
				return await Error(server, context, "Server not found.");
			}

			var db = _bot.RelDatabase(serverId);

			var postData = context.RequestFormDataDictionary();
			var hardDelete = postData.ContainsKey("hard_delete");
			var quotes = Util.ReadFormArray(postData, "quote_delete");
			if(!quotes.Any())
				return await Error(server, context, "No quotes provided.");

			foreach(var quote in quotes)
			{
				var quoteId = long.Parse(quote);
				if(hardDelete)
					await Quote.DeleteQuote(db, quoteId);
				else
				{
					var quoteObj = new Quote(db, quoteId);
					quoteObj.SoftDeleted = !quoteObj.SoftDeleted;
					await quoteObj.Save();
				}
			}

			return await ListQuotesAdmin(server, context, serverId);
		}

		[WebApiHandler(HttpVerbs.Get, "/admin/quotes/{serverId}")]
		public async Task<bool> ListQuotesAdmin(WebServer server, HttpListenerContext context, ulong serverId)
		{
			if(!AuthManager.IsAdminOfServer(server, context, _bot, serverId))
			{
				return await ForbiddenError(server, context);
			}

			var guild = _bot.Client.GetGuild(serverId);
			if(guild == null)
			{
				return await Error(server, context, "Server not found.");
			}

			var quotes = await Quote.All(_bot.RelDatabase(serverId));
			return await ViewResponse(server, context, "admin/quotes", new { Title = "List Quotes", Quotes = quotes, ServerId = serverId });
		}

		// leave a server
		[WebApiHandler(HttpVerbs.Post, "/admin/leave/{serverId}")]
		public async Task<bool> LeaveServer(WebServer server, HttpListenerContext context, ulong serverId)
		{
			if(!AuthManager.IsAdminOfServer(server, context, _bot, serverId))
			{
				return await ForbiddenError(server, context);
			}

			var guild = _bot.Client.GetGuild(serverId);
			await guild.LeaveAsync();

			return Redirect(server, context, "/admin");
		}

		// print a server's info
		[WebApiHandler(HttpVerbs.Get, "/admin/server/{serverId}")]
		public async Task<bool> ServerPage(WebServer server, HttpListenerContext context, ulong serverId)
		{
			if(!AuthManager.IsAdminOfServer(server, context, _bot, serverId))
			{
				return await ForbiddenError(server, context);
			}

			var guild = _bot.Client.GetGuild(serverId);
			var users = guild.Users.OrderBy(u => u.Username);
			var lastCommand = await ActivityManager.GetLastActive(_bot, serverId);

			return await ViewResponse(server, context, "admin/server", new
			{
				Title = guild.Name,
				Server = guild,
				Users = users,
				LastCommand = lastCommand == DateTime.MinValue ? "never" : lastCommand.ToString("f")
			});
		}

		// allow the admin to edit a user's bio
		[WebApiHandler(HttpVerbs.Post, "/admin/bio")]
		public async Task<bool> BioEdit(WebServer server, HttpListenerContext context)
		{
			if(!AuthManager.IsAdmin(server, context, _bot))
			{
				return await ForbiddenError(server, context);
			}

			var data = context.RequestFormDataDictionary();
			if(!data.ContainsKey("user") || !data.ContainsKey("server"))
			{
				return await Error(server, context, "That's not right.");
			}

			var guild = _bot.Client.GetGuild(ulong.Parse(data["server"].ToString()));
			var user = _bot.Client.GetUser(ulong.Parse(data["user"].ToString()));
			var bios = await Bio.GetBios(_bot, user);
			var bio = bios.Where(b => b.SharedGuilds.Any(kv => kv.Key == guild.Id)).FirstOrDefault();
			long bioId;
			if(bio == null)
			{
				bioId = await Bio.CreateBio(user, _bot.GlobalDatabase);
			}
			else
			{
				bioId = bio.Id;
			}

			return Redirect(server, context, "/bio/edit/" + bioId);
		}

		[WebApiHandler(HttpVerbs.Get, "/admin")]
		public async Task<bool> AdminPage(WebServer server, HttpListenerContext context)
		{
			if(!AuthManager.IsAuthenticated(server, context))
			{
				return await ForbiddenError(server, context);
			}

			var user = AuthManager.GetUser(server, context, _bot);
			var servers = _bot.GuildsWithUserAsAdmin(user)
				.OrderBy(g => g.Name);

			var serverModels = new List<ServerModel>();
			foreach(var dServer in servers)
			{
				var model = new ServerModel
				{
					Name = dServer.Name,
					Id = dServer.Id,
					HasActivity = (await ActivityManager.GetLastActive(_bot, dServer.Id)) != DateTime.MinValue
				};
				serverModels.Add(model);
			}

			return await ViewResponse(server, context, "admin/index", new
			{
				Title = "Admin",
				Servers = serverModels,
				IsGlobalAdmin = AuthManager.IsAdmin(server, context, _bot)
			});
		}

		public class ServerModel
		{
			public ulong Id;
			public string Name;
			public bool HasActivity;
		}
	}
}
