using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct Big
{
	#region Static
	public static readonly Big ZERO = new Big(0, 0);
	public static readonly Big ONE = new Big(1, 0);
	public static readonly Big TWO = new Big(2, 0);
	public static readonly Big MILLION = new Big(1, 6);

	static Big()
	{
		var firstLetter = 'a';
		var secondLetter = 'a';
		for(long exp = 27; exp < 350; exp+=3)
		{
			ExponentNames.Add(exp, $"{firstLetter}{secondLetter}");

			secondLetter++;
			if(secondLetter > 'z')
			{
				secondLetter = 'a';
				firstLetter++;
				if(firstLetter > 'z')
				{
					Debug.LogError($"Not enough letters for Big shorthand representation (exp={exp})");
				}
			}
		}
	}

	public static Big FromExponential(double coefficient, double baseValue, double power)
	{
		var exp = power * Math.Log10(baseValue);
		var totalExp = Math.Log10(coefficient) + exp;

		long e = (long)Math.Floor(totalExp);
		double c = Math.Pow(10, totalExp - e);
		return new Big(c, e).Normalized;
	}

    public static Big Pow(double value, double power) => Pow(new Big(value, 0).Normalized, power);

    public static Big Pow(Big value, double power)
    {
        if (value.Coefficient < 0 && (power % 1 != 0))
            throw new ArgumentOutOfRangeException(nameof(value), "Cannot raise a negative number to a fractional power.");

        if (value.Coefficient == 0)
            return ZERO;

        // x = c × 10^e
        // log10(x) = log10(c) + e
        double log10Value = Math.Log10(value.Coefficient) + value.Exponent;

        // Now raise to power:
        // log10(x^p) = log10(x) * p
        double totalLog10 = log10Value * power;

        // Convert back: 10^totalLog10
        long newExponent = (long)Math.Floor(totalLog10);
        double newCoefficient = Math.Pow(10, totalLog10 - newExponent);

        return new Big(newCoefficient, newExponent).Normalized;
    }

    public static Big Log(Big value, double baseValue)
	{
		if (value.Coefficient <= 0)
			throw new ArgumentOutOfRangeException(nameof(value), "Logarithm is undefined for zero or negative values.");

		// log_b(x) = log_10(x) / log_10(b)
		double log10Value = Math.Log10(value.Coefficient) + value.Exponent;
		double logBase = Math.Log10(baseValue);

		double rawCoefficient = log10Value / logBase;

		return ((Big)rawCoefficient).Normalized;
	}
	#endregion

	#region Fields
	public double Coefficient;
	public long Exponent;
	#endregion

	#region Constructors
	public Big(double coefficient, long exponent)
	{
		Coefficient = coefficient;
		Exponent = exponent;
	}
	#endregion

	#region Properties
	[Newtonsoft.Json.JsonIgnore]
	public Big Normalized { get { var copy = this; copy.Normalize(); return copy; } }
	[Newtonsoft.Json.JsonIgnore]
	public Big Floored { get { var copy = this; copy.Floor(); return copy; } }
	#endregion

	#region Operators
	public static explicit operator double(Big value) => value.Coefficient * Math.Pow(10d, value.Exponent);
	public static explicit operator Big(double value) => new Big(value, 0);

	public static bool operator ==(Big a, Big b) => Equals(a: a, b: b);
	public static bool operator !=(Big a, Big b) => !Equals(a: a, b: b);

	public static bool operator >(Big a, Big b)
	{
		a.Normalize();
		b.Normalize();
		ToCommonExponent(ref a, ref b);
		return a.Coefficient > b.Coefficient;
	}

	public static bool operator >=(Big a, Big b)
	{
		a.Normalize();
		b.Normalize();
		ToCommonExponent(ref a, ref b);
		return a.Coefficient >= b.Coefficient;
	}

	public static bool operator <(Big a, Big b)
	{
		a.Normalize();
		b.Normalize();
		ToCommonExponent(ref a, ref b);
		return a.Coefficient < b.Coefficient;
	}

	public static bool operator <=(Big a, Big b)
	{
		a.Normalize();
		b.Normalize();
		ToCommonExponent(ref a, ref b);
		return a.Coefficient <= b.Coefficient;
	}

	public static Big operator +(Big a, Big b)
	{
		ToCommonExponent(ref a, ref b);
		Big result = new Big { Coefficient = a.Coefficient + b.Coefficient, Exponent = a.Exponent }.Normalized;
		return result;
	}
	public static Big operator +(Big a, double b) => (a + new Big(b, 0).Normalized).Normalized;
	public static Big operator -(Big a, Big b)
	{
		ToCommonExponent(ref a, ref b);
		return new Big { Coefficient = a.Coefficient - b.Coefficient, Exponent = a.Exponent }.Normalized;
	}
	public static Big operator -(Big a, double b) => (a - new Big(b, 0).Normalized).Normalized;
	public static Big operator *(Big a, Big b) => new Big { Coefficient = a.Coefficient * b.Coefficient, Exponent = a.Exponent + b.Exponent }.Normalized;
	public static Big operator *(Big a, double b) => (a * new Big(b, 0).Normalized).Normalized;
	public static Big operator /(Big a, Big b) => new Big(a.Coefficient / b.Coefficient, a.Exponent - b.Exponent).Normalized;
	public static Big operator /(Big a, double b) => (a / new Big(b, 0).Normalized).Normalized;
	#endregion

	#region Overrides
	public override string ToString() => $"{Coefficient} {GetExponentName()}";

	public override bool Equals(object obj) => obj != null ? Equals(a: this, b: (Big)obj) : false;

	/// <summary> I think this exponent.GetHashCode() will fully ignore numerical proximity, which is not fine is some cases, but fine for others. </summary>
	/// <remarks> Modify this method to fit your specific use case. </remarks>
	public override int GetHashCode()
	{
		var copy = this.Normalized;
		unchecked
		{
			int hash = 486187739;
			hash = hash * 16777619 + copy.Coefficient.GetHashCode();
			hash = hash * 16777619 + copy.Exponent.GetHashCode();
			return hash;
		}
	}
	#endregion

	#region Public
	public string ToStringPrecise() => $"{Coefficient.ToString("G17")}e{Exponent.ToString("G17")}";

    public string ToStringSimpleScientific(int digits = 2)
    {
        double value = Coefficient * Math.Pow(10, Exponent);
        int scientificExponent = 0;
        double scientificCoeff = value;

        if (value != 0)
        {
            scientificExponent = (int)Math.Floor(Math.Log10(Math.Abs(value)));
            scientificCoeff = value / Math.Pow(10, scientificExponent);
        }

        if (Mathf.Abs(scientificExponent) < 3)
        {
            return value.ToString($"F{digits}");
        }

        return $"{scientificCoeff.ToString($"F{digits}")}e{scientificExponent}";
    }

    public string ToStringSimpleScientificInt(int digits = 2)
    {
        double value = Coefficient * Math.Pow(10, Exponent);
        int scientificExponent = 0;
        double scientificCoeff = value;

        if (value != 0)
        {
            scientificExponent = (int)Math.Floor(Math.Log10(Math.Abs(value)));
            scientificCoeff = value / Math.Pow(10, scientificExponent);
        }

        if (Mathf.Abs(scientificExponent) < 3)
        {
            return ((int)value).ToString();
        }

        return $"{scientificCoeff.ToString($"F{digits}")}e{scientificExponent}";
    }

    public const string SCIENTIFIC_NOTATION_KEY = "ScientificNotation";
	public string ToStringShorthand(string coefficientFormat = "F2")
	{
        if(SettingsManager.Instance.GetBool(SCIENTIFIC_NOTATION_KEY))
        {
            return ToStringSimpleScientific();
        }

        ExponentNames.TryGetValue(Exponent, out string exp);
		exp ??= $"e{Exponent}";
		if(Mathf.Abs(Exponent) < 3)
		{
			exp = "";
		}
		return $"{Coefficient.ToString(coefficientFormat)}{exp}";
	}

	public string ToStringShortInt()
    {
        if (SettingsManager.Instance.GetBool(SCIENTIFIC_NOTATION_KEY))
        {
            return ToStringSimpleScientificInt();
        }

        ExponentNames.TryGetValue(Exponent, out string exp);
		exp ??= $"e{Exponent}";
		if (Mathf.Abs(Exponent) < 3)
		{
			exp = "";
			return $"{Coefficient:F0}{exp}";
		}
		return $"{Coefficient:F2}{exp}";
	}

	public static bool Equals(Big a, Big b)
	{
		a.Normalize();
		b.Normalize();
		bool result = a.Exponent == b.Exponent && Approx(a.Coefficient, b.Coefficient);
		return result;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool Approx(double a, double b)
	{
		const double precision = 1E-14;
		return Math.Abs(a - b) <= precision;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void ToCommonExponent(ref Big a, ref Big b)
	{
		if (a.Exponent == b.Exponent) return;
		else if (a.Exponent > b.Exponent)
		{
			b.Coefficient /= Math.Pow(10d, a.Exponent - b.Exponent);
			b.Exponent = a.Exponent;
		}
		else
		{
			a.Coefficient /= Math.Pow(10d, b.Exponent - a.Exponent);
			a.Exponent = b.Exponent;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	/// <summary> Attempts to pack stored value to prevent coefficient component from losing it's accuracy. </summary>
	/// <remarks> Prefers **engineering notation**. </remarks>
	public void Normalize()
	{
		long e2 = 0;
		double frac = 0;
		int sign = Math.Sign(Coefficient);
		if (sign != 0)
		{
			double log10 = Math.Log10(Math.Abs(Coefficient)) * (double)sign;
			long log10floor = (long)Math.Floor(log10);
			frac = Coefficient / Math.Pow(10d, log10floor);
			e2 = log10floor + Exponent;
		}
		long e2mod3 = e2 % 3;

		Exponent = e2 - e2mod3;
		Coefficient = frac * Math.Pow(10d, e2mod3);

        // Prevents cases where rounding would end up displaying 1000suffix
        if(Coefficient >= 999.995)
        {
            Coefficient = 1;
            Exponent += 3;
        }
	}

	public void Floor()
	{
		if (Coefficient == 0)
			return;

		// If the exponent is big enough, fractional coefficient doesn't matter
		if (Exponent >= 16)
		{
			return; // Already "floored"
		}

		// Account for slight floating point error which might cause unexpected values
		var epsilon = 1e-9;
		var roundedCoefficient = Math.Round(Coefficient);
		var effectiveCoefficient = Coefficient;
		if (Math.Abs(roundedCoefficient - Coefficient) < epsilon)
		{
			effectiveCoefficient = roundedCoefficient;
		}

		// Compute the full value as a double, floor it, then convert back to Big
		var pow = Math.Pow(10, Exponent);
		double value = effectiveCoefficient * pow;
		double floored = Math.Floor(value);

		// Recompute coefficient and exponent from the floored value
		int sign = Math.Sign(floored);
		if (sign == 0)
		{
			Coefficient = 0;
			Exponent = 0;
			return;
		}

		double newCoefficient = floored / pow;

		Coefficient = newCoefficient;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool IsNormalized() =>
		Coefficient == 0 ?
			Exponent == 0 :
			Math.Abs(Coefficient) < 1000;

	public string GetExponentName()
	{
		ExponentNames.TryGetValue(Exponent, out string result);
		return result;
	}
	public bool GetExponentName(out string result)
	{
		if (!ExponentNames.TryGetValue(Exponent, out result))
		{
			result = $"E{Exponent}";
			return false;
		}
		return true;
	}
	#endregion

	#region LUT
	// Contains the "standard" single abbreviations through Septillion
	// Everything larger is filled in the static initializer
	private static readonly Dictionary<long, string> ExponentNames = new Dictionary<long, string>{
		{   3       ,   "K"		} ,
		{   6       ,   "M"		} ,
		{   9       ,   "B"		} ,
		{   12      ,   "T"		} ,
		{   15      ,   "q"		} ,
		{   18      ,   "Q"		} ,
		{   21      ,   "s"		} ,
		{   24      ,   "S"		} ,
	};
	#endregion
}