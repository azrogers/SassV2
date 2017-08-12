using Discord;
using Discord.WebSocket;
using System;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
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

			foreach (var kv in dictionary)
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

		public static IServiceCollection AddPaginator(this IServiceCollection collection, DiscordSocketClient client)
		{
			collection.AddSingleton(new PaginationService(client));
			return collection;
		}

		/// <summary>
		/// Returns a _private_ Property Value from a given Object. Uses Reflection.
		/// Throws a ArgumentOutOfRangeException if the Property is not found.
		/// </summary>
		/// <typeparam name="T">Type of the Property</typeparam>
		/// <param name="obj">Object from where the Property Value is returned</param>
		/// <param name="propName">Propertyname as string.</param>
		/// <returns>PropertyValue</returns>
		public static T GetPrivatePropertyValue<T>(this object obj, string propName)
		{
			if (obj == null) throw new ArgumentNullException("obj");
			PropertyInfo pi = obj.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if (pi == null) throw new ArgumentOutOfRangeException("propName", string.Format("Property {0} was not found in Type {1}", propName, obj.GetType().FullName));
			return (T)pi.GetValue(obj, null);
		}

		/// <summary>
		/// Returns a private Property Value from a given Object. Uses Reflection.
		/// Throws a ArgumentOutOfRangeException if the Property is not found.
		/// </summary>
		/// <typeparam name="T">Type of the Property</typeparam>
		/// <param name="obj">Object from where the Property Value is returned</param>
		/// <param name="propName">Propertyname as string.</param>
		/// <returns>PropertyValue</returns>
		public static T GetPrivateFieldValue<T>(this object obj, string propName)
		{
			if (obj == null) throw new ArgumentNullException("obj");
			Type t = obj.GetType();
			FieldInfo fi = null;
			while (fi == null && t != null)
			{
				fi = t.GetField(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
				t = t.GetTypeInfo().BaseType;
			}
			if (fi == null) throw new ArgumentOutOfRangeException("propName", string.Format("Field {0} was not found in Type {1}", propName, obj.GetType().FullName));
			return (T)fi.GetValue(obj);
		}

		/// <summary>
		/// Sets a _private_ Property Value from a given Object. Uses Reflection.
		/// Throws a ArgumentOutOfRangeException if the Property is not found.
		/// </summary>
		/// <typeparam name="T">Type of the Property</typeparam>
		/// <param name="obj">Object from where the Property Value is set</param>
		/// <param name="propName">Propertyname as string.</param>
		/// <param name="val">Value to set.</param>
		/// <returns>PropertyValue</returns>
		public static void SetPrivatePropertyValue<T>(this object obj, string propName, T val)
		{
			Type t = obj.GetType();
			if (t.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) == null)
				throw new ArgumentOutOfRangeException("propName", string.Format("Property {0} was not found in Type {1}", propName, obj.GetType().FullName));
			var member = t.GetMethod(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.SetProperty | BindingFlags.Instance);
			member.Invoke(obj, new object[] { val });
		}

		/// <summary>
		/// Set a private Property Value on a given Object. Uses Reflection.
		/// </summary>
		/// <typeparam name="T">Type of the Property</typeparam>
		/// <param name="obj">Object from where the Property Value is returned</param>
		/// <param name="propName">Propertyname as string.</param>
		/// <param name="val">the value to set</param>
		/// <exception cref="ArgumentOutOfRangeException">if the Property is not found</exception>
		public static void SetPrivateFieldValue<T>(this object obj, string propName, T val)
		{
			if (obj == null) throw new ArgumentNullException("obj");
			Type t = obj.GetType();
			FieldInfo fi = null;
			while (fi == null && t != null)
			{
				fi = t.GetField(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
				t = t.GetTypeInfo().BaseType;
			}
			if (fi == null) throw new ArgumentOutOfRangeException("propName", string.Format("Field {0} was not found in Type {1}", propName, obj.GetType().FullName));
			fi.SetValue(obj, val);
		}
	}
}
