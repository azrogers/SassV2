using NLog;
using System.Linq;
using System.Threading.Tasks;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Modules;
using Unosquare.Net;
using SassV2.Commands;

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

		[WebApiHandler(HttpVerbs.Post, "/admin/leave/{serverId}")]
		public async Task<bool> LeaveServer(WebServer server, HttpListenerContext context, ulong serverId)
		{
			if (!AuthManager.IsAdmin(server, context, _bot))
			{
				return ForbiddenError(server, context);
			}

			var guild = _bot.Client.GetGuild(serverId);
			await guild.LeaveAsync();

			return Redirect(server, context, "/admin");
		}

		[WebApiHandler(HttpVerbs.Get, "/admin/server/{serverId}")]
		public bool ServerPage(WebServer server, HttpListenerContext context, ulong serverId)
		{
			if (!AuthManager.IsAdmin(server, context, _bot))
			{
				return ForbiddenError(server, context);
			}

			var guild = _bot.Client.GetGuild(serverId);
			var users = guild.Users.OrderBy(u => u.Username);

			return ViewResponse(server, context, "admin/server", new { Title = guild.Name, Server = guild, Users = users });
		}

		[WebApiHandler(HttpVerbs.Post, "/admin/bio")]
		public async Task<bool> BioEdit(WebServer server, HttpListenerContext context)
		{
			if (!AuthManager.IsAdmin(server, context, _bot))
			{
				return ForbiddenError(server, context);
			}

			var data = context.RequestFormDataDictionary();
			if(!data.ContainsKey("user") || !data.ContainsKey("server"))
			{
				return Error(server, context, "That's not right.");
			}

			var guild = _bot.Client.GetGuild(ulong.Parse(data["server"].ToString()));
			var user = _bot.Client.GetUser(ulong.Parse(data["user"].ToString()));
			var bios = await Bio.GetBios(_bot, user, _bot.GlobalDatabase);
			var bio = bios.Where(b => b.SharedServers.Any(kv => kv.Key == guild.Id)).FirstOrDefault();
			long bioId;
			if (bio == null)
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
		public bool AdminPage(WebServer server, HttpListenerContext context)
		{
			if (!AuthManager.IsAdmin(server, context, _bot))
			{
				return ForbiddenError(server, context);
			}

			var servers = _bot.Client.Guilds.OrderBy(g => g.Name);

			return ViewResponse(server, context, "admin/index", new { Title = "Admin", Servers = servers });
		}
	}
}
