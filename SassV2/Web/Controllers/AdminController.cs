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

			// backup existing requests
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

			// find discord server with this id
			var guild = _bot.Client.GetGuild(serverId);
			if(guild == null)
			{
				return await Error(server, context, "Server not found.");
			}

			var db = _bot.RelDatabase(serverId);

			// read in quotes to delete from form
			var postData = context.RequestFormDataDictionary();
			var hardDelete = postData.ContainsKey("hard_delete");
			var quotes = Util.ReadFormArray(postData, "quote_delete");
			if(!quotes.Any())
				return await Error(server, context, "No quotes provided.");

			// delete each quote
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

		[WebApiHandler(HttpVerbs.Post, "/admin/images/{serverId}")]
		public async Task<bool> DeleteImages(WebServer server, HttpListenerContext context, ulong serverId)
		{
			if(!AuthManager.IsAdminOfServer(server, context, _bot, serverId))
			{
				return await ForbiddenError(server, context);
			}

			// find discord server with this id
			var guild = _bot.Client.GetGuild(serverId);
			if(guild == null)
			{
				return await Error(server, context, "Server not found.");
			}

			var db = _bot.Database(serverId);

			// read in quotes to delete from form
			var postData = context.RequestFormDataDictionary();
			var images = Util.ReadFormArray(postData, "image_delete");
			if(!images.Any())
			{
				return await Error(server, context, "No images provided.");
			}

			// delete each quote
			foreach(var image in images)
			{
				if(!image.StartsWith("image:"))
				{
					continue;
				}

				db.InvalidateObject(image);
			}

			return await ListImagesAdmin(server, context, serverId);
		}

		[WebApiHandler(HttpVerbs.Get, "/admin/images/{serverId}")]
		public async Task<bool> ListImagesAdmin(WebServer server, HttpListenerContext context, ulong serverId)
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

			var imageKeyValues = _bot.Database(serverId).GetKeysOfNamespace<string>("image");
			var images = imageKeyValues.OrderBy(k => k.Key);
			return await ViewResponse(server, context, "admin/images", new { Title = "List Images", Images = images, ServerId = serverId });
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

		[WebApiHandler(HttpVerbs.Post, "/admin/roles/{serverId}")]
		public async Task<bool> SetManagedRoles(WebServer server, HttpListenerContext context, ulong serverId)
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

			var db = _bot.Database(serverId);

			// read in quotes to delete from form
			var postData = context.RequestFormDataDictionary();
			var managedStr = Util.ReadFormArray(postData, "role_managed");
			var managed = new HashSet<ulong>();

			foreach(var str in managedStr)
			{
				// not a valid id
				if(!ulong.TryParse(str, out var roleId))
				{
					continue;
				}

				// role doesn't exist in this guild
				if(guild.GetRole(roleId) == null)
				{
					continue;
				}

				managed.Add(roleId);
			}

			db.InsertObject("roles:managed", managed.ToArray());
			return await ViewRoles(server, context, serverId);
		}

		[WebApiHandler(HttpVerbs.Get, "/admin/roles/{serverId}")]
		public async Task<bool> ViewRoles(WebServer server, HttpListenerContext context, ulong serverId)
		{
			if(!AuthManager.IsAdminOfServer(server, context, _bot, serverId))
			{
				return await ForbiddenError(server, context);
			}

			var guild = _bot.Client.GetGuild(serverId);
			var permissions = guild.CurrentUser.GuildPermissions;

			// can't manage roles, don't go any further
			if(!permissions.ManageRoles)
			{
				return await ViewResponse(server, context, "admin/roles", new
				{
					Title = guild.Name + " Roles",
					Roles = new RoleModel[] { },
					CanEdit = false,
					ServerId = serverId
				});
			}

			var db = _bot.Database(serverId);
			var managedRoles = new HashSet<ulong>(db.GetOrCreateObject("roles:managed", () => new ulong[] { }));

			// find the highest role that we have
			var myRole =
				guild.CurrentUser.Roles
				.Where(r => r.Permissions.ManageRoles)
				.OrderByDescending(r => r.Position)
				.First();

			// find all roles we can manage (position lower than our role)
			var roles =
				guild.Roles
				.Where(r => r.Position < myRole.Position && !r.IsEveryone)
				.OrderByDescending(r => r.Position)
				.Select(r => new RoleModel { IsManaged = managedRoles.Contains(r.Id), Name = r.Name, Id = r.Id })
				.ToArray();

			return await ViewResponse(server, context, "admin/roles", new
			{
				title = guild.Name + " Roles",
				Roles = roles,
				CanEdit = true,
				ServerId = serverId
			});
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

			// get post data
			var data = context.RequestFormDataDictionary();
			if(!data.ContainsKey("user") || !data.ContainsKey("server"))
			{
				return await Error(server, context, "That's not right.");
			}

			// find bio shared with this server
			var guild = _bot.Client.GetGuild(ulong.Parse(data["server"].ToString()));
			var user = _bot.Client.GetUser(ulong.Parse(data["user"].ToString()));
			var bios = await Bio.GetBios(_bot, user);
			var bio = bios.Where(b => b.SharedGuilds.Any(kv => kv.Key == guild.Id)).FirstOrDefault();

			long bioId;
			if(bio == null)
			{
				// no bio for this server
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

			// get a list of servers this user is admin of
			var user = AuthManager.GetUser(server, context, _bot);
			var servers = _bot.GuildsWithUserAsAdmin(user)
				.OrderBy(g => g.Name);

			// put discord servers into server models
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

		public class RoleModel
		{
			public ulong Id;
			public string Name;
			public bool IsManaged;
		}
	}
}
