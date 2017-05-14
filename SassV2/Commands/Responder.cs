using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace SassV2.Commands
{
	public class Responder
	{
		[Command(name: "add filter", desc: "add a responder filter", usage: "add filter <server id> <name> <lisp>", isPM: true)]
		public static string AddFilter(DiscordBot bot, Message msg, string args)
		{
			if(bot.Config.GetRole(msg.User.Id) != "admin")
			{
				throw new CommandException("You're not allowed to use this command.");
			}

			var parts = args.Split(' ');
			if(parts.Length < 3)
			{
				throw new CommandException("You need to provide a server id, a name, and a lisp expression.");
			}

			ulong serverId;
			if(!ulong.TryParse(parts[0], out serverId))
			{
				throw new CommandException("Invalid server ID.");
			}

			if(!bot.ServerIds.Contains(serverId))
			{
				throw new CommandException("SASS isn't connected to that server.");
			}

			var name = parts[1];

			var filterLisp = string.Join(" ", parts.Skip(1));

			bot.AddResponderFilter(name, filterLisp);

			var db = bot.Database(serverId);
			db.InsertObject<string>("filter:" + name, filterLisp);

			return "Filter added.";
		}

		[Command(name: "list filters", desc: "list filters", usage: "list filters <server id>", isPM: true)]
		public static string ListFilters(DiscordBot bot, Message msg, string args)
		{
			if(bot.Config.GetRole(msg.User.Id) != "admin")
			{
				throw new CommandException("You're not allowed to use this command.");
			}

			ulong serverId;
			if(!ulong.TryParse(args, out serverId))
			{
				throw new CommandException("Invalid server ID.");
			}

			if(!bot.ServerIds.Contains(serverId))
			{
				throw new CommandException("SASS isn't connected to that server.");
			}

			var db = bot.Database(serverId);
			var filters = db.GetKeysOfNamespace<string>("filter");

			return string.Join("\n", filters.Select(kv => kv.Key.Substring("filter:".Length) + " - " + kv.Value));
		}

		/*[Command(name: "add response", desc: "add a response to a filter", usage: "add filter <server id> <name> response", isPM: true)]
		public static string AddResponse(DiscordBot bot, Message msg, string args)
		{
			if(bot.Config.GetRole(msg.User.Id) != "admin")
			{
				throw new CommandException("You're not allowed to use this command.");
			}

			var parts = args.Split(' ');
			if(parts.Length < 3)
			{
				throw new CommandException("You need to provide a server id, a name, and a response.");
			}

			ulong serverId;
			if(!ulong.TryParse(parts[0], out serverId))
			{
				throw new CommandException("Invalid server ID.");
			}

			if(!bot.ServerIds.Contains(serverId))
			{
				throw new CommandException("SASS isn't connected to that server.");
			}

			var name = parts[1];

			var response = string.Join(" ", parts.Skip(1));

			bot.AddResponderFilter(name, filterLisp);

			var db = bot.Database(serverId);
			db.InsertObject<string>("filter:" + name, filterLisp);

			return "Filter added.";
		}*/
	}
}
