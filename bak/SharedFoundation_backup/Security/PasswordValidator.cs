
using System.Text.RegularExpressions;

namespace Connect2.Foundation.Security
{
    public enum PasswordStrengthLevel
    {
        Unmet,
        Weak,
        Medium,
        Strong,
    }
    public static class PasswordValidator
    {
        #region public
        public static bool IsValid(string password)
        {
            return IsWeak(password);
        }
        public static PasswordStrengthLevel GetStrengthLevel(string password)
        {
            PasswordStrengthLevel strengthLevel = PasswordStrengthLevel.Unmet;

            try
            {
                if (IsValid(password) == true)
                {
                    if (IsStrong(password) == true)
                    {
                        strengthLevel = PasswordStrengthLevel.Strong;
                    }
                    else if (IsMedium(password) == true)
                    {
                        strengthLevel = PasswordStrengthLevel.Medium;
                    }
                    else if (IsWeak(password) == true)
                    {
                        strengthLevel = PasswordStrengthLevel.Weak;
                    }
                    else
                    {
                        strengthLevel = PasswordStrengthLevel.Unmet;
                    }
                }
            }
            catch
            {
                strengthLevel = PasswordStrengthLevel.Unmet;
            }

            return strengthLevel;
        }
        #endregion

        #region private
        private static bool IsWeak(string password)
        {
            return Has8To16Chars(password) == true;//12345678
        }
        private static bool IsMedium(string password)
        {
            return Has8To16Chars(password) == true
                && ((Has1NumberAtLeast(password) == true
                        && Has1LowercaseAtLeast(password) == true
                        && Has1UppercaseAtLeast(password) == true)//1Aa
                    || (Has1NumberAtLeast(password) == true
                        && Has1UppercaseAtLeast(password) == true
                        && Has1SpecialAtLeast(password) == true)//1A!
                    || (Has1NumberAtLeast(password) == true
                        && Has1LowercaseAtLeast(password) == true
                        && Has1SpecialAtLeast(password) == true)//1a!
                    || (Has1UppercaseAtLeast(password) == true
                        && Has1LowercaseAtLeast(password) == true
                        && Has1SpecialAtLeast(password) == true)//Aa!
                        );
        }
        private static bool IsStrong(string password)
        {
            return Has8To16Chars(password) == true
                && Has1NumberAtLeast(password) == true
                && Has1LowercaseAtLeast(password) == true
                && Has1UppercaseAtLeast(password) == true
                && Has1SpecialAtLeast(password) == true;//1Aa!
        }

        private static bool Has8To16Chars(string password)
        {
            //加密算法要求不能大于16
            return !string.IsNullOrEmpty(password) && password.Length >= 8;// && password.Length <=16;
        }
        private static bool Has1NumberAtLeast(string password)
        {
            bool isValid = false;

            try
            {
                Regex regex = new Regex(@"^(.*?)[0-9]+(.*?)$");

                isValid = string.IsNullOrEmpty(password) == false
                    && regex.Match(password).Success == true;
            }
            catch
            {
                isValid = false;
            }

            return isValid;
        }
        private static bool Has1LowercaseAtLeast(string password)
        {
            bool isValid = false;

            try
            {
                Regex regex = new Regex(@"^(.*?)[a-z]+(.*?)$");

                isValid = string.IsNullOrEmpty(password) == false
                    && regex.Match(password).Success == true;
            }
            catch
            {
                isValid = false;
            }

            return isValid;
        }
        private static bool Has1UppercaseAtLeast(string password)
        {
            bool isValid = false;

            try
            {
                Regex regex = new Regex(@"^(.*?)[A-Z]+(.*?)$");

                isValid = string.IsNullOrEmpty(password) == false
                    && regex.Match(password).Success == true;
            }
            catch
            {
                isValid = false;
            }

            return isValid;
        }
        private static bool Has1SpecialAtLeast(string password)
        {
            bool isValid = false;

            try
            {
                Regex regex = new Regex(@"^(.*?)[^a-z0-9A-Z]+(.*?)$");

                isValid = string.IsNullOrEmpty(password) == false
                    && regex.Match(password).Success == true;
            }
            catch
            {
                isValid = false;
            }

            return isValid;
        }
        #endregion
    }
}
