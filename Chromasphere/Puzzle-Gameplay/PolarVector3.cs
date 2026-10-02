using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct PolarVector3
{
	public static PolarVector3 Zero => new PolarVector3(0, 0, 0);

	public float Radius;
	public float Latitude;
	public float Longitude;

	public PolarVector3(float radius, float latitude, float longitude)
	{
		Radius = radius;
		Latitude = latitude;
		Longitude = longitude;
		if(Longitude < 0)
		{
			Longitude += 360f;
		}
	}

	public Vector3 ToCartesian() => ToCartesian(Latitude, Longitude, Radius);

	public static Vector3 ToCartesian(float latitude, float longitude, float radius = 1f)
	{
		float latRad = latitude * Mathf.Deg2Rad;
		float lonRad = longitude * Mathf.Deg2Rad;
		float x = radius * Mathf.Cos(latRad) * Mathf.Cos(lonRad);
		float y = radius * Mathf.Sin(latRad);
		float z = radius * Mathf.Cos(latRad) * Mathf.Sin(lonRad);
		return new Vector3(x, y, z);
	}

	public static bool operator ==(PolarVector3 a, PolarVector3 b) =>
		a.Radius == b.Radius &&
		a.Latitude == b.Latitude &&
		a.Longitude == b.Longitude;

	public static bool operator !=(PolarVector3 a, PolarVector3 b) =>
		a.Radius != b.Radius ||
		a.Latitude != b.Latitude ||
		a.Longitude != b.Longitude;

	public override bool Equals(object obj)
	{
		if(obj is PolarVector3 other)
		{
			return other == this;
		}
		return false;
	}

	public override int GetHashCode() => Radius.GetHashCode() + Latitude.GetHashCode() + Longitude.GetHashCode();

	public override string ToString() =>
		$"Polar r:{Radius}, lat:{Latitude}, long:{Longitude}";
}

public static class Vector3Extensions
{
	public static PolarVector3 ToPolar(this Vector3 vec)
	{
		if(vec == Vector3.zero)
		{
			return PolarVector3.Zero;
		}

		var radius = vec.magnitude;
		float latitude = Mathf.Asin(vec.y / radius) * Mathf.Rad2Deg;
		float longitude = Mathf.Atan2(vec.z, vec.x) * Mathf.Rad2Deg;
		return new PolarVector3(radius, latitude, longitude);
	}
}
