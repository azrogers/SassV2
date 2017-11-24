using NLog;
using SassV2.Commands;
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

		[WebApiHandler(HttpVerbs.Post, "/admin/leave/{serverId}")]
		public async Task<bool> LeaveServer(WebServer server, HttpListenerContext context, ulong serverId)
		{
			if(!AuthManager.IsAdmin(server, context, _bot))
			{
				return await ForbiddenError(server, context);
			}

			var guild = _bot.Client.GetGuild(serverId);
			await guild.LeaveAsync();

			return Redirect(server, context, "/admin");
		}

		[WebApiHandler(HttpVerbs.Get, "/admin/server/{serverId}")]
		public Task<bool> ServerPage(WebServer server, HttpListenerContext context, ulong serverId)
		{
			if(!AuthManager.IsAdmin(server, context, _bot))
			{
				return ForbiddenError(server, context);
			}

			var guild = _bot.Client.GetGuild(serverId);
			var users = guild.Users.OrderBy(u => u.Username);

			return ViewResponse(server, context, "admin/server", new {
				Title = guild.Name,
				Server = guild,
				Users = users,
				LastCommand = ActivityManager.GetLastActive(_bot, serverId) });
		}

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
			var bio = bios.Where(b => b.SharedServers.Any(kv => kv.Key == guild.Id)).FirstOrDefault();
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
		public Task<bool> AdminPage(WebServer server, HttpListenerContext context)
		{
			if(!AuthManager.IsAdmin(server, context, _bot))
			{
				return ForbiddenError(server, context);
			}

			var servers = _bot.Client.Guilds.OrderBy(g => g.Name);

			return ViewResponse(server, context, "admin/index", new { Title = "Admin", Servers = servers });
		}
	}
}
