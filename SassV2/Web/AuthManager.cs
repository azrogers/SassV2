using Discord;
using System.Net;
using Unosquare.Labs.EmbedIO;

namespace SassV2.Web
{
	public static class AuthManager
	{
		private const string SessionAuthKey = "discord_user_id";
		private const string SessionUsernameKey = "discord_user_name";
		
		/// <summary>
		/// Has the user been authenticated?
		/// </summary>
		public static bool IsAuthenticated(WebServer server, HttpListenerContext context)
		{
			var session = server.GetSession(context);
			return session.Data.ContainsKey(SessionAuthKey);
		}

		/// <summary>
		/// Is the user a bot-wide admin?
		/// </summary>
		public static bool IsAdmin(WebServer server, HttpListenerContext context, DiscordBot bot)
		{
			if (!IsAuthenticated(server, context))
				return false;
			return bot.Config.GetRole((ulong)server.GetSession(context)[SessionAuthKey]) == "admin";
		}

		/// <summary>
		/// Is the user an admin of this specific server?
		/// </summary>
		public static bool IsAdminOfServer(WebServer server, HttpListenerContext context, DiscordBot bot, ulong serverId)
		{
			if(!IsAuthenticated(server, context))
				return false;
			var id = (ulong)server.GetSession(context)[SessionAuthKey];
			return
				bot.Config.GetRole(id) == "admin" ||
				bot.Client.GetGuild(serverId).GetUser(id).GuildPermissions.Administrator;
		}

		/// <summary>
		/// Returns the IUser object if authenticated, or null.
		/// </summary>
		public static IUser GetUser(WebServer server, HttpListenerContext context, DiscordBot bot)
		{
			if (!IsAuthenticated(server, context)) return null;
			var id = (ulong)server.GetSession(context)[SessionAuthKey];
			return bot.Client.GetUser(id);
		}

		/// <summary>
		/// Returns only the user's username if authenticated, or null.
		/// </summary>
		public static string GetUsername(WebServer server, HttpListenerContext context)
		{
			if (!IsAuthenticated(server, context)) return null;
			return server.GetSession(context)[SessionUsernameKey].ToString();
		}

		/// <summary>
		/// Saves an IUser to the session.
		/// </summary>
		public static void SaveUser(WebServer server, HttpListenerContext context, IUser user)
		{
			var session = server.GetSession(context);
			session[SessionAuthKey] = user.Id;
			session[SessionUsernameKey] = user.Username;
		}

		/// <summary>
		/// Clears the user's session.
		/// </summary>
		public static void Logout(WebServer server, HttpListenerContext context)
		{
			server.DeleteSession(context);
		}
	}
}
