using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Discord;
using Discord.WebSocket;
using Functional.Maybe;

namespace SassV2
{
	public class CommandHandler
	{
		public delegate string CommandDelegate(DiscordBot bot, IMessage msg, string args);
		public delegate Task<string> AsyncCommandDelegate(DiscordBot bot, IMessage msg, string args);

		private List<string> _commands;
		private List<string> _pmCommands;
		private Dictionary<string, MethodInfo> _commandMap;
		private Dictionary<string, MethodInfo> _pmCommandMap;
		private List<CommandAttribute> _commandAttributes;

		public List<CommandAttribute> CommandAttributes => _commandAttributes;

		public CommandHandler()
		{
			_commands = new List<string>();
			_pmCommands = new List<string>();
			_commandMap = new Dictionary<string, MethodInfo>();
			_pmCommandMap = new Dictionary<string, MethodInfo>();
			_commandAttributes = new List<CommandAttribute>();

			var commandNamespace = typeof(CommandHandler).Namespace + ".Commands";
			// find all command methods
			var methodInfo =
				Assembly.GetExecutingAssembly().GetTypes()
				.Where(t => t.IsClass && t.Namespace == commandNamespace)
				.Select(t => t.GetTypeInfo().DeclaredMethods)
				.SelectMany(m => m)
				.ToArray();

			// add methods to map and list
			foreach(var method in methodInfo)
			{
				var commandAttribute = method.GetCustomAttribute<CommandAttribute>();
				if(commandAttribute == null)
				{
					continue;
				}

				_commandAttributes.Add(commandAttribute);

				foreach(var commandName in commandAttribute.Names)
				{
					if(commandAttribute.IsPM)
					{
						_pmCommands.Add(commandName);
						_pmCommandMap[commandName] = method;
					}
					else
					{
						_commands.Add(commandName);
						_commandMap[commandName] = method;
					}
				}
			}

			// make sure the longest commands are at the top of the command list
			_commands = _commands.OrderByDescending(n => n.Length).ToList();
		}

		/// <summary>
		/// Find a command for the given message.
		/// </summary>
		/// <param name="message">The message to find a command for.</param>
		/// <returns>A Maybe representing the command or nothing.</returns>
		public Maybe<Command> FindCommand(string message, bool isPrivate)
		{
			var commandFound = false;
			var commandName = "";
			foreach(var name in (isPrivate ? _pmCommands : _commands))
			{
				if(message.ToLower().Trim() == name.ToLower() || message.StartsWith(name + " ", StringComparison.CurrentCultureIgnoreCase))
				{
					commandFound = true;
					commandName = name;
					break;
				}
			}

			if(!commandFound)
			{
				return Maybe<Command>.Nothing;
			}

			var args = message.Substring(commandName.Length).Trim();

			var command = new Command();
			var commandMethod = (isPrivate ? _pmCommandMap[commandName] : _commandMap[commandName]);
			if(commandMethod.ReturnType == typeof(string))
			{
				command.Delegate = (CommandDelegate)Delegate.CreateDelegate(typeof(CommandDelegate), commandMethod);
			}
			else
			{
				command.IsAsync = true;
				command.AsyncDelegate = (AsyncCommandDelegate)Delegate.CreateDelegate(typeof(AsyncCommandDelegate), commandMethod);
			}
			command.Attribute = _commandAttributes.Where(c => c.Names.Where(n => n.ToLower() == commandName).Any()).First();
			command.Arguments = args;

			return command.ToMaybe();
		}

		public class Command
		{
			public CommandDelegate Delegate;
			public AsyncCommandDelegate AsyncDelegate;
			public string Arguments;
			public CommandAttribute Attribute;
			public bool IsAsync = false;
		}
	}

	[AttributeUsage(AttributeTargets.Method)]
	public class CommandAttribute : Attribute
	{
		public string[] Names;
		public string Description;
		public string Usage;
		public string Category;
		public bool Hidden;
		public bool IsPM = false;

		public CommandAttribute(string[] names, string desc = "", string usage = "", string category = "General", bool hidden = false, bool isPM = false)
		{
			Names = names;
			Description = desc;
			Usage = usage;
			Category = category;
			Hidden = hidden;
			IsPM = isPM;
		}
		public CommandAttribute(string name, string desc = "", string usage = "", string category = "General", bool hidden = false, bool isPM = false)
		{
			Names = new string[] { name };
			Description = desc;
			Usage = usage;
			Category = category;
			Hidden = hidden;
			IsPM = isPM;
		}
	}

	public class CommandException : Exception
	{
		public CommandException(string message)
			: base(message)
		{

		}
	}
}
