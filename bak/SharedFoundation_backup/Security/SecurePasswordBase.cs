
using System;
using System.Runtime.InteropServices;
using System.Security;

namespace Connect2.Foundation.Security
{
    public abstract class SecurePasswordBase
    {
        #region public
        //instance
        public uint Length
        {
            get
            {
                return (uint)SecureString.Length;
            }
        }
        public bool IsValid
        {
            get
            {
                string plainPassword = GetString();
                bool isValid = PasswordValidator.IsValid(plainPassword);
                plainPassword = DestroyString(plainPassword);

                return isValid;
            }
        }
        public PasswordStrengthLevel StrengthLevel
        {
            get
            {
                string plainPassword = GetString();
                PasswordStrengthLevel strengthLevel = PasswordValidator.GetStrengthLevel(plainPassword);
                plainPassword = DestroyString(plainPassword);

                return strengthLevel;
            }
        }
        public SecureString SecureString
        {
            get;
            protected set;
        }

        public abstract string GetString();

        //class
        public static string DestroyString(string aString)
        {
            if (aString != null)
            {
                unsafe
                {
                    fixed (char* pointer = aString)
                    {
                        for (int index = 0; index < aString.Length; ++index)
                        {
                            pointer[index] = '\0';
                        }
                    }
                }
            }

            return null;
        }
        public static string GetStringFromSecureString(SecureString aSecureString)
        {
            string aString = null;
            IntPtr pointer = IntPtr.Zero;

            try
            {
                pointer = Marshal.SecureStringToBSTR(aSecureString);
                aString = Marshal.PtrToStringBSTR(pointer);
            }
            catch (Exception)
            {
                //make sure to return null if any unexpected things happened.
                aString = null;
            }
            finally
            {
                Marshal.ZeroFreeBSTR(pointer);
            }

            return aString;
        }
        public static SecureString DestroySecureString(SecureString aSecureString)
        {
            if (aSecureString != null)
            {
                if (aSecureString.IsReadOnly() == false)
                {
                    aSecureString.Clear();
                }

                aSecureString.Dispose();
                aSecureString = null;
            }

            return null;
        }
        public static SecureString GetSecureStringFromString(string aString, bool toMakeReadonly = true)
        {
            SecureString aSecureString = null;

            if (string.IsNullOrEmpty(aString) == false)
            {
                unsafe
                {
                    fixed (char* pointer = aString)
                    {
                        aSecureString = new SecureString(pointer, aString.Length);
                        if (toMakeReadonly == true)
                        {
                            aSecureString.MakeReadOnly();
                        }
                    }
                }
            }

            return aSecureString;
        }
        #endregion        
    }
}
