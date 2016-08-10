using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Discord;

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
		public static string NicknameOrDefault(this User user)
		{
			return user.Nickname == null ? user.Name : user.Nickname;
		}

		/// <summary>
		/// Outputs an HTML response given an HTML string.
		/// </summary>
		public static bool HtmlReponse(this HttpListenerContext context, string html)
		{
			var buffer = Encoding.UTF8.GetBytes(html);
			context.Response.ContentType = "text/html";
			context.Response.OutputStream.Write(buffer, 0, buffer.Length);
			return true;
		}
	}
}
