using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Mono.Data.Sqlite;

namespace SassV2.Commands
{
	public static class PokemonCommand
	{
		private static PokemonDatabase _database = new PokemonDatabase();

		[Command(name: "pokemon", desc: "gotta catch 'em all.", usage: "pokemon <name> or pokemon #<number>", category: "Useful")]
		public static string Pokemon(DiscordBot bot, IMessage msg, string args)
		{
			var builder = new StringBuilder();
			
			if(args.StartsWith("#", StringComparison.CurrentCulture))
			{
				int pokemonId;
				if(!int.TryParse(args.Substring(1), out pokemonId))
				{
					throw new CommandException("Invalid Pokemon ID.");
				}

				return _database.FindPokemon(pokemonId);
			}
			else if(string.IsNullOrEmpty(args))
			{
				return _database.RandomPokemon();
			}
			else
			{
				return _database.FindPokemon(args);
			}
		}

	}

	public class PokemonDatabase
	{
		private SqliteConnection _connection;
		private Dictionary<int, string> _typeNames = new Dictionary<int, string>();
		private Dictionary<int, string> _pokemonNames = new Dictionary<int, string>();

		public PokemonDatabase()
		{
			_connection = new SqliteConnection("Data Source=pokedex.sqlite;Version=3;");
			_connection.Open();
		}

		public string RandomPokemon()
		{
			var cmd = new SqliteCommand("SELECT species_id FROM pokemon GROUP BY species_id ORDER BY RANDOM() LIMIT 1;", _connection);
			var reader = cmd.ExecuteReader();
			if(!reader.HasRows)
				return "No pokemon???";
			reader.Read();
			var pokemonId = reader.GetInt32(0);
			return FindPokemon((int)pokemonId);
		}

		public string FindPokemon(string name)
		{
			var cmd = new SqliteCommand("SELECT species_id FROM pokemon WHERE identifier LIKE ? GROUP BY species_id;", _connection);
			cmd.Parameters.AddWithValue(null, "%" + name + "%");
			var reader = cmd.ExecuteReader();
			if(!reader.HasRows)
				return "Pokemon not found.";
			reader.Read();
			var pokemonId = reader.GetInt32(0);
			return FindPokemon((int)pokemonId);
		}

		public string FindPokemon(int pokemonId)
		{
			var builder = new StringBuilder();
			builder.AppendLine("**Pokemon #" + pokemonId + " - " + GetPokemonName(pokemonId) + "** *(" + string.Join(",", GetPokemonTypes(pokemonId)) + ")*");
			builder.AppendLine(GetPokemonFlavorText(pokemonId));
			builder.AppendLine("https://www.anime-night.com/images/pokemon/" + pokemonId + ".png");
			builder.AppendLine("https://www.anime-night.com/images/pokemon/shiny/" + pokemonId + ".png");
			return builder.ToString();
		}

		private string GetPokemonFlavorText(int id)
		{
			var cmd = new SqliteCommand("SELECT flavor_text FROM pokemon_species_flavor_text WHERE species_id=? AND language_id=9 ORDER BY version_id DESC LIMIT 1;", _connection);
			cmd.Parameters.AddWithValue(null, id);
			var reader = cmd.ExecuteReader();
			if(!reader.HasRows)
				return "[No Flavor Text]";
			reader.Read();
			return reader.GetString(0);
		}

		private string GetPokemonName(int id)
		{
			if(_pokemonNames.ContainsKey(id))
			{
				return _pokemonNames[id];
			}
			var cmd = new SqliteCommand("SELECT name FROM pokemon_species_names WHERE pokemon_species_id = ? AND local_language_id=9;", _connection);
			cmd.Parameters.AddWithValue(null, id);
			var reader = cmd.ExecuteReader();
			if(!reader.HasRows)
				return "[Unknown Name]";
			reader.Read();
			return reader.GetString(0);
		}

		private List<string> GetPokemonTypes(int id)
		{
			var types = new List<string>();
			var cmd = new SqliteCommand("SELECT type_id FROM pokemon_types WHERE pokemon_id=?;", _connection);
			cmd.Parameters.AddWithValue(null, id);
			var reader = cmd.ExecuteReader();
			if(!reader.HasRows)
				return types;
			while(reader.Read())
			{
				types.Add(GetTypeName(reader.GetInt32(0)));
			}
			return types;
		}

		private string GetTypeName(int id)
		{
			if(_typeNames.ContainsKey(id))
			{
				return _typeNames[id];
			}

			var cmd = new SqliteCommand("SELECT name FROM type_names WHERE type_id = ? AND local_language_id = 9;", _connection);
			cmd.Parameters.AddWithValue(null, id);
			var reader = cmd.ExecuteReader();
			if(!reader.HasRows)
				return "[Unknown Type]";
			reader.Read();
			return (_typeNames[id] = reader.GetString(0));
		}
	}
}
