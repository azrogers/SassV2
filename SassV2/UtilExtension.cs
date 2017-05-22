using Discord;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unosquare.Net;

namespace SassV2
{
	public static class UtilExtension
	{
		/// <summary>
		/// Shuffles the enumerable.
		/// </summary>
		/// <param name="list">The enumerable to shuffle.</param>
		public static IEnumerable<T> Randomize<T>(this IEnumerable<T> list)
		{
			Random rnd = new Random();
			return list.OrderBy(c => rnd.Next());
		}

		/// <summary>
		/// Returns the user's nickname, or their name if they have no nickname set.
		/// </summary>
		/// <returns>The user's nickname.</returns>
		public static string NicknameOrDefault(this IGuildUser user)
		{
			return user.Nickname == null ? user.Username : user.Nickname;
		}

		/// <summary>
		/// Outputs an HTML response given an HTML string.
		/// </summary>
		public static bool HtmlResponse(this HttpListenerContext context, string html)
		{
			var buffer = Encoding.UTF8.GetBytes(html);
			context.Response.ContentType = "text/html";
			context.Response.OutputStream.Write(buffer, 0, buffer.Length);
			return true;
		}

		public static ulong ServerId(this IMessage msg)
		{
			return (msg.Channel as IGuildChannel).GuildId;
		}

		public static GuildPermissions AuthorPermissions(this IMessage msg)
		{
			return (msg.Author as IGuildUser).GuildPermissions;
		}

		public static long ToUnixTime(this DateTime date)
		{
			var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			return Convert.ToInt64((date - epoch).TotalSeconds);
		}

		public static DateTime FromUnixTime(long unixTime)
		{
			var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			return epoch.AddSeconds(unixTime);
		}

		/// <summary>
		/// Extension method that turns a dictionary of string and object to an ExpandoObject
		/// </summary>
		public static ExpandoObject ToExpando(this IDictionary<string, object> dictionary)
		{
			var expando = new ExpandoObject();
			var eoColl = (ICollection<KeyValuePair<string, object>>)expando;

			foreach(var kv in dictionary)
			{
				eoColl.Add(kv);
			}

			return expando;
		}

		public static bool ContainsKey(this ExpandoObject eo, string key)
		{
			return ((IDictionary<string, object>)eo).ContainsKey(key);
		}

		public static bool IsAdmin(this IGuildUser user, DiscordBot bot)
		{
			var permissions = user.GuildPermissions;
			return permissions.Administrator || bot.Config.GetRole(user.Id) == "admin";
		}

		public static void Forget(this Task task)
		{
			// this method left deliberately empty.
		}
	}
}
