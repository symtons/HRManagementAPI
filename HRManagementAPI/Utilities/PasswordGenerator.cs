using System.Security.Cryptography;
using System.Text;

namespace HRManagementAPI.Utilities
{
    public static class PasswordGenerator
    {
        /// <summary>
        /// Generates a secure random password with specified length
        /// </summary>
        /// <param name="length">Length of password (default: 12)</param>
        /// <param name="includeSpecialChars">Include special characters (default: true)</param>
        /// <returns>Randomly generated password</returns>
        public static string GeneratePassword(int length = 12, bool includeSpecialChars = true)
        {
            const string lowercase = "abcdefghijklmnopqrstuvwxyz";
            const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string digits = "0123456789";
            const string specialChars = "!@#$%^&*";

            // Build character set
            string charSet = lowercase + uppercase + digits;
            if (includeSpecialChars)
            {
                charSet += specialChars;
            }

            // Ensure password contains at least one of each required type
            var password = new StringBuilder();

            // Add at least one lowercase
            password.Append(lowercase[RandomNumberGenerator.GetInt32(lowercase.Length)]);

            // Add at least one uppercase
            password.Append(uppercase[RandomNumberGenerator.GetInt32(uppercase.Length)]);

            // Add at least one digit
            password.Append(digits[RandomNumberGenerator.GetInt32(digits.Length)]);

            // Add at least one special char if required
            if (includeSpecialChars)
            {
                password.Append(specialChars[RandomNumberGenerator.GetInt32(specialChars.Length)]);
            }

            // Fill remaining length with random characters from full set
            int remainingLength = length - password.Length;
            for (int i = 0; i < remainingLength; i++)
            {
                password.Append(charSet[RandomNumberGenerator.GetInt32(charSet.Length)]);
            }

            // Shuffle the password to avoid predictable patterns
            return Shuffle(password.ToString());
        }

        /// <summary>
        /// Shuffles a string using Fisher-Yates algorithm
        /// </summary>
        private static string Shuffle(string input)
        {
            char[] array = input.ToCharArray();
            int n = array.Length;

            for (int i = n - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                // Swap
                char temp = array[i];
                array[i] = array[j];
                array[j] = temp;
            }

            return new string(array);
        }

        /// <summary>
        /// Generates a TPA-branded temporary password (e.g., TPA2024!Xk9p)
        /// </summary>
        public static string GenerateTpaPassword()
        {
            int year = DateTime.Now.Year;
            string randomPart = GeneratePassword(6, true);
            return $"TPA{year}!{randomPart}";
        }
    }
}
