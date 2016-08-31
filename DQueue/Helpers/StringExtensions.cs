using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Text.RegularExpressions;

namespace DQueue.Helpers
{
    public static class StringExtensions
    {
        public const string EnqueueTime = "$EnqueueTime$";

        public static string Serialize(this object content)
        {
            if (content == null) { return string.Empty; }
            var json = JsonConvert.SerializeObject(content);
            return json.InsertField(EnqueueTime, DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss"));
        }

        public static T Deserialize<T>(this string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return default(T);
            }

            content = content.RemoveField(EnqueueTime);
            return JsonConvert.DeserializeObject<T>(content);
        }

        public static T Deserialize<T>(this RedisValue content)
        {
            if (!content.HasValue || content.IsNull)
            {
                return default(T);
            }

            return content.ToString().Deserialize<T>();
        }

        public static string GetMD5(this string input)
        {
            input = input.RemoveField(EnqueueTime);
            return HashCodeGenerator.MD5(input);
        }

        public static string GetMD5(this RedisValue value)
        {
            if (!value.HasValue || value.IsNull)
            {
                return string.Empty;
            }

            return value.ToString().GetMD5();
        }

        private static string InsertField(this string json, string name, string value)
        {
            if (json.Length < 2) { return json; }
            if (json[0] != '{') { return json; }

            name = name.Replace("\"", "\\\"");
            value = value.Replace("\"", "\\\"");

            var comma = json.Length > 2 ? "," : string.Empty;
            var field = $"\"{name}\":\"{value}\"{comma}";
            return json.Insert(1, field);
        }

        private static string RemoveField(this string json, string name)
        {
            name = name.Replace("\"", "\\\"");
            name = Regex.Escape(name);

            var pattern = "\"" + name + "\":\"((\\\\\"|[^\"])*)\",{0,1}";
            return Regex.Replace(json, pattern, string.Empty);
        }

        public static int? AsNullableInt(this string input)
        {
            int val;

            if (int.TryParse(input, out val))
            {
                return val;
            }

            return null;
        }

        public static TimeSpan? AsNullableTimeSpan(this string input)
        {
            TimeSpan result;

            if (TimeSpan.TryParse(input, out result))
            {
                return result;
            }

            var number = input.AsNullableInt();

            if (number.HasValue)
            {
                return TimeSpan.FromSeconds(number.Value);
            }

            return null;
        }
    }
}
