using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace ConnectTo.Foundation.Extension
{
    public static class XmlDocumentExtension
    {
        private static readonly List<RSACryptoServiceProvider> LenovoKeys = new List<RSACryptoServiceProvider>
        {
            CreateRSACryptoServiceProviderFromString("<RSAKeyValue><Modulus>twVWMufFrSa68v9h+qMIEPTNZrZcxPzdiOoDfdTuX4A51yhSpbcySu9MbeXsu4Zg8f29nQMYnYsxSVfAUpaed3Sjjnff2k6/WHpSfoGzf5oDWrzY3Nss64VrE2/C3FZ0XVlp/xRAOO51MyRQ14Pz0KVOggw4LXgjQ8Tyy379Fi9CiDdBLmSFBt2rDoGVPfiiu0XPg6H6r/jw4U3sQ2iw1eyFWXa+tKqHm4uJKvWzWZWd5Wsls4iWx5RhHgT7+O3fLRc1FPf8oXl5QdVer/1UHGHN4wRbEJDDJWKhCUCbpzLxCkd1FIxMPQgojbiz9TkBBLi35zYzhHeRABHm1DX/BQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>"),
            CreateRSACryptoServiceProviderFromString("<RSAKeyValue><Modulus>vKn97XWN3F6SXADFjQvheHsuEq5Ri1zHWyrryhm9MkEXXKgm0oh/3MoJp0mB+AeZpjO1QEGFVtT2Cj1guZuZss3xp7Zo43ERG1f8QiMSczsSDaIpH7okbLN2unoFslm7NXHEjRyqqRH8+3Ffbwz4Ge1nH5QrJj1UyR4UoxK4x9gh5r7oh5XsYeWIxHKFPbTm4xcFEcm2MVKoo+Fbb85vTweecBGJcC775jPCIAMeBj0tUYkPnCSsyl/ZDqJWxnseHZdqDllj0dAuOXB4sdQGsJPpuC+siYfezm4pAyvysJO2dgY+L7DehPmI9w8Vyf9ITUoVvdBilpSyHRylpWeloQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>")
        };

        public static bool IsValid(this XmlDocument document)
        {
            if (document == null) return false;

            return true;
        }

        public static bool IsSignedByLenovo(this XmlDocument document)
        {
            if (document == null) return false;

            var signedXml = new SignedXml(document);
            var signatures = document.GetElementsByTagName("Signature");

            if (signatures.Count == 1)
            {
                var element = (XmlElement) signatures[0];

                if (element != null)
                {
                    signedXml.LoadXml(element);

                    foreach (var key in LenovoKeys.Where(key => key != null))
                    {
                        try
                        {
                            signedXml.CheckSignature(key);
                            return true;
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }
            }

            return false;
        }

        private static RSACryptoServiceProvider CreateRSACryptoServiceProviderFromString(string keyString)
        {
            if (string.IsNullOrEmpty(keyString)) return null;

            var provider = new RSACryptoServiceProvider();

            try
            {
                provider.FromXmlString(keyString);
            }
            catch
            {
                provider = null;
            }

            return provider;
        }
    }
}
