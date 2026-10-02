using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;

public class DailyPuzzleGenManager
{
	public PuzzlePackGeneratorLite Generator;

	public const string ALL_PACK_GEN_CONFIGS_FILENAME = "PackGenConfigs.json";
	public const string DAILY_CONFIGS_FILENAME = "DailyConfigs.txt";
	public const string DAILY_AVAILABILITY_KEYS_FILENAME = "DailyAvailabilityKeys.txt";

	public Dictionary<string, PackGenerationConfig> AllPackGenerationConfigs;
	public List<PackGenerationConfig> PuzzleGenParameters;
	// 0 means free, any non-zero value means the requester must specify that value for us to provide the details
	public List<int> AvailabilityKeys;

	public DailyPuzzles PuzzlesYesterday;
	public DailyPuzzles PuzzlesToday;
	public DailyPuzzles PuzzlesTomorrow;

	private DateTime Yesterday;
	private DateTime Today;
	private DateTime Tomorrow;

	private Timer CheckDateUpdateTimer;

	public DailyPuzzleGenManager()
	{
		Generator = new PuzzlePackGeneratorLite();

		Generator.FirstPuzzle = 1;
		Generator.LastPuzzle = 1;

		var success = LoadAllPackGenConfigs();
		if(!success)
		{
			Console.WriteLine("ERROR: Failed to load pack gen configs");
			return;
		}
		success = LoadDailyConfigs();
		if(!success)
		{
			Console.WriteLine("ERROR: Failed to load daily configs");
			return;
		}

		UpdateDate();
		GeneratePuzzles(dayPassMode: false);

		CheckDateUpdateTimer = new Timer(CheckDateUpdate);
		CheckDateUpdateTimer.Change(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
	}

	private void CheckDateUpdate(object timerState)
	{
		bool dateChanged = UpdateDate();
		if (dateChanged)
		{
			GeneratePuzzles(dayPassMode: true);
		}
	}

	private bool UpdateDate()
	{
		var today = DateTime.Now.Date;
		if (today > Today)
		{
			Yesterday = today.AddDays(-1).Date;
			Today = today;
			Tomorrow = today.AddDays(1).Date;
			return true;
		}
		return false;
	}

	private bool LoadAllPackGenConfigs()
	{
		var path = Path.Combine(Directory.GetCurrentDirectory(), ALL_PACK_GEN_CONFIGS_FILENAME);
		if (!File.Exists(path))
		{
			return false;
		}
		var json = File.ReadAllText(path);
		var configs = new List<PackGenerationConfig>();
		try
		{
			var configsList = JsonConvert.DeserializeObject<PackGenConfigList>(json);
			configs.AddRange(configsList.Configs);
		}
		catch(Exception e)
		{
			Console.WriteLine($"ERROR: Could not deserialize pack generation configs:\n{e}\n{e.Message}\n{e.StackTrace}");
			return false;
		}

		if(!configs.Any())
		{
			Console.WriteLine($"ERROR: No pack generation configs");
			return false;
		}

		AllPackGenerationConfigs = new Dictionary<string, PackGenerationConfig>();
		foreach (var config in configs)
		{
			AllPackGenerationConfigs.Add(config.PackId, config);
		}
		return true;
	}

	private bool LoadDailyConfigs()
	{
		var dailyConfigsPath = Path.Combine(Directory.GetCurrentDirectory(), DAILY_CONFIGS_FILENAME);
		if (!File.Exists(dailyConfigsPath))
		{
			return false;
		}
		var availabilityKeysPath = Path.Combine(Directory.GetCurrentDirectory(), DAILY_AVAILABILITY_KEYS_FILENAME);
		if (!File.Exists(availabilityKeysPath))
		{
			return false;
		}

		var configNames = File.ReadAllLines(dailyConfigsPath);
		var keys = File.ReadAllLines(availabilityKeysPath);

		if(configNames.Length != keys.Length)
		{
			Console.WriteLine($"ERROR: Unequal number of daily puzzle pack configs and availability keys! ({configNames.Length} and {keys.Length})");
			return false;
		}

		PuzzleGenParameters = new List<PackGenerationConfig>();
		foreach (var configName in configNames)
		{
			if(!AllPackGenerationConfigs.ContainsKey(configName))
			{
				Console.WriteLine($"ERROR: AllPackGenerationConfigs does not contain a pack by the name {configName}");
				return false;
			}
			PuzzleGenParameters.Add(AllPackGenerationConfigs[configName]);
		}

		AvailabilityKeys = new List<int>();
		foreach (var keyStr in keys)
		{
			if(!int.TryParse(keyStr, out int keyInt))
			{
				Console.WriteLine($"ERROR: Could not parse availability key {keyStr} to an int");
				return false;
			}
			AvailabilityKeys.Add(keyInt);
		}
		return true;
	}

	private void GeneratePuzzles(bool dayPassMode)
	{
		// Optionally save some processing by reusing generated puzzles from days past
		if (dayPassMode)
		{
			PuzzlesYesterday = PuzzlesToday;
			Console.WriteLine($"Today's Daily Puzzles moved to Yesterday ({Yesterday})");
			PuzzlesToday = PuzzlesTomorrow;
			Console.WriteLine($"Tomorrow's Daily Puzzles moved to Today ({Today})");
		}

		var seed = GetSeed(Tomorrow);
		PuzzlesTomorrow = GeneratePuzzles(seed);
		Console.WriteLine($"Daily Puzzles generated for Tomorrow ({Tomorrow})");
		if (!dayPassMode)
		{
			// Otherwise generate today's and yesterday's too
			seed = GetSeed(Today);
			PuzzlesToday = GeneratePuzzles(seed);
			Console.WriteLine($"Daily Puzzles generated for Today ({Today})");
			seed = GetSeed(Yesterday);
			PuzzlesYesterday = GeneratePuzzles(seed);
			Console.WriteLine($"Daily Puzzles generated for Yesterday ({Yesterday})");
		}
	}

	private static int GetSeed(DateTime dateTime)
	{
		var date = dateTime.Date;
		return date.Year * 10000 + date.Month * 100 + date.Day;
	}

	private DailyPuzzles GeneratePuzzles(int seed)
	{
		var seeds = new List<int>();
		for (int i = 0; i < PuzzleGenParameters.Count; i++)
		{
			seeds.Add(seed);
			seed = Random.Range(int.MinValue, int.MaxValue);
		}

		var puzzleSet = new DailyPuzzles();
		int id = 1;
		foreach (var parameters in PuzzleGenParameters)
		{
			Generator.MasterSeed = seeds[id - 1].ToString();
			Generator.Config = parameters;
			var puzzleConfig = Generator.GeneratePackPuzzles().First();
			puzzleConfig.Pack = "Daily";
			puzzleConfig.Id = id.ToString();
			var payload = new PuzzleConfigPayload(puzzleConfig);
			payload.DailyPuzzleGroup = parameters.PackId;
			puzzleSet.Puzzles.Add(payload);
			id++;
		}
		foreach (var puzzle in puzzleSet.Puzzles)
		{
			var redacted = new PuzzleConfigPayload();
			redacted.Pack = puzzle.Pack;
			redacted.Id = puzzle.Id;
			redacted.DailyPuzzleGroup = puzzle.DailyPuzzleGroup;
			puzzleSet.RedactedPuzzles.Add(redacted);
		}
		return puzzleSet;
	}

	public List<PuzzleConfigPayload> GetDailyPuzzles(DateTime dateTime, ISet<int> availabilityKeys, bool allowOnDemand = false)
	{
		var results = new List<PuzzleConfigPayload>();
		DailyPuzzles puzzleSet;
		if (dateTime.Date == Yesterday)
		{
			puzzleSet = PuzzlesYesterday;
		}
		else if (dateTime.Date == Today)
		{
			puzzleSet = PuzzlesToday;
		}
		else if (dateTime.Date == Tomorrow)
		{
			puzzleSet = PuzzlesTomorrow;
		}
		else if (allowOnDemand)
		{
			puzzleSet = GeneratePuzzles(GetSeed(dateTime));
		}
		else
		{
			return results;
		}

		for (int i = 0; i < puzzleSet.Puzzles.Count; i++)
		{
			if (AvailabilityKeys[i] == 0 || availabilityKeys.Contains(AvailabilityKeys[i]))
			{
				results.Add(puzzleSet.Puzzles[i]);
			}
			else
			{
				results.Add(puzzleSet.RedactedPuzzles[i]);
			}
		}

		return results;
	}
}

[Serializable]
public class DailyPuzzles
{
	public List<PuzzleConfigPayload> Puzzles = new List<PuzzleConfigPayload>();
	public List<PuzzleConfigPayload> RedactedPuzzles = new List<PuzzleConfigPayload>();
}

[Serializable]
public class PackGenConfigList
{
	public List<PackGenerationConfig> Configs;
}