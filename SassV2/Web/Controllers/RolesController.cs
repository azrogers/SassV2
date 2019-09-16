using NLog;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Constants;
using Unosquare.Labs.EmbedIO.Modules;

namespace SassV2.Web.Controllers
{
	public class RolesController : BaseController
	{
		private DiscordBot _bot;
		private Logger _logger;

		public RolesController(DiscordBot bot, ViewManager viewManager)
			: base(viewManager)
		{
			_bot = bot;
			_logger = LogManager.GetCurrentClassLogger();
		}

		[WebApiHandler(HttpVerbs.Post, "/roles/manage/{serverId}")]
		public async Task<bool> SetRoles(WebServer server, HttpListenerContext context, ulong serverId)
		{
			if(!AuthManager.IsAuthenticated(server, context))
			{
				return await ForbiddenError(server, context);
			}

			var guild = _bot.Client.GetGuild(serverId);
			if(guild == null)
			{
				return await Error(server, context, "The specified server doesn't exist.");
			}

			var user = AuthManager.GetUser(server, context, _bot);
			var guildUser = guild.GetUser(user.Id);
			if(guildUser == null)
			{
				return await Error(server, context, "You aren't a member of this server!");
			}

			var managedRoles = _bot.Database(guild.Id).GetOrCreateObject("roles:managed", () => new ulong[] { });

			var postData = context.RequestFormDataDictionary();
			var roles = Util.ReadFormArray(postData, "roles");

			var userRoles = new HashSet<ulong>();
			foreach(var r in roles)
			{
				if(!ulong.TryParse(r, out var roleId))
				{
					continue;
				}

				if(guild.GetRole(roleId) == null || !managedRoles.Contains(roleId))
				{
					continue;
				}

				userRoles.Add(roleId);
			}

			// remove the user from roles that aren't checked but are managed
			var rolesToDelete =
				managedRoles
				.Where(r => !userRoles.Contains(r))
				.Select(r => guild.GetRole(r))
				.Where(r => r != null);

			// remove first
			await guildUser.RemoveRolesAsync(rolesToDelete);
			// then add
			await guildUser.AddRolesAsync(userRoles.Select(r => guild.GetRole(r)));

			SetFlashMessage(server, context, "Updated roles.");

			return await ManageRoles(server, context, serverId);
		}

		[WebApiHandler(HttpVerbs.Get, "/roles/manage/{serverId}")]
		public async Task<bool> ManageRoles(WebServer server, HttpListenerContext context, ulong serverId)
		{
			if(!AuthManager.IsAuthenticated(server, context))
			{
				return await ForbiddenError(server, context);
			}

			var guild = _bot.Client.GetGuild(serverId);
			if(guild == null)
			{
				return await Error(server, context, "The specified server doesn't exist.");
			}

			var user = AuthManager.GetUser(server, context, _bot);
			var guildUser = guild.GetUser(user.Id);
			if(guildUser == null)
			{
				return await Error(server, context, "You aren't a member of this server!");
			}

			// find all roles that are managed by sass that exist in this guild
			var managedRoles =
				_bot.Database(guild.Id)
				.GetObject<ulong[]>("roles:managed")?
				.Select(r => guild.GetRole(r))
				.Where(r => r != null);

			if(managedRoles == null || !managedRoles.Any())
			{
				return await Error(server, context, "This server doesn't have any roles managed by SASS.");
			}

			// create models for them
			var models = managedRoles
				.OrderByDescending(r => r.Position)
				.Select(r => new RolesModel()
				{
					Id = r.Id,
					Name = r.Name,
					IsMember = guildUser.Roles.Any(ur => ur.Id == r.Id)
				});

			return await ViewResponse(server, context, "roles/edit", new
			{
				Title = $"Manage {guild.Name} Roles",
				Roles = models,
				ServerId = guild.Id,
				ServerName = guild.Name
			});
		}

		[WebApiHandler(HttpVerbs.Get, "/roles/manage")]
		public async Task<bool> ListServers(WebServer server, HttpListenerContext context)
		{
			if(!AuthManager.IsAuthenticated(server, context))
			{
				return await ForbiddenError(server, context);
			}

			var user = AuthManager.GetUser(server, context, _bot);
			// find all guilds that have roles managed by sass
			var guilds =
				_bot.Client.GuildsContainingUser(user)
				.Where(g => _bot.Database(g.Id).GetObject<ulong[]>("roles:managed") != default(ulong[]))
				.Select(g => new RolesServer() { Id = g.Id, Name = g.Name })
				.OrderBy(g => g.Name);

			return await ViewResponse(server, context, "roles/index", new { Title = "Manage Roles", Servers = guilds.ToArray() });
		}

		public class RolesServer
		{
			public ulong Id;
			public string Name;
		}

		public class RolesModel
		{
			public ulong Id;
			public string Name;
			public bool IsMember;
		}
	}
}