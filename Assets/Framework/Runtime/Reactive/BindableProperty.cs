using System;

namespace GameFramework
{
    public class BindableProperty<T>
    {
        private T mValue;
        private Action<T> mOnValueChanged = value => { };

        public BindableProperty(T defaultValue = default)
        {
            mValue = defaultValue;
        }

        public T Value
        {
            get => mValue;
            set
            {
                if (value == null && mValue == null)
                    return;
                if (value != null && value.Equals(mValue))
                    return;

                mValue = value;
                mOnValueChanged(mValue);
            }
        }

        public IUnRegister Register(Action<T> onValueChanged)
        {
            mOnValueChanged += onValueChanged;
            return new BindablePropertyUnRegister<T>
            {
                BindableProperty = this,
                OnValueChanged = onValueChanged
            };
        }

        public IUnRegister RegisterWithInitValue(Action<T> onValueChanged)
        {
            onValueChanged(mValue);
            return Register(onValueChanged);
        }

        public void UnRegister(Action<T> onValueChanged)
        {
            mOnValueChanged -= onValueChanged;
        }

        public static implicit operator T(BindableProperty<T> property)
        {
            return property.Value;
        }

        public override string ToString()
        {
            return Value?.ToString();
        }

        public sealed class BindablePropertyUnRegister<TValue> : IUnRegister
        {
            public BindableProperty<TValue> BindableProperty { get; set; }
            public Action<TValue> OnValueChanged { get; set; }

            public void UnRegister()
            {
                BindableProperty.UnRegister(OnValueChanged);
                BindableProperty = null;
                OnValueChanged = null;
            }
        }
    }
}
