namespace SCode.Compiler
{
    internal static class RandomGenerator
    {
        private const int DEFAULT_RANDOM_SIZE = 12;
        private static readonly Random random = new();

        public static int RandomNumber(int min, int max)
        {
            return random.Next(min, max);
        }

        public static string RandomString(int size = DEFAULT_RANDOM_SIZE)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            return new string(Enumerable.Repeat(chars, size)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public static string RandomStringLabel(string? prefix = null)
        {
            return prefix != null ? $"__{prefix}_{RandomString(DEFAULT_RANDOM_SIZE)}" : $"__{RandomString(DEFAULT_RANDOM_SIZE)}";
        }
    }
}
