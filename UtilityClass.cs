using Dapper;
using H2HAPICore.Model.BRI;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using RestSharp;
using System.Data;
using System.Dynamic;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Text;
using ModelBCA = H2HAPICore.Model.BCA;
using ModelPermata = H2HAPICore.Model.Permata;
using ModelCIMB = H2HAPICore.Model.CIMB;
using ModelBRI = H2HAPICore.Model.BRI;
using System.Net.NetworkInformation;

namespace H2HAPICore
{
    public static class UtilityClass
    {
        public static int PermataNID
        {
            get { return 5; }
        }

        public static string SysUserID = "s21+";
        public static int UserMaker = 1;
        public static string CompH2H = "H2H-SERVER";
        public static string IPAddH2H = "192.168.1.1";

        static Random random;

        public static string CutOffHour
        {
            get
            {
                return Program.configuration.GetSection("H2HSettings:CutOffHour").Get<string>();
            }
        }

        public static DateTime CutOffTime
        {
            get
            {
                return Convert.ToDateTime($"{DateTime.Now.ToString("yyyy-MM-dd")} {CutOffHour}");
            }
        }

        public static string ExcludeString
        {
            get
            {
                return Program.configuration.GetSection("H2HSettings:ExcludeString").Get<string>();
            }
        }

        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        public static string Base64Decode(string encodedString)
        {
            byte[] data = Convert.FromBase64String(encodedString);
            return Encoding.UTF8.GetString(data);
        }

        private static string ToHex(byte[] bytes, bool upperCase)
        {
            StringBuilder result = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
                result.Append(bytes[i].ToString(upperCase ? "X2" : "x2"));
            return result.ToString();
        }

        public static string SHA256HexHashString(string StringIn)
        {
            string hashString;
            using (var sha256 = SHA256Managed.Create())
            {
                var hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(StringIn));
                hashString = ToHex(hash, false);
            }

            return hashString;
        }

        public static String ConvertImageURLToBase64(String url)
        {
            StringBuilder _sb = new StringBuilder();

            Byte[] _byte = GetImage(url);
            if (_byte != null)
                _sb.Append(Convert.ToBase64String(_byte, 0, _byte.Length));
            else
                _sb.Append("xxx");
            return _sb.ToString();
        }

        private static byte[] GetImage(string url)
        {
            Stream stream = null;
            byte[] buf;

            try
            {
                WebProxy myProxy = new WebProxy();
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);

                HttpWebResponse response = (HttpWebResponse)req.GetResponse();
                stream = response.GetResponseStream();

                using (BinaryReader br = new BinaryReader(stream))
                {
                    int len = (int)(response.ContentLength);
                    buf = br.ReadBytes(len);
                    br.Close();
                }

                stream.Close();
                response.Close();
            }
            catch (Exception)
            {
                buf = null;
            }

