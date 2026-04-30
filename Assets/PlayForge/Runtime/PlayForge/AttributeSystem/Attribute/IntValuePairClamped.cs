using UnityEngine;

namespace FarEmerald.PlayForge
{
    public struct IntValuePairClamped
    {
        private int _currentValue;
        public int CurrentValue
        {
            get => _currentValue;
            set
            {
                _currentValue = Mathf.Clamp(value, MinValue, MaxValue);
            }
        }

        private int _minValue;

        public int MinValue
        {
            get => _minValue;
            set
            {
                _minValue = Mathf.Min(value, _maxValue);
                _currentValue = Mathf.Clamp(_currentValue, _minValue, MaxValue);
            }
        }

        private int _maxValue;
        public int MaxValue
        {
            get => _maxValue;
            set
            {
                _maxValue = Mathf.Max(value, _minValue);
                _currentValue = Mathf.Clamp(_currentValue, MinValue, _maxValue);
            }
        }

        // public float Ratio => ForgeHelper.RelativeOffsetValue(this, _minValue);
        public float Ratio => Mathf.InverseLerp(_minValue, _maxValue, _currentValue);
        public float ClampedRatio => Mathf.Clamp01(Ratio);
        
        public bool ContainsImpact => CurrentValue != 0;

        public IntValuePairClamped(int value) : this()
        {
            _minValue = value;
            _maxValue = value;
            _currentValue = value;
        }

        public IntValuePairClamped(int value, int maxValue) : this()
        {
            _minValue = value;
            _maxValue = Mathf.Max(_minValue, maxValue);
            _currentValue = Mathf.Clamp(value, _minValue, maxValue);
        }

        public IntValuePairClamped(int value, int minValue, int maxValue) : this()
        {
            _minValue = minValue;
            _maxValue = Mathf.Max(_minValue, maxValue);
            _currentValue = Mathf.Clamp(value, minValue, maxValue);
        }

        public static IntValuePairClamped operator +(IntValuePairClamped a, IntValuePairClamped b)
        {
            return new IntValuePairClamped(a.CurrentValue + b.CurrentValue, a.MaxValue + b.MaxValue);
        }

        public static IntValuePairClamped operator -(IntValuePairClamped a, IntValuePairClamped b)
        {
            return new IntValuePairClamped(a.CurrentValue - b.CurrentValue, a.MaxValue - b.MaxValue);
        }

        public string PrettyString(bool spaceSeparated = false) => spaceSeparated ? $"{_currentValue} / {_maxValue}" : $"{_currentValue}/{_maxValue}";
        
        public override string ToString()
        {
            return $"{_minValue}/{_currentValue}/{_maxValue}";
        }
    }
}
