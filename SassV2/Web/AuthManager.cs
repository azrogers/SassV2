using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unosquare.Labs.EmbedIO;
using Unosquare.Net;
using Discord;

namespace SassV2.Web
{
	public static class AuthManager
	{
		private const string SessionAuthKey = "discord_user_id";
		private const string SessionUsernameKey = "discord_user_name";

		public static bool IsAuthenticated(WebServer server, HttpListenerContext context)
		{
			var session = server.GetSession(context);
			return session.Data.ContainsKey(SessionAuthKey);
		}

		public static async Task<IUser> GetUser(WebServer server, HttpListenerContext context, DiscordBot bot)
		{
			if (!IsAuthenticated(server, context)) return null;
			var id = (ulong)server.GetSession(context)[SessionAuthKey];
			return bot.Client.GetUser(id);
		}

		public static string GetUsername(WebServer server, HttpListenerContext context)
		{
			if (!IsAuthenticated(server, context)) return null;
			return server.GetSession(context)[SessionUsernameKey].ToString();
		}

		public static void SaveUser(WebServer server, HttpListenerContext context, IUser user)
		{
			var session = server.GetSession(context);
			session[SessionAuthKey] = user.Id;
			session[SessionUsernameKey] = user.Username;
		}

		public static void Logout(WebServer server, HttpListenerContext context)
		{
			server.DeleteSession(context);
		}
	}
}
