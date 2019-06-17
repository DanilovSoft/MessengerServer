using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace StubClient
{
    public static class Extensions
    {
        public static SecureString Secure(string password)
        {
            var secString = new SecureString();
            foreach (char ch in password)
                secString.AppendChar(ch);

            secString.MakeReadOnly();
            return secString;
        }

        public static byte[] ToByteArray(SecureString secureString, Encoding encoding = null)
        {
            if (secureString == null)
                throw new ArgumentNullException(nameof(secureString));

            encoding = encoding ?? Encoding.UTF8;

            IntPtr unmanagedString = IntPtr.Zero;
            try
            {
                unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(secureString);
                return encoding.GetBytes(Marshal.PtrToStringUni(unmanagedString));
            }
            finally
            {
                if (unmanagedString != IntPtr.Zero)
                    Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
            }
        }
    }
}
