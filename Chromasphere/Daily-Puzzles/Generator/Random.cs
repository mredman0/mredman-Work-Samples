using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class Random
{
	private static System.Random _rand = new System.Random();
	public static System.Random Rand => _rand;

	public static void InitState(int seed)
	{
		_rand = new System.Random(seed);
	}

	public static int Range(int minInclusive, int maxExclusive)
	{
		var diff = (long)maxExclusive - (long)minInclusive;
		if (diff == 0)
		{
			_rand.Next(); // Discard a value for consistent state change
			return minInclusive;
		}
		var raw = _rand.Next();
		return (int)(raw % diff) + minInclusive;
	}

	public static float Range(float minInclusive, float maxInclusive)
	{
		var diff = maxInclusive - minInclusive;
		var raw = value;

		return (raw * diff) + minInclusive;
	}

	public static float value
	{
		get
		{
			var raw = _rand.NextSingle();
			if (_rand.Next(0, int.MaxValue) == 0)
			{
				return 1.0f;
			}
			return raw;
		}
	}

	public static Vector3 onUnitSphere
	{
		get
		{
			throw new NotImplementedException();
		}
	}
}
