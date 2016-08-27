using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Text.RegularExpressions;

namespace DQueue.Helpers
{
    public static class StringExtensions
    {
        public static string Serialize(this object content)
        {
            if (content == null) { return string.Empty; }
            var json = JsonConvert.SerializeObject(content);
            return AppendEnqueueTime(json);
        }

        public static T Deserialize<T>(this string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return default(T);
            }

            content = RemoveEnqueueTime(content);
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
            input = RemoveEnqueueTime(input);
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

        private static string AppendEnqueueTime(string input)
        {
            if (input.Length < 2) { return input; }
            if (input[0] != '{') { return input; }

            var comma = input.Length > 2 ? "," : string.Empty;
            var text = $"\"$EnqueueTime$\":\"{DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss")}\"{comma}";

            return input.Insert(01, text);
        }

        private static string RemoveEnqueueTime(string input)
        {
            var pattern = "\"\\$EnqueueTime\\$\":\"\\d{4}-\\d{2}-\\d{2} \\d{2}:\\d{2}:\\d{2}\",*";
            return Regex.Replace(input, pattern, string.Empty);
        }
    }
}
