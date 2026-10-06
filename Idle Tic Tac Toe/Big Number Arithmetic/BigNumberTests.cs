using System;
using UnityEngine;

[CreateAssetMenu(fileName = "BigNumberTests", menuName ="ScriptableObjects/UnitTestModules/BigNumberTests")]
public class BigNumberTests : TestModule
{
	Big a = new Big(1, 0);
	Big b = new Big(1, 0);
	Big c = new Big(2, 0);
	Big d = new Big(1, 1);
	Big e = new Big(1, 3);
	Big f = new Big(1000, 0);
	Big g = new Big(100, 4);
	Big h = new Big(25000, 0);
	Big i = new Big(2500, 1);

	public override bool OnRun()
	{
		var success = true;

		success &= EqualsTests();
		success &= CoefficientTests();
		success &= ExponentTests();

		success &= NormalizeTests();
		success &= FloorTests();
		success &= ToCommonExponentTests();

		success &= FromExponentialTests();

        success &= PowTests();
        success &= LogTests();

		success &= ComparisonTests();

		return success;
	}

	private bool EqualsTests()
	{
		var success = true;

		success &= AssertEqual(a, b);
		success &= AssertNotEqual(a, c);
		success &= AssertEqual(a + b, c);
		success &= AssertNotEqual(a, d);

		success &= AssertEqual(a.Coefficient, b.Coefficient);
		success &= AssertNotEqual(a.Coefficient, c.Coefficient);
		success &= AssertEqual(a.Exponent, b.Exponent);
		success &= AssertEqual(a.Exponent, c.Exponent);

		return success;
	}

	private bool CoefficientTests()
	{
		var success = true;

		success &= AssertEqual(a.Coefficient, b.Coefficient);
		success &= AssertNotEqual(a.Coefficient, c.Coefficient);
		success &= AssertEqual(a.Coefficient, d.Coefficient);

		return success;
	}

	private bool ExponentTests()
	{
		var success = true;

		success &= AssertEqual(a.Exponent, b.Exponent);
		success &= AssertEqual(a.Exponent, c.Exponent);
		success &= AssertNotEqual(a.Exponent, d.Exponent);

		return success;
	}

	private bool NormalizeTests()
	{
		var success = true;

		var aNorm = a.Normalized;
		success &= AssertEqual(aNorm.Coefficient, 1d);
		success &= AssertEqual(aNorm.Exponent, 0L);

		var cNorm = c.Normalized;
		success &= AssertEqual(cNorm.Coefficient, 2d);
		success &= AssertEqual(cNorm.Exponent, 0L);

		var dNorm = d.Normalized;
		success &= AssertEqual(dNorm.Coefficient, 10d);
		success &= AssertEqual(dNorm.Exponent, 0L);

		var eNorm = e.Normalized;
		success &= AssertEqual(eNorm.Coefficient, 1d);
		success &= AssertEqual(eNorm.Exponent, 3L);

		var fNorm = f.Normalized;
		success &= AssertEqual(fNorm.Coefficient, 1d);
		success &= AssertEqual(fNorm.Exponent, 3L);

		var gNorm = g.Normalized;
		success &= AssertEqual(gNorm.Coefficient, 1d);
		success &= AssertEqual(gNorm.Exponent, 6L);

		var hNorm = h.Normalized;
		success &= AssertEqual(hNorm.Coefficient, 25d);
		success &= AssertEqual(hNorm.Exponent, 3L);

		var iNorm = i.Normalized;
		success &= AssertEqual(iNorm.Coefficient, 25d);
		success &= AssertEqual(iNorm.Exponent, 3L);

		return success;
	}

	private bool FloorTests()
	{
		var success = true;

		// Add test cases
		var a = new Big(1.5, 0);    // Should floor to 1
		var b = new Big(123.999, 0); // Should floor to 123
		var c = new Big(1.2345, 3);  // 1234.5 -> floor to 1234
		var d = new Big(999.9999, 3); // 999999.9 -> floor to 999999
		var e = new Big(1.999, 16);  // Should remain unchanged (double can't represent fraction at this size)
		var f = new Big(0.0001, 0);  // Should floor to 0
		var g = new Big(-1.512, 2);    // -151.2 -> floor to -152
		var h = new Big(1.23, -2);   // Very small number -> floor to 0

		// Expected values
		success &= AssertEqual(a.Floored, new Big(1, 0));
		success &= AssertEqual(b.Floored, new Big(123, 0));
		success &= AssertEqual(c.Floored, new Big(1.234, 3));   // 1234, normalized
		success &= AssertEqual(d.Floored, new Big(999.999, 3));     // 999999
		success &= AssertEqual(e.Floored, e);                   // Should be unchanged due to high exponent
		success &= AssertEqual(f.Floored, new Big(0, 0));
		success &= AssertEqual(g.Floored, new Big(-1.52, 2));      // -152
		success &= AssertEqual(h.Floored, new Big(0, 0));

		return success;
	}

