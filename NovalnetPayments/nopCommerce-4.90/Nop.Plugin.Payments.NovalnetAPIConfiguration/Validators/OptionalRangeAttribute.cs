using System;
using System.ComponentModel.DataAnnotations;

namespace Nop.Plugin.Payments.NovalnetAPIConfiguration.Validators;

public class OptionalRangeAttribute : ValidationAttribute
{
	private readonly int _min;
	private readonly int _max;

	public OptionalRangeAttribute(int min, int max)
	{
		_min = min;
		_max = max;
		ErrorMessage = $"Value must be empty or between {_min} and {_max}.";
	}

	public override bool IsValid(object value)
	{
		if (value == null)
			return true;

		if (value is int intValue)
		{
			if (intValue == 0)
			{
				return true;
			}
			return intValue >= _min && intValue <= _max;
		}

		return false;
	}
}

