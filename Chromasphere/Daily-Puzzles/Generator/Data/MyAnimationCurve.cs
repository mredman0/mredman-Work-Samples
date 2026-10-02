using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class MyAnimationCurve
{
	public MyKeyframe[] keys;
	public int length;
	public int preWrapMode;
	public int postWrapMode;

	public float Evaluate(float t)
	{
		if(t <= keys[0].time)
		{
			return keys[0].value;
		}
		if(t >= keys[length-1].time)
		{
			return keys[length-1].value;
		}

		for(int i = 0; i < length - 1; i++)
		{
			var left = keys[i];
			var right = keys[i+1];
			if(t == right.time)
			{
				return right.value;
			}
			if(t < right.time)
			{
				return Evaluate(Mathf.InverseLerp(left.time, right.time, t), left, right);
			}
		}

		// Should never get here
		return 0;
	}
	private float Evaluate(float t, MyKeyframe keyframe0, MyKeyframe keyframe1)
	{
		float dt = keyframe1.time - keyframe0.time;

		float m0 = keyframe0.outTangent * dt;
		float m1 = keyframe1.inTangent * dt;

		float t2 = t * t;
		float t3 = t2 * t;

		float a = 2 * t3 - 3 * t2 + 1;
		float b = t3 - 2 * t2 + t;
		float c = t3 - t2;
		float d = -2 * t3 + 3 * t2;

		return a * keyframe0.value + b * m0 + c * m1 + d * keyframe1.value;
	}
}

public class MyKeyframe
{
	public float time;
	public float value;
	public float inTangent;
	public float outTangent;
	public float inWeight;
	public float outWeight;
	public int weightedMode;
	public int tangentMode;
}