	private bool ToCommonExponentTests()
	{
		var success = true;

		var a = new Big(1, 0); // 1
		var b = new Big(1, 3); // 1000

		Big.ToCommonExponent(ref a, ref b);
		success &= AssertEqual(a.Exponent, 3L);
		success &= AssertEqual(b.Exponent, 3L);
		success &= AssertEqual(a.Coefficient, 0.001d);
		success &= AssertEqual(b.Coefficient, 1d);

		return success;
	}

	private bool FromExponentialTests()
	{
		var success = true;

		var a = Big.FromExponential(1, 1.5, 0);
		var b = Big.FromExponential(1, 1.5, 1);
		var c = Big.FromExponential(2, 1.5, 1);
		var d = Big.FromExponential(10, 1, 1);

		success &= AssertEqual(a.Coefficient, 1d);
		success &= AssertEqual(a.Exponent, 0L);

		success &= AssertEqual(b.Coefficient, 1.5d);
		success &= AssertEqual(b.Exponent, 0L);

		success &= AssertEqual(c.Coefficient, 3d);
		success &= AssertEqual(c.Exponent, 0L);

		success &= AssertEqual(d.Coefficient, 10d);
		success &= AssertEqual(d.Exponent, 0L);

		return success;
	}

    private bool PowTests()
    {
        var success = true;

        var powA1 = Big.Pow(a, 2);
        success &= AssertEqual(powA1.Coefficient, 1d);
        success &= AssertEqual(powA1.Exponent, 0L);

        var powA2 = Big.Pow(a, 1.25);
        success &= AssertEqual(powA2.Coefficient, 1d);
        success &= AssertEqual(powA2.Exponent, 0L);

        var powD1 = Big.Pow(d, 2);
        success &= AssertEqual(powD1.Coefficient, 100d);
        success &= AssertEqual(powD1.Exponent, 0L);

        var powD2 = Big.Pow(d, 1.25);
        success &= AssertTrue(Math.Abs(powD2.Coefficient - 17.7827941) < 0.00001);
        success &= AssertEqual(powD2.Exponent, 0L);

        var e = new Big(2, 3);
        var powE1 = Big.Pow(e, 2);
        success &= AssertTrue(Math.Abs(powE1.Coefficient - 4d) < 0.00001);
        success &= AssertEqual(powE1.Exponent, 6L);

        var powE2 = Big.Pow(e, 1.1);
        success &= AssertTrue(Math.Abs(powE2.Coefficient - 4.27693839) < 0.00001);
        success &= AssertEqual(powE2.Exponent, 3L);

        return success;
    }

	private bool LogTests()
	{
		var success = true;

		// log_1.5(1) = 0
		var a = new Big(1, 0);
		var logA = Big.Log(a, 1.5);
		success &= AssertEqual(logA.Coefficient, 0d);
		success &= AssertEqual(logA.Exponent, 0L);

		// log_1.5(1.5) = 1
		var b = new Big(1.5, 0);
		var logB = Big.Log(b, 1.5);
		success &= AssertEqual(logB.Coefficient, 1d);
		success &= AssertEqual(logB.Exponent, 0L);

		// log_1.5(2.25) = 2
		var c = new Big(2.25, 0);
		var logC = Big.Log(c, 1.5);
		success &= AssertEqual(logC.Coefficient, 2d);
		success &= AssertEqual(logC.Exponent, 0L);

		// log_10(10) = 1
		var d = new Big(1, 1);
		var logD = Big.Log(d, 10);
		success &= AssertEqual(logD.Coefficient, 1d);
		success &= AssertEqual(logD.Exponent, 0L);

		// log_10(1000) = 3
		var e = new Big(1, 3);
		var logE = Big.Log(e, 10);
		success &= AssertEqual(logE.Coefficient, 3d);
		success &= AssertEqual(logE.Exponent, 0L);

		// log_2(1024) = 10
		var f = new Big(1.024, 3);
		var logF = Big.Log(f, 2);
		success &= AssertEqual(logF.Coefficient, 10d);
		success &= AssertEqual(logF.Exponent, 0L);

		return success;
	}

	private bool ComparisonTests()
	{
		var success = true;

		success &= AssertTrue(c > a);
		success &= AssertFalse(a > b);
		success &= AssertTrue(d > a);
		success &= AssertFalse(c > d);

		success &= AssertTrue(a < c);
		success &= AssertFalse(b < a);
		success &= AssertTrue(a < d);
		success &= AssertFalse(d < c);

		success &= AssertTrue(f > a);
		success &= AssertEqual(f.Coefficient, 1000d);

		return success;
	}
}