            return (buf);
        }

        public static string cut(string value, int length)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= length)
                return value;

            return value.Substring(0, length);
        }

        public static dynamic DeserializeJSON(string jsonStr)
        {
            var converter = new ExpandoObjectConverter();
            return JsonConvert.DeserializeObject<ExpandoObject>(jsonStr, converter);
        }

        public static string createToken()
        {
            byte[] time = BitConverter.GetBytes(DateTime.UtcNow.ToBinary());
            byte[] key = Guid.NewGuid().ToByteArray();
            return Convert.ToBase64String(time.Concat(key).ToArray());
        }

        public static string randomString(int length, bool numberOnly)
        {
            string characters = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            string numbers = "0123456789";

            if (numberOnly)
                characters = numbers;

            StringBuilder result = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                result.Append(characters[random.Next(characters.Length)]);
            }
            return result.ToString();
        }

        public static async Task<RandomString> GetRandomString()
        {
            RandomString result = new RandomString();

            Random rnd = new Random();

            result.CustomerReference = Convert.ToString(rnd.Next(100000, 10000000));
            result.PartnerReferenceNo = Convert.ToString(rnd.Next(100000, 10000000));
            result.ExternalID = Convert.ToString(rnd.Next(100000, 10000000));

            return result;
        }

        public static string GetTimestamp()
        { 
            string timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") + "+07:00";
            return timestamp;
        }
    }
    public class RandomString
    {
        public string ExternalID { get; set; }

        public string CustomerReference { get; set; }

        public string PartnerReferenceNo { get; set; }
    }

    public static class BCAUtility
    {
        public static ModelBCA.Settings bcaSettings
        {
            get
            {
                return Program.configuration.GetSection("H2HSettings:BCA").Get<Model.BCA.Settings>();
            }
        }

        public static async Task<string> getCorporateIDKBB(IDbConnection connectionBO)
        {
            return (await connectionBO.QueryAsync<string>("SELECT CorporateIDKBB FROM [SetupFinance]")).SingleOrDefault();
        }

        public static bool validateExpToken(string token)
        {
            bool result = true;
            byte[] data = Convert.FromBase64String(token);
            DateTime created = DateTime.FromBinary(BitConverter.ToInt64(data, 0));
            int TokenExp_Sec = (int)bcaSettings.ExpireToken;

            if (created < DateTime.UtcNow.AddSeconds(-TokenExp_Sec))
            {
                result = false;
            }
            return result;
        }

        public static string GetSignature(string dataToSign)
        {
            string Sign = "";

            try
            {

                byte[] messageAsByte = Encoding.UTF8.GetBytes(dataToSign);
                string privateKeyXml = File.ReadAllText(@"C:\S21WEB\H2H\Debug\private-key\private.xml");

                RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
                rsa.FromXmlString(privateKeyXml);

                HashAlgorithm hash256Algorith = SHA256Managed.Create();

                byte[] sig256 = rsa.SignData(messageAsByte, hash256Algorith);

                Sign = Convert.ToBase64String(sig256);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
            }

            return Sign;

        }

        public static async Task<string> StringtoSign(string Method, string URL, string Token, string Payload, string timestamp)
        {
            string result = "";
            string delimiter = ":";

            result = Method + delimiter + URL + delimiter + Token + delimiter + UtilityClass.SHA256HexHashString(Payload) + delimiter + timestamp;

            return result;
        }

        public static async Task<string> GetSignatureService(string clientsecret, string toSign)
        {
            string result = "";

            byte[] keyBytes = Encoding.UTF8.GetBytes(clientsecret);
            byte[] messageBytes = Encoding.UTF8.GetBytes(toSign);

            using (HMACSHA512 hmac = new HMACSHA512(keyBytes))
            {
                byte[] hashBytes = hmac.ComputeHash(messageBytes);

                result = Convert.ToBase64String(hashBytes);
            }

            return result;
        }

    }

    public static class PermataUtility
    {

        public static ModelPermata.Settings permataSettings
        {
            get
            {
                return Program.configuration.GetSection("H2HSettings:PERMATA").Get<Model.Permata.Settings>();
            }
        }
        public static string GetSignature(string dataToSign)
        {
            string Sign = "";

            try
            {

                byte[] messageAsByte = Encoding.UTF8.GetBytes(dataToSign);
                string privateKeyXml = File.ReadAllText(@"D:\valbury\private.xml");

                RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
                rsa.FromXmlString(privateKeyXml);

                HashAlgorithm hash256Algorith = SHA256Managed.Create();

                byte[] sig256 = rsa.SignData(messageAsByte, hash256Algorith);

                Sign = Convert.ToBase64String(sig256);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
            }

            return Sign;

        }
        public static async Task<string> StringtoSign(string Method, string URL, string Token, string Payload, string timestamp)
        {
            string result = "";
            string delimiter = ":";

            result = Method + delimiter + URL + delimiter + Token + delimiter + UtilityClass.SHA256HexHashString(Payload) + delimiter + timestamp;

            return result;
        }
        public static async Task<string> GetSignatureService(string clientsecret, string toSign)
        {
            string result = "";

            byte[] keyBytes = Encoding.UTF8.GetBytes(clientsecret);
            byte[] messageBytes = Encoding.UTF8.GetBytes(toSign);

            using (HMACSHA512 hmac = new HMACSHA512(keyBytes))
            {
                byte[] hashBytes = hmac.ComputeHash(messageBytes);

                result = Convert.ToBase64String(hashBytes);
            }

            return result;
        }
    }

    public static class CIMBUtility
    {
        public static ModelCIMB.Settings cimbSettings
        {
            get
            {
                return Program.configuration.GetSection("H2HSettings:CIMB").Get<Model.CIMB.Settings>();
            }
        }
    }

    public static class BRIUtility
    {
        public static ModelBRI.Settings briSettings
        {
            get
            {
                return Program.configuration.GetSection("H2HSettings:BRI").Get<Model.BRI.Settings>();
            }
        }

        public static string GetSignature(string dataToSign)
        {
            string Sign = "";

            try
            {

                byte[] messageAsByte = Encoding.UTF8.GetBytes(dataToSign);
                string privateKeyXml = File.ReadAllText(@"D:\S21 MICROPIRANTI COMPUTER\TRIMEGAH\private.xml");

                RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
                rsa.FromXmlString(privateKeyXml);

                HashAlgorithm hash256Algorith = SHA256Managed.Create();

                byte[] sig256 = rsa.SignData(messageAsByte, hash256Algorith);

                Sign = Convert.ToBase64String(sig256);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
            }

            return Sign;

        }

        public static async Task<string> StringtoSign(string Method, string URL, string Token, string Payload, string timestamp)
        {
            string result = "";
            string delimiter = ":";

            result = Method + delimiter + URL + delimiter + Token + delimiter + UtilityClass.SHA256HexHashString(Payload) + delimiter + timestamp;

            return result;
        }

        public static async Task<string> GetSignatureService(string clientsecret, string toSign)
        {
            string result = "";

            byte[] keyBytes = Encoding.UTF8.GetBytes(clientsecret);
            byte[] messageBytes = Encoding.UTF8.GetBytes(toSign);

            using (HMACSHA512 hmac = new HMACSHA512(keyBytes))
            {
                byte[] hashBytes = hmac.ComputeHash(messageBytes);

                result = Convert.ToBase64String(hashBytes);
            }

            return result;
        }
    }
}
