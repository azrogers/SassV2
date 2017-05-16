using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unosquare.Net;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

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
	}
}
