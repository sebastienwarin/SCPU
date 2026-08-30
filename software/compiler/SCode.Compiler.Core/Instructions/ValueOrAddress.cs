namespace SCode.Compiler.Instructions
{
    public struct ValueOrAddress
    {
        public ushort Value { get; set; }
        public string Address { get; set; }

        public override readonly string ToString()
        {
            return Address ?? $"#{Value}";
        }

        public static ValueOrAddress Create(string address)
        {
            return new ValueOrAddress() { Address = address };
        }

        public static ValueOrAddress Create(ushort value)
        {
            return new ValueOrAddress() { Value = value };
        }

        public static ValueOrAddress Create(short value)
        {
            return Create((ushort)value);
        }

        public static implicit operator ValueOrAddress(int value)
        {
            if ((value << 16 >> 16 ^ value) != 0)
            {
                throw new ArgumentException("Unable to convert the integer value to an immediate 16-bit value", nameof(value));
            }
            return Create((ushort)value);
        }

        public static implicit operator ValueOrAddress(short value)
        {
            return Create(value);
        }

        public static implicit operator ValueOrAddress(ushort value)
        {
            return Create(value);
        }

        public static implicit operator ValueOrAddress(string value)
        {
            return Create(value);
        }

        public static implicit operator string(ValueOrAddress value)
        {
            return value.Address;
        }

        public static implicit operator ushort(ValueOrAddress value)
        {
            return value.Value;
        }
    }
}
